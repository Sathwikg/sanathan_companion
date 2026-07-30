# Deploying to Render

Render builds **one image per service** and ignores `docker-compose.yml` entirely.
The files in this folder rebuild the compose stack as a single container so the
deployed topology still matches the local one:

```
Render edge (TLS) ──► nginx :$PORT ─┬─ /      static Blazor WebAssembly
                                    └─ /api/ ──► 127.0.0.1:8080 (Kestrel)
```

`docker-compose.yml` is untouched and is still the way to run the stack locally.

| File | Role |
| --- | --- |
| `../../render.yaml` | Blueprint: the service, its plan/region, health check and env vars |
| `Dockerfile` | Builds the API and the SPA, then packs both with nginx into one image |
| `nginx.conf.template` | Server block; `${PORT}`/`${API_PORT}` filled in at start |
| `entrypoint.sh` | Renders the config, writes the SPA's `appsettings.json`, supervises both processes |

## First deploy

1. Push this branch to GitHub.
2. Render Dashboard → **New** → **Blueprint** → select the repo and the branch.
3. Render prompts for `ConnectionStrings__DefaultConnection`. Paste the Supabase
   **session pooler** string — see the caveat below. `JwtSettings__Secret` is
   generated automatically.
4. Apply. The first build takes a while: two `dotnet publish` runs from a cold
   layer cache.

The service is live only once `GET /api/regions/options` returns 200, which
requires the database connection to work — a failing health check on first
deploy is almost always the connection string.

## Things that differ from compose

**Supabase must use the session pooler.** Render has no outbound IPv6, and
`db.<project-ref>.supabase.co` publishes only an AAAA record, so the direct
connection fails with `Network is unreachable`. Use
`aws-0-<region>.pooler.supabase.com:5432` with the `postgres.<project-ref>`
username. Port 6543 (transaction pooler) is also wrong here — it breaks the
prepared statements EF Core migrations depend on. Same constraint as Docker's
IPv4-only bridge network, for a different reason.

**`resolver 127.0.0.11` is gone.** That is Docker's embedded DNS and does not
exist on Render; `FrontEnd/docker/nginx.conf` would 502 on every `/api` call
here. Since both processes now share a network namespace, the template proxies
to a literal `127.0.0.1`, which also removes the need for the
variable-in-`proxy_pass` workaround.

**`X-Forwarded-Proto` is passed through, not overwritten.** Render terminates
TLS and forwards plain HTTP, so `$scheme` inside the container is always `http`.
Forwarding that would make `UseHttpsRedirection()` redirect every request back
to itself. The `map` at the top of the template prefers the edge's header.

**nginx listens on `$PORT`, not 80,** and only on IPv4 — see the comment in the
template.

## Known caveat: the auth rate limiter buckets every user together

`Program.cs` partitions the `auth` rate limiter (10 requests/minute) on
`HttpContext.Connection.RemoteIpAddress`. Behind a proxy that address is the
proxy's, so every user shares one bucket and the eleventh login attempt per
minute across the whole app gets a 429.

`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` does **not** fix this. That switch
enables `XForwardedHost | XForwardedProto` only — it deliberately leaves
`XForwardedFor` off. (The comment in `docker-compose.yml` claims otherwise; the
same problem exists there.)

This is pre-existing behaviour, not something the Render layout introduces, so
it is left alone here. To fix it, replace the env var with explicit options in
`Program.cs` and call `UseForwardedHeaders()` before `UseRateLimiter()`:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost;
    // Only loopback is trusted by default; nginx is loopback here, but Render's
    // edge is not, so the whole chain has to be accepted.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
