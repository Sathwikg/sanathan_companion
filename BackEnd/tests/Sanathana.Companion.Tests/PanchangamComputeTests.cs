using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sanathana.Companion.Api.RateLimiting;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Sanathana.Companion.Application.Panchangam;
using Sanathana.Companion.Application.Services;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The compute endpoint's three defences: a cache that must not change any answer, a calculator
/// whose optimisations must be value-preserving, and a rate-limit partition that only works
/// because the limiter now runs after authentication.
/// </summary>
public class PanchangamComputeTests
{
    private static readonly (string Name, double Lat, double Lon)[] Places =
    {
        ("hyderabad", 17.39, 78.49),
        ("reykjavik", 64.15, -21.94),   // high latitude, where a small change in the maths shows
    };

    // ---------------------------------------------------------------- the answer must not move

    /// <summary>
    /// A digest of every field of every day of 2026 at both places, captured from the calculator
    /// as it stood before the elongation carry-forward and the Ugadi memo were added.
    /// </summary>
    /// <remarks>
    /// If this fails, an "optimisation" changed an answer. Regenerate it only with a reason: the
    /// point of the constant is that speeding the calculator up must not move a sunrise.
    /// </remarks>
    private const string FullYear2026Digest = "92A9CB34380028B8EA4F0C821A066D1B5B8F087F0593AD55A90B126B35A82D58";

    [Fact]
    public void A_whole_year_at_two_places_still_computes_exactly_what_it_used_to()
    {
        var sb = new StringBuilder();
        foreach (var (name, lat, lon) in Places)
            for (var d = new DateOnly(2026, 1, 1); d.Year == 2026; d = d.AddDays(1))
                sb.Append(name).Append('|').Append(d).Append('|')
                  .Append(JsonSerializer.Serialize(PanchangamCalculator.Compute(d, lat, lon))).Append('\n');

        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));

        Assert.Equal(FullYear2026Digest, digest);
    }

    [Fact]
    public void And_the_sample_days_match_field_by_field_so_a_failure_says_which_one()
    {
        // The digest above proves nothing moved; this one says what moved when it does.
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "panchangam-2026-sample.json");
        using var golden = JsonDocument.Parse(File.ReadAllText(path));

        foreach (var row in golden.RootElement.EnumerateArray())
        {
            var place = row.GetProperty("place").GetString()!;
            var date = DateOnly.Parse(row.GetProperty("date").GetString()!);
            var (_, lat, lon) = Places.Single(p => p.Name == place);

            var computed = JsonSerializer.SerializeToElement(PanchangamCalculator.Compute(date, lat, lon));

            foreach (var expected in row.GetProperty("day").EnumerateObject())
            {
                var actual = computed.GetProperty(expected.Name);
                Assert.True(expected.Value.ToString() == actual.ToString(),
                    $"{place} {date:yyyy-MM-dd} {expected.Name}: expected {expected.Value}, got {actual}");
            }
        }
    }

    [Fact]
    public void The_ugadi_memo_answers_the_same_as_a_fresh_computation_across_the_whole_supported_range()
    {
        // The memo is only bounded because ComputeAtLocationAsync refuses dates outside 1900-2100.
        // Walk the range twice: the second pass is served entirely from the dictionary.
        var first = new Dictionary<int, string>();
        for (var year = 1900; year <= 2100; year++)
            first[year] = JsonSerializer.Serialize(PanchangamCalculator.Compute(new DateOnly(year, 4, 15), 17.39, 78.49));

        for (var year = 1900; year <= 2100; year++)
            Assert.Equal(first[year],
                JsonSerializer.Serialize(PanchangamCalculator.Compute(new DateOnly(year, 4, 15), 17.39, 78.49)));
    }

    // ---------------------------------------------------------------- the cache

    [Fact]
    public void Two_points_in_one_cell_get_the_same_answer_whichever_arrived_first()
    {
        // The bug this pins: rounding only the KEY and computing from the raw coordinates would
        // make the answer depend on who warmed the entry, so the same request would return
        // different sunrises cold and warm.
        using var cache = new PanchangamComputeCache();
        var date = new DateOnly(2026, 8, 20);

        var west = cache.GetOrCompute(date, 17.3812, 78.4867);
        var east = cache.GetOrCompute(date, 17.3849, 78.4867);

        Assert.Same(west, east);
        Assert.Equal(west.Sunrise, east.Sunrise);
        Assert.Equal(west.Sunset, east.Sunset);
    }

    [Fact]
    public void And_it_is_the_same_answer_a_cold_calculator_would_have_given_for_the_cell()
    {
        using var cache = new PanchangamComputeCache();
        var date = new DateOnly(2026, 8, 20);

        var cached = cache.GetOrCompute(date, 17.3849, 78.4867);
        var direct = PanchangamCalculator.Compute(date, 17.38, 78.49);

        Assert.Equal(JsonSerializer.Serialize(direct), JsonSerializer.Serialize(cached));
    }

    [Fact]
    public void Neighbouring_cells_are_separate_entries()
    {
        using var cache = new PanchangamComputeCache();
        var date = new DateOnly(2026, 8, 20);

        Assert.NotSame(cache.GetOrCompute(date, 17.38, 78.49), cache.GetOrCompute(date, 17.39, 78.49));
    }

    // ---------------------------------------------------------------- the rate-limit partition

    [Fact]
    public void A_signed_in_seeker_gets_their_own_bucket()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(JwtRegisteredClaimNames.Sub, "11111111-1111-1111-1111-111111111111") }, "test"))
        };

        Assert.Equal("user:11111111-1111-1111-1111-111111111111", ComputeRateLimitPartition.For(context));
    }

    [Fact]
    public void An_anonymous_caller_falls_back_to_the_address()
    {
        // This is what every caller looked like when the limiter ran before UseAuthentication —
        // one bucket per NAT rather than one per seeker.
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");

        Assert.Equal("addr:203.0.113.7", ComputeRateLimitPartition.For(context));
    }
}
