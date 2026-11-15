// Light/dark theme: follows the device's own setting unless the seeker picked one.
//
// Three stored values: 'light' or 'dark' (the seeker's own choice, kept until they change it) and
// nothing / 'system' (follow the phone or computer, live: switching the phone to dark at sunset
// switches the open app with it). This is how every app on a phone behaves, and the app used to
// start light whatever the phone said until someone found the toggle.
//
// The localStorage mirror exists for the first paint. Blazor cannot set data-theme until the
// runtime has started and ThemeToggle has run, which is late enough that a dark-theme user sees a
// full cream screen flash first. boot() runs from a blocking <script> in <head> and settles the
// attribute before anything renders.
//
// On the web the value is already in localStorage because LocalStorageThemeStore put it there.
// On mobile the real store is MAUI Preferences, which the WebView cannot read — so apply() writes
// localStorage too, purely as a cache for the next cold start. Preferences stays authoritative:
// if the two ever disagree, ThemeToggle overwrites this within a frame of start-up.
window.scTheme = (function () {
    var KEY = 'sc-theme';
    var media = null;
    var listeners = {};   // handle -> .NET reference
    var nextHandle = 0;

    function query() {
        if (!media && window.matchMedia) media = window.matchMedia('(prefers-color-scheme: dark)');
        return media;
    }

    function systemDark() {
        var m = query();
        return !!(m && m.matches);
    }

    function set(theme) {
        document.documentElement.setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function stored() {
        try {
            var value = localStorage.getItem(KEY);
            return value === 'dark' || value === 'light' ? value : null;
        } catch (e) { return null; }
    }

    function follow() {
        set(systemDark() ? 'dark' : 'light');
    }

    // One listener for the life of the page. It acts only while no choice is stored, so a seeker
    // who picked a theme keeps it whatever the phone does.
    function watch() {
        var m = query();
        if (!m || watch.done) return;
        watch.done = true;
        var onChange = function () {
            if (stored()) return;
            follow();
            Object.keys(listeners).forEach(function (h) {
                try { listeners[h].invokeMethodAsync('OnSystemThemeChanged', systemDark()); } catch (e) { /* page leaving */ }
            });
        };
        if (m.addEventListener) m.addEventListener('change', onChange);
        else if (m.addListener) m.addListener(onChange);   // iOS 13 and older WebKit
    }

    return {
        /** choice: 'light', 'dark', or 'system' to follow the device again. */
        apply: function (choice) {
            if (choice === 'dark' || choice === 'light') {
                set(choice);
                try { localStorage.setItem(KEY, choice); } catch (e) { /* private mode, or no storage */ }
            } else {
                try { localStorage.removeItem(KEY); } catch (e) { /* nothing stored */ }
                follow();
            }
            watch();
        },

        boot: function () {
            var choice = stored();
            if (choice) set(choice); else follow();
            watch();
        },

        isSystemDark: systemDark,

        /**
         * Components to tell when the device switches while the app follows it (several toggles
         * can be on screen). Returns a handle for unlisten: Blazor hands JS a new wrapper object for
         * the same .NET reference on every call, so the reference itself cannot be matched later.
         */
        listen: function (dotnetRef) { var h = ++nextHandle; listeners[h] = dotnetRef; watch(); return h; },
        unlisten: function (handle) { delete listeners[handle]; }
    };
})();
