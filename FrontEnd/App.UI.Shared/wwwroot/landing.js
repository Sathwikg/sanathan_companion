// The features page's motion. Loaded on demand by Features.razor as an ES module; attach() wires
// it to the page root, detach() removes every listener and observer when the seeker leaves.
//
// The CSS already does what it can on its own: the hero animates on load with keyframes, the
// ambient layers drift with keyframes, and browsers with scroll-driven animations settle the
// phones into place as they scroll in. This file adds the two facts CSS cannot learn everywhere
// yet — "is this element on screen" (the reveals) and "how far has the page scrolled" (the
// parallax and the progress bar) — plus the hero phone leaning towards a mouse pointer.
//
// Everything written to the DOM is a class or a CSS variable; the drawing stays in the stylesheet.
// Under prefers-reduced-motion every reveal is applied at once and nothing else runs.

let state = null;

/**
 * @param {HTMLElement} root  the .sc-land element
 */
export function attach(root) {
    detach();
    if (!root) return;

    const reduce = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    const targets = Array.from(root.querySelectorAll('[data-reveal]'));

    if (reduce || !('IntersectionObserver' in window)) {
        targets.forEach(el => el.classList.add('is-in'));
        root.classList.add('is-ready');
        return;
    }

    // Reveal once, then forget the element: a section that has appeared stays put when the
    // seeker scrolls back up, the way a page and not a slideshow behaves.
    const observer = new IntersectionObserver(entries => {
        for (const e of entries) {
            if (!e.isIntersecting) continue;
            e.target.classList.add('is-in');
            observer.unobserve(e.target);
        }
    }, { rootMargin: '0px 0px -10% 0px', threshold: 0.1 });
    targets.forEach(el => observer.observe(el));

    // Scroll facts, written at most once per frame. --land-y moves the sky (parallax),
    // --land-prog fills the progress bar, --land-hero fades the hero as it leaves.
    const s = { root, observer, frame: 0, stage: null, onMove: null, onLeave: null, onScroll: null };
    s.onScroll = () => {
        if (s.frame) return;
        s.frame = requestAnimationFrame(() => {
            s.frame = 0;
            const y = window.scrollY || 0;
            const h = window.innerHeight || 1;
            const total = Math.max(1, (document.documentElement.scrollHeight || 1) - h);
            root.style.setProperty('--land-y', y + 'px');
            root.style.setProperty('--land-prog', Math.min(1, y / total).toFixed(4));
            root.style.setProperty('--land-hero', Math.min(1, y / h).toFixed(4));
        });
    };
    window.addEventListener('scroll', s.onScroll, { passive: true });
    window.addEventListener('resize', s.onScroll, { passive: true });
    s.onScroll();

    // Tilt: the hero phone leans towards the pointer. Only for a real pointer — a finger is not
    // hovering over anything, and the phone already floats on its own.
    const stage = root.querySelector('[data-tilt]');
    const fine = !!(window.matchMedia && window.matchMedia('(pointer: fine)').matches);
    if (stage && fine) {
        s.stage = stage;
        s.onMove = ev => {
            const r = stage.getBoundingClientRect();
            if (!r.width || !r.height) return;
            const x = (ev.clientX - r.left) / r.width - 0.5;
            const y = (ev.clientY - r.top) / r.height - 0.5;
            stage.style.setProperty('--tilt-x', (-y * 10).toFixed(2) + 'deg');
            stage.style.setProperty('--tilt-y', (x * 14).toFixed(2) + 'deg');
        };
        s.onLeave = () => {
            stage.style.setProperty('--tilt-x', '0deg');
            stage.style.setProperty('--tilt-y', '0deg');
        };
        stage.addEventListener('pointermove', s.onMove);
        stage.addEventListener('pointerleave', s.onLeave);
    }

    root.classList.add('is-ready');
    state = s;
}

/** Smooth-scrolls to an element on the page — the hero's Download button → the store section. */
export function scrollTo(id) {
    const el = document.getElementById(id);
    if (!el) return;
    const reduce = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    el.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' });
}

export function detach() {
    if (!state) return;
    const s = state;
    state = null;
    s.observer.disconnect();
    if (s.frame) cancelAnimationFrame(s.frame);
    window.removeEventListener('scroll', s.onScroll);
    window.removeEventListener('resize', s.onScroll);
    if (s.stage) {
        s.stage.removeEventListener('pointermove', s.onMove);
        s.stage.removeEventListener('pointerleave', s.onLeave);
    }
}
