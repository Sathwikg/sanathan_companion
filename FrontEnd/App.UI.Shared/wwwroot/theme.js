// Light/dark theme, applied to the document and mirrored into localStorage.
//
// The mirror exists for the first paint. Blazor cannot set data-theme until the runtime has
// started and ThemeToggle has run, which is late enough that a dark-theme user sees a full cream
// screen flash first. boot() runs from a blocking <script> in <head> and settles the attribute
// before anything renders.
//
// On the web the value is already in localStorage because LocalStorageThemeStore put it there.
// On mobile the real store is MAUI Preferences, which the WebView cannot read — so apply() writes
// localStorage too, purely as a cache for the next cold start. Preferences stays authoritative:
// if the two ever disagree, ThemeToggle overwrites this within a frame of start-up.
window.scTheme = (function () {
    var KEY = 'sc-theme';

    function set(theme) {
        document.documentElement.setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    return {
        apply: function (theme) {
            var value = theme === 'dark' ? 'dark' : 'light';
            set(value);
            try { localStorage.setItem(KEY, value); } catch (e) { /* private mode, or no storage */ }
        },

        boot: function () {
            try {
                var stored = localStorage.getItem(KEY);
                if (stored) set(stored);
            } catch (e) { /* leave the default light theme */ }
        }
    };
})();
