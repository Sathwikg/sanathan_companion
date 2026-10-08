// Phone preview frame — web host only.
//
// When this host renders the phone shell (Platform is Mobile, or the page origin is one of
// MobilePreviewOrigins — the same two facts Program.cs reads) and the window is desktop-sized,
// the app is not started in this document at all. The page becomes a handset bezel with an
// <iframe> of itself in the screen, and the app boots inside THAT. An iframe is a real viewport:
// media queries, dvh units, position:fixed sheets, safe-area maths and the body scroll lock all
// see a 412×915 phone rather than a 1920px desktop, which no CSS on a wrapper div could give
// them. The inner document sees window.self !== window.top and starts Blazor at once, so there
// is no recursion.
//
// On a narrow window — a real phone, or a desktop browser already in device emulation — there
// is nothing to frame and Blazor starts here as before. ?frame=0 opts out explicitly, for the
// odd case of wanting the phone shell at full desktop width.
//
// The MAUI host does not link this file: it is the phone.
window.scPhoneFrame = (function () {
    var SCREEN_W = 412, SCREEN_H = 915; // Pixel-class Android in CSS px, the width the bottom nav was designed at
    var BEZEL = 12;                     // must match .sc-phone-device padding in phoneFrame.css
    var MIN_W = 700, MIN_H = 520;       // below either, the window itself is the phone

    function startBlazor() { return window.Blazor.start(); }

    function wantsFrame(cfg) {
        var platform = String((cfg && cfg.Platform) || 'Web');
        if (platform.toLowerCase() === 'mobile') return true;

        var origins = cfg && Array.isArray(cfg.MobilePreviewOrigins) ? cfg.MobilePreviewOrigins : [];
        var here = window.location.origin.toLowerCase();
        return origins.some(function (o) {
            return String(o || '').replace(/\/+$/, '').toLowerCase() === here;
        });
    }

    function el(tag, className, text) {
        var node = document.createElement(tag);
        node.className = className;
        if (text) node.textContent = text;
        return node;
    }

    // Blazor navigates with history.pushState inside the iframe, so the address bar — which
    // shows the top document's URL — would stay on whatever route was first opened, and a
    // refresh would lose the seeker's place. Mirror every inner navigation onto this document.
    // Same origin, so the inner history object is ours to wrap; it is re-wrapped on every load
    // because a forced reload inside the frame replaces the window's objects.
    function mirrorHistory(inner) {
        function mirror() {
            try { window.history.replaceState(window.history.state, '', inner.location.href); }
            catch (e) { /* navigated somewhere that is not ours to mirror */ }
        }

        ['pushState', 'replaceState'].forEach(function (name) {
            var original = inner.history[name];
            inner.history[name] = function () {
                var result = original.apply(inner.history, arguments);
                mirror();
                return result;
            };
        });
        inner.addEventListener('popstate', mirror);
        mirror();
    }

    // <PageTitle> sets the inner document's title; the tab shows the outer one.
    function mirrorTitle(inner) {
        var doc = inner.document;
        function apply() { if (doc.title) document.title = doc.title; }
        apply();
        if (!('MutationObserver' in window)) return;
        new MutationObserver(apply).observe(doc.head, { childList: true, subtree: true, characterData: true });
    }

    function mount() {
        var app = document.getElementById('app');
        app.textContent = '';               // drop the boot loader; the phone shows its own
        app.className = 'sc-phone-stage';

        var slot = el('div', 'sc-phone-slot');
        var device = el('div', 'sc-phone-device');
        var screen = el('div', 'sc-phone-screen');
        var frame = document.createElement('iframe');
        frame.className = 'sc-phone-frame';
        frame.title = document.title + ', phone preview';
        frame.src = window.location.href;  // same route, query and hash, so deep links survive

        screen.appendChild(frame);
        device.appendChild(screen);
        slot.appendChild(device);
        app.appendChild(slot);
        app.appendChild(el('p', 'sc-phone-caption',
            SCREEN_W + ' × ' + SCREEN_H + ' phone preview · the app inside runs as it would on a phone · add ?frame=0 to the URL for full width'));

        // Scale the whole handset to fit the window rather than shrinking the screen: the app
        // inside must keep seeing exactly 412×915, whatever the desktop window happens to be.
        function fit() {
            var w = SCREEN_W + 2 * BEZEL, h = SCREEN_H + 2 * BEZEL;
            var scale = Math.min(1, (window.innerWidth - 32) / w, (window.innerHeight - 96) / h);
            slot.style.width = Math.round(w * scale) + 'px';
            slot.style.height = Math.round(h * scale) + 'px';
            device.style.transform = 'scale(' + scale + ')';
        }
        fit();
        window.addEventListener('resize', fit);

        frame.addEventListener('load', function () {
            var inner = frame.contentWindow;
            if (!inner) return;
            mirrorHistory(inner);
            mirrorTitle(inner);
        });

        // The app inside writes its theme to localStorage (theme.js); the backdrop follows it.
        window.addEventListener('storage', function (e) {
            if (e.key === 'sc-theme') window.scTheme.boot();
        });
    }

    return {
        boot: function () {
            // Inside the frame, or on a phone-sized window: nothing to do but start the app.
            if (window.self !== window.top
                || window.innerWidth < MIN_W || window.innerHeight < MIN_H
                || new URLSearchParams(window.location.search).get('frame') === '0') {
                startBlazor();
                return;
            }

            // Only a desktop-sized top-level window pays for this, and Blazor is about to
            // request the same file, so it costs one round trip at most.
            fetch('appsettings.json', { cache: 'no-cache' })
                .then(function (r) { return r.ok ? r.json() : null; })
                .catch(function () { return null; })
                .then(function (cfg) { if (cfg && wantsFrame(cfg)) mount(); else startBlazor(); });
        }
    };
})();
