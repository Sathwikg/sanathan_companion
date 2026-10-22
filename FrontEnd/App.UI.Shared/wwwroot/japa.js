// The japa screen's tap surface: a tap anywhere on the screen counts one repetition.
// Loaded on demand by JapaChant.razor as an ES module; nothing here runs at page load, and
// detach() removes every listener when the seeker leaves the screen.
//
// Why here and not in Razor: the feedback has to be instant and must never cost a re-render of
// the chant text the seeker is reading. The ripple and the count's bump are drawn straight into
// the DOM, and the browser's vibration has to start inside the gesture itself. .NET is told only
// that a repetition happened.
//
// Battery: nothing runs between taps — no timers, no animation loops. Each tap is one short CSS
// animation on a compositor-only property (transform/opacity), and in lite mode not even that.

// Anything the seeker taps to DO something is not a chant: the controls, the audio player, the
// app's own chrome, and whatever sheet or dialog is open over the page.
const IGNORE = [
    'a', 'button', 'input', 'select', 'textarea', 'label', 'audio', 'video', 'summary',
    '[contenteditable]', '[role="button"]', '[data-jp-ignore]',
    'header', 'nav', 'aside', '.sc-topbar', '.scm-topbar', '.scm-bottomnav',
    '.sc-modal-backdrop', '.scm-sheet', '.scm-sheet-scrim', '.sc-toast-host'
].join(',');

const SLOP_PX = 12;          // further than this between down and up is a scroll or a drag
const LONG_PRESS_MS = 650;   // held longer than this is a pause, not a chant
const MAX_RIPPLES = 6;       // fast tapping never stacks up more work than this
const SCROLL_SETTLE_MS = 180; // a touch this soon after the page moved is stopping a fling, not chanting

let state = null;

/**
 * Starts listening. options: { lite, webVibrate, tickMs }.
 * lite drops the ripple and the bump; webVibrate makes the browser vibrate on each tap.
 */
export function attach(dotnet, options) {
    detach();

    const layer = document.createElement('div');
    layer.className = 'sc-jp-ripples';
    layer.setAttribute('aria-hidden', 'true');
    document.body.appendChild(layer);

    state = { dotnet, opts: Object.assign({ lite: false, webVibrate: false, tickMs: 12 }, options), start: null, layer, live: 0, scrolledAt: -Infinity };

    document.documentElement.classList.add('sc-jp-on');
    document.addEventListener('pointerdown', onDown, { passive: true });
    document.addEventListener('pointerup', onUp, { passive: true });
    document.addEventListener('pointercancel', onCancel, { passive: true });
    window.addEventListener('scroll', onScroll, { passive: true });
    document.addEventListener('keydown', onKey);
}

export function update(options) {
    if (state) Object.assign(state.opts, options);
}

export function detach() {
    document.removeEventListener('pointerdown', onDown);
    document.removeEventListener('pointerup', onUp);
    document.removeEventListener('pointercancel', onCancel);
    window.removeEventListener('scroll', onScroll);
    document.removeEventListener('keydown', onKey);
    document.documentElement.classList.remove('sc-jp-on');
    if (state && state.layer) state.layer.remove();
    state = null;
}

/** True where the browser can drive the motor (Android Chrome; not iOS Safari, not desktops). */
export function canVibrate() {
    return typeof navigator.vibrate === 'function';
}

/** A pattern or a duration in ms. Used by .NET for the long completed-mala vibration. */
export function vibrate(pattern) {
    try { return canVibrate() && navigator.vibrate(pattern); }
    catch { return false; }
}

// ---------------------------------------------------------------- gesture

function ignored(target) {
    return !(target instanceof Element) || target.closest(IGNORE) !== null;
}

function onDown(e) {
    if (!state) return;
    if (!e.isPrimary || (e.pointerType === 'mouse' && e.button !== 0) || ignored(e.target)
        || performance.now() - state.scrolledAt < SCROLL_SETTLE_MS) {
        state.start = null;
        return;
    }
    state.start = { x: e.clientX, y: e.clientY, t: e.timeStamp, id: e.pointerId };
}

function onUp(e) {
    const s = state && state.start;
    if (!s || e.pointerId !== s.id) return;
    state.start = null;

    const dx = e.clientX - s.x;
    const dy = e.clientY - s.y;
    if (dx * dx + dy * dy > SLOP_PX * SLOP_PX) return;   // reading: the seeker scrolled or dragged
    if (e.timeStamp - s.t > LONG_PRESS_MS) return;       // reading: the seeker paused on a line
    if (ignored(e.target)) return;

    count(e.clientX, e.clientY);
}

function onCancel() {
    if (state) state.start = null;
}

// A scroll mid-touch means the seeker was reading; a touch that lands while the page is still
// gliding from a fling is them stopping it. Neither is a chant.
function onScroll() {
    if (!state) return;
    state.start = null;
    state.scrolledAt = performance.now();
}

// Space or Enter counts on a desktop, so a seeker at a keyboard can chant without the mouse.
function onKey(e) {
    if (!state || e.repeat || e.altKey || e.ctrlKey || e.metaKey) return;
    if (e.key !== ' ' && e.key !== 'Enter') return;
    if (e.target !== document.body && ignored(e.target)) return;
    if (document.querySelector('.sc-modal-backdrop, .scm-sheet')) return;
    e.preventDefault();                                   // Space would otherwise scroll the page
    count(null, null);
}

// ---------------------------------------------------------------- feedback

function count(x, y) {
    const o = state.opts;
    if (o.webVibrate) vibrate(o.tickMs);
    if (!o.lite) {
        if (x !== null) ripple(x, y);
        bump();
    }
    state.dotnet.invokeMethodAsync('OnTap');
}

// A soft light where the finger landed: low opacity, gone in half a second, never over the text
// for long enough to read as anything but an acknowledgement.
function ripple(x, y) {
    if (state.live >= MAX_RIPPLES) return;
    const r = document.createElement('span');
    r.className = 'sc-jp-ripple';
    r.style.left = x + 'px';
    r.style.top = y + 'px';
    state.live++;
    const layer = state.layer;
    r.addEventListener('animationend', () => {
        r.remove();
        if (state && state.layer === layer) state.live--;
    }, { once: true });
    layer.appendChild(r);
}

// The counter breathes once per chant. Restarting a CSS animation needs the class off, a reflow,
// and the class back on.
function bump() {
    const el = document.querySelector('[data-jp-bump]');
    if (!el) return;
    el.classList.remove('is-bump');
    void el.offsetWidth;
    el.classList.add('is-bump');
}