```

Then drop `ASPNETCORE_FORWARDEDHEADERS_ENABLED` from `render.yaml`, otherwise
the middleware runs twice.

## Running on the free plan

This is what the free instance type actually constrains, and what has already
been done about it.

### 512 MB RAM, 0.1 CPU, shared by both processes

nginx costs a few MB; the rest is Kestrel. It fits, but not with room to spare,
so `render.yaml` sets `DOTNET_gcServer=0`. ASP.NET Core otherwise defaults to
the **server** GC, which sizes its heaps against the machine's CPU count and
deliberately delays collection to buy throughput — the wrong trade when the cap
is 512 MB and you have a tenth of a core. The workstation GC collects sooner and
keeps the resident set lower. Drop that variable if you move to a bigger plan.

The 0.1 CPU figure is a *baseline*, not a hard ceiling — Render lets instances
burst — but sustained CPU work will be throttled. Serving the Blazor payload is
mostly disk and socket, so this matters less than the memory number.

### Sleep after 15 minutes, and the cold start that follows

No inbound request for 15 minutes and the instance is stopped. The next request
has to wait for the container to start, .NET to JIT, and
`db.Database.Migrate()` to round-trip to Supabase before nginx's `/api` proxy
returns anything. Expect the first request after a sleep to take tens of
seconds; a request that arrives mid-start can 502 rather than queue.

Nothing in this repo can fix that — it is what `plan: free` means. The options
are to accept it, move to `plan: starter`, or shave the startup itself (below).

**Do not** work around it by pinging the service on a schedule from an external
cron. It converts a sleeping service into an always-on one, and the free plan
allots 750 instance-hours per month *across all your free services* — one
service awake continuously is ~744 of them. You would spend the entire quota to
avoid a cold start, and any second free service would then be starved.

### Shaving the cold start (optional, unverified here)

Publishing ReadyToRun precompiles IL to native code, which removes most of the
JIT work from startup. It is a real win for exactly this situation. It is not
applied by default because it needs a RID-specific restore and I could not
build-test it on this machine:

```diff
-RUN dotnet restore src/Sanathana.Companion.Api/Sanathana.Companion.Api.csproj
+RUN dotnet restore src/Sanathana.Companion.Api/Sanathana.Companion.Api.csproj -r linux-x64

 COPY BackEnd/src/ src/
 RUN dotnet publish src/Sanathana.Companion.Api/Sanathana.Companion.Api.csproj \
         -c Release \
         -o /app/api \
         --no-restore \
+        -r linux-x64 --no-self-contained \
+        -p:PublishReadyToRun=true \
         /p:UseAppHost=false
```

Costs a longer build and a larger image. Try it once you have a working deploy,
not before — a failed build burns metered build minutes for no diagnostic gain.

### No shell access

Render's SSH/shell is a paid feature, so on free the only window into a running
instance is the **Logs** tab. `entrypoint.sh` logs the resolved port, the
`ApiBaseUrl` it wrote, and the exit status of whichever process dies first, and
it takes the container down when either one exits rather than leaving a
half-dead instance serving 502s — all of that exists because you cannot get a
prompt to go and look yourself.

### No persistent disk

Free services get no disk, which costs this app nothing: it is stateless and all
state lives in Supabase. Do not add anything that writes to local paths and
expects it to survive — that includes file logging.

### Build minutes are metered

Two `dotnet publish` runs is not a cheap build. The Dockerfile restores from the
`.csproj` files before copying source precisely so a source-only change reuses
the cached NuGet layer, and the API and SPA are separate stages so editing one
does not rebuild the other. Keep `autoDeployTrigger: commit` in mind: every push
to the deploy branch spends minutes. Push deliberately, or set it to `off` and
deploy by hand from the dashboard.

### Free Supabase pauses too

Supabase suspends free projects after about a week of inactivity. While the
Render instance is awake its health check queries the database and keeps it
active, but a sleeping Render service issues no queries at all. Leave the whole
thing untouched for a week and the first visitor wakes Render into a paused
database — migrations fail, the health check never passes, and the app returns
500s until you resume the project from the Supabase dashboard. Worth knowing
before you conclude the deploy is broken.
