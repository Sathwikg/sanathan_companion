// Locks the page behind an open drawer or modal.
//
// CSS alone cannot do this. `overscroll-behavior: contain` only stops scroll CHAINING once a
// scroller reaches its end — it does not stop the page scrolling when the pointer is over a
// non-scrolling area, which is most of a scrim.
//
// Overlays nest (a modal can open above the drawer), so this counts holders rather than toggling
// a boolean: the lock lifts only when the last one releases.
window.scScroll = (function () {
    var holders = 0;
    var savedTop = 0;

    function apply() {
        var body = document.body;
        if (holders > 0 && !body.classList.contains('sc-noscroll')) {
            // Pin the current offset: `overflow: hidden` alone makes iOS jump to the top.
            savedTop = window.scrollY || 0;
            body.style.top = '-' + savedTop + 'px';
            body.style.position = 'fixed';
            body.style.width = '100%';
            body.classList.add('sc-noscroll');
        } else if (holders === 0 && body.classList.contains('sc-noscroll')) {
            body.classList.remove('sc-noscroll');
            body.style.position = '';
            body.style.top = '';
            body.style.width = '';
            window.scrollTo(0, savedTop);
        }
    }

    return {
        lock: function () { holders++; apply(); },
        release: function () { holders = Math.max(0, holders - 1); apply(); },
        // Called on dispose, when a component may or may not currently hold the lock.
        reset: function () { holders = 0; apply(); }
    };
})();
