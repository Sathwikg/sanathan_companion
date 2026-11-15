// The phone's own text-size setting, applied as the root font size (the phone app only).
//
// Every rem in theme.css, app.css and mobile.css is measured from the root, so setting
// --sc-font-scale on <html> grows text and the spacing around it together, the way a native screen
// does. The native host reads the setting (IDeviceFontScale) and calls setFontScale; the value is
// cached in localStorage so the next cold start paints at the right size before Blazor has started,
// instead of jumping a frame later.
window.scDevice = (function () {
    var KEY = 'sc-font-scale';

    function set(scale) {
        var value = Number(scale);
        if (!isFinite(value) || value <= 0) value = 1;
        document.documentElement.style.setProperty('--sc-font-scale', String(value));
        return value;
    }

    return {
        setFontScale: function (scale) {
            var value = set(scale);
            try { localStorage.setItem(KEY, String(value)); } catch (e) { /* no storage */ }
        },

        boot: function () {
            try {
                var cached = localStorage.getItem(KEY);
                if (cached) set(cached);
            } catch (e) { /* the default size */ }
        }
    };
})();
