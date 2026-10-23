// Draws a completed mala — the chant, the streak, the totals — as one portrait picture and hands
// it to the share sheet. Loaded on demand by SadhanaComplete.razor as an ES module; nothing here
// runs at page load, and nothing is drawn until the seeker taps Share.
//
// Drawn on a canvas rather than screenshotted from the DOM, so the picture is identical on the web,
// in the phone app, in light or dark mode. 1080×1920 is the 9:16 frame status posts and stories
// use — sharp on any phone, and a quarter of the memory of a 4K canvas.

const W = 1080;
const H = 1920;
const CX = W / 2;

// The app's light palette (theme.css), fixed so a shared picture looks the same whatever the theme.
const C = {
    bgTop: '#FDF8EF', bgMid: '#F8EAD3', bgBottom: '#F0DAB4',
    card: 'rgba(255, 255, 255, 0.66)',
    hairline: 'rgba(141, 78, 25, 0.20)',
    primary: '#8D4E19', accent: '#D47222', gold: '#E4A951',
    text: '#3D210A', secondary: '#705745',
    okBg: 'rgba(47, 125, 79, 0.12)', ok: '#2F7D4F'
};
const FONT_HEAD = 'Georgia, "Noto Serif", "Times New Roman", serif';
const FONT_BODY = 'system-ui, "Segoe UI", "Noto Sans", sans-serif';
const FONT_NAME = '"Cinzel", Georgia, "Noto Serif", serif';

const state = { blob: null, fileName: null };

// ---------------------------------------------------------------- public API

/** Draws the picture and keeps the JPEG in memory for share / download. */
export async function render(p) {
    if (document.fonts && document.fonts.ready) {
        try { await document.fonts.ready; } catch { /* draw with whatever has loaded */ }
    }
    const logo = await loadImage(p.logoUrl);

    const canvas = document.createElement('canvas');
    canvas.width = W;
    canvas.height = H;
    const ctx = canvas.getContext('2d');
    ctx.imageSmoothingQuality = 'high';

    drawBackground(ctx);
    drawHeader(ctx, p, logo);
    const circleBottom = drawStreak(ctx, p);
    const afterChip = drawSecured(ctx, p.secured, circleBottom + 56);
    const afterChant = drawChant(ctx, p, afterChip + 40);
    drawStats(ctx, p.stats || [], Math.max(afterChant + 50, 1300));
    drawFooter(ctx, p);

    state.blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.92));
    state.fileName = p.fileName || 'sadhana.jpg';
    // The canvas is dropped here; only the compressed bytes stay in memory.
}

export async function getImageBytes() {
    if (!state.blob) throw new Error('Nothing has been rendered yet.');
    return new Uint8Array(await state.blob.arrayBuffer());
}

/** The browser's share sheet where it can take a file, otherwise a download. */
export async function shareOrDownload(fileName, title, text) {
    if (!state.blob) throw new Error('Nothing has been rendered yet.');
    const file = new File([state.blob], fileName, { type: 'image/jpeg' });
    if (navigator.canShare && navigator.canShare({ files: [file] })) {
        try {
            await navigator.share({ files: [file], title, text });
            return 'shared';
        } catch (e) {
            if (e && e.name === 'AbortError') return 'cancelled';
            // Anything else (a refused share target, an expired gesture): fall through to download.
        }
    }
    download(fileName);
    return 'downloaded';
}

export function download(fileName) {
    if (!state.blob) throw new Error('Nothing has been rendered yet.');
    const url = URL.createObjectURL(state.blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName || state.fileName || 'sadhana.jpg';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 15000);
}

// ---------------------------------------------------------------- drawing

function drawBackground(ctx) {
    const g = ctx.createLinearGradient(0, 0, 0, H);
    g.addColorStop(0, C.bgTop);
    g.addColorStop(0.55, C.bgMid);
    g.addColorStop(1, C.bgBottom);
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, W, H);

    // A warm light behind the streak, and the mandala it sits in.
    radial(ctx, CX, 700, 560, 'rgba(228, 169, 81, 0.30)');
    ctx.save();
    ctx.translate(CX, 700);
    ctx.strokeStyle = 'rgba(141, 78, 25, 0.10)';
    ctx.lineWidth = 2;
    for (const r of [300, 360, 430]) {
        ctx.beginPath();
        ctx.arc(0, 0, r, 0, Math.PI * 2);
        ctx.stroke();
    }
    ctx.fillStyle = 'rgba(228, 169, 81, 0.13)';
    for (let i = 0; i < 24; i++) {
        ctx.rotate(Math.PI / 12);
        ctx.beginPath();
        ctx.ellipse(0, -360, 22, 64, 0, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.restore();

    // A quiet border, inset like a printed card.
    ctx.strokeStyle = C.hairline;
    ctx.lineWidth = 3;
    roundRect(ctx, 36, 36, W - 72, H - 72, 44);
    ctx.stroke();
}

function drawHeader(ctx, p, logo) {
    const size = 76;
    const y = 120;
    ctx.font = font(700, 44, FONT_NAME);
    const nameW = ctx.measureText(p.appName || '').width;
    const total = size + 22 + nameW;
    const x = CX - total / 2;

    if (logo) {
        ctx.save();
        roundRect(ctx, x, y, size, size, 20);
        ctx.clip();
        ctx.drawImage(logo, x, y, size, size);
        ctx.restore();
    } else {
        ctx.fillStyle = C.primary;
        roundRect(ctx, x, y, size, size, 20);
        ctx.fill();
        ctx.fillStyle = '#FFF4E2';
        ctx.font = font(700, 46, FONT_HEAD);
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(p.logoGlyph || 'ॐ', x + size / 2, y + size / 2 + 2);
    }

    ctx.fillStyle = C.primary;
    ctx.font = font(700, 44, FONT_NAME);
    ctx.textAlign = 'left';
    ctx.textBaseline = 'middle';
    ctx.fillText(p.appName || '', x + size + 22, y + size / 2 + 2);

    ctx.fillStyle = C.accent;
    ctx.textAlign = 'center';
    spaced(ctx, upperIfLatin(p.kicker || ''), CX, 300, font(700, 40, FONT_BODY), 6);
}

/** The streak in a medallion; returns the medallion's bottom edge. */
function drawStreak(ctx, p) {
    const cy = 700;
    const r = 230;

    const fill = ctx.createRadialGradient(CX, cy - 60, 20, CX, cy, r);
    fill.addColorStop(0, '#FFFBF3');
    fill.addColorStop(1, '#F6DCAE');
    ctx.fillStyle = fill;
    ctx.beginPath();
    ctx.arc(CX, cy, r, 0, Math.PI * 2);
    ctx.fill();

    const ring = ctx.createLinearGradient(CX - r, cy - r, CX + r, cy + r);
    ring.addColorStop(0, C.gold);
    ring.addColorStop(1, C.accent);
    ctx.strokeStyle = ring;
    ctx.lineWidth = 10;
    ctx.stroke();

    ctx.textAlign = 'center';
    ctx.textBaseline = 'alphabetic';
    if (p.streak === null || p.streak === undefined) {
        ctx.font = font(400, 170, FONT_BODY);
        ctx.fillText('🪷', CX, cy + 60);
        return cy + r;
    }

    ctx.font = font(400, 84, FONT_BODY);
    ctx.fillText('🔥', CX, cy - 92);
    ctx.fillStyle = C.primary;
    fitFont(ctx, String(p.streak), r * 1.5, 700, 200, 90, FONT_HEAD);
    ctx.fillText(String(p.streak), CX, cy + 76);
    ctx.fillStyle = C.secondary;
    spaced(ctx, upperIfLatin(p.streakLabel || ''), CX, cy + 140, font(700, 32, FONT_BODY), 4);
    return cy + r;
}

function drawSecured(ctx, text, top) {
    if (!text) return top - 20;
    ctx.font = font(700, 34, FONT_BODY);
    const label = '✓  ' + text;
    const w = Math.min(ctx.measureText(label).width + 80, W - 160);
    const h = 74;
    // Solid first, so the mandala's petals do not show through the tint.
    roundRect(ctx, CX - w / 2, top, w, h, h / 2);
    ctx.fillStyle = C.bgTop;
    ctx.fill();
    ctx.fillStyle = C.okBg;
    ctx.fill();
    ctx.fillStyle = C.ok;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(label, CX, top + h / 2 + 1, w - 40);
    return top + h;
}

function drawChant(ctx, p, top) {
    ctx.fillStyle = C.text;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'alphabetic';
    ctx.font = font(700, 70, FONT_HEAD);
    const lines = wrap(ctx, p.chantName || '', W - 200).slice(0, 2);
    let y = top + 70;
    for (const line of lines) {
        ctx.fillText(line, CX, y);
        y += 84;
    }
    ctx.fillStyle = C.secondary;
    ctx.font = font(500, 38, FONT_BODY);
    ctx.fillText(p.malaLine || '', CX, y - 10, W - 200);
    return y;
}

function drawStats(ctx, stats, top) {
    if (stats.length === 0) return;
    const x = 110;
    const w = W - 220;
    const h = 250;
    ctx.fillStyle = C.card;
    roundRect(ctx, x, top, w, h, 40);
    ctx.fill();
    ctx.strokeStyle = C.hairline;
    ctx.lineWidth = 2;
    ctx.stroke();

    const col = w / stats.length;
    stats.forEach((s, i) => {
        const cx = x + col * i + col / 2;
        if (i > 0) {
            ctx.strokeStyle = C.hairline;
            ctx.beginPath();
            ctx.moveTo(x + col * i, top + 50);
            ctx.lineTo(x + col * i, top + h - 50);
            ctx.stroke();
        }
        ctx.textAlign = 'center';
        ctx.textBaseline = 'alphabetic';
        ctx.fillStyle = C.primary;
        fitFont(ctx, String(s.value), col - 30, 700, 76, 40, FONT_HEAD);
        ctx.fillText(String(s.value), cx, top + 128);
        ctx.fillStyle = C.secondary;
        fitFont(ctx, s.label || '', col - 30, 600, 30, 20, FONT_BODY);
        ctx.fillText(s.label || '', cx, top + 182);
    });
}

function drawFooter(ctx, p) {
    ctx.textAlign = 'center';
    ctx.textBaseline = 'alphabetic';
    ctx.fillStyle = C.secondary;
    ctx.font = font(600, 34, FONT_BODY);
    ctx.fillText(p.dateLabel || '', CX, H - 250, W - 200);
    if (p.userName) {
        ctx.font = font(500, 32, FONT_BODY);
        ctx.fillText(`${p.sharedByLabel || ''} ${p.userName}`.trim(), CX, H - 196, W - 200);
    }
    ctx.fillStyle = C.primary;
    ctx.font = font(600, 30, FONT_BODY);
    ctx.fillText(p.tagline || '', CX, H - 110, W - 200);
}

// ---------------------------------------------------------------- helpers

function font(weight, size, family) {
    return `${weight} ${size}px ${family}`;
}

function isLatin(text) {
    return /^[\u0000-ɏ\s\d\p{P}\p{S}]*$/u.test(text);
}

// Capitals only where the script has them; Telugu or Hindi passes through untouched.
function upperIfLatin(text) {
    return isLatin(text) ? text.toLocaleUpperCase() : text;
}

function fitFont(ctx, text, maxWidth, weight, start, min, family) {
    let size = start;
    ctx.font = font(weight, size, family);
    while (size > min && ctx.measureText(text).width > maxWidth) {
        size -= 2;
        ctx.font = font(weight, size, family);
    }
}

function spaced(ctx, text, x, y, f, spacing) {
    ctx.font = f;
    if ('letterSpacing' in ctx) {
        ctx.letterSpacing = `${spacing}px`;
        ctx.fillText(text, x, y);
        ctx.letterSpacing = '0px';
    } else {
        ctx.fillText(text, x, y);
    }
}

function wrap(ctx, text, maxWidth) {
    const words = String(text).split(/\s+/).filter(Boolean);
    const lines = [];
    let line = '';
    for (const word of words) {
        const next = line ? `${line} ${word}` : word;
        if (ctx.measureText(next).width <= maxWidth || !line) {
            line = next;
        } else {
            lines.push(line);
            line = word;
        }
    }
    if (line) lines.push(line);
    // A second line that still overflows is shortened rather than spilling off the card.
    return lines.map(l => {
        if (ctx.measureText(l).width <= maxWidth) return l;
        let cut = l;
        while (cut.length > 1 && ctx.measureText(cut + '…').width > maxWidth) cut = cut.slice(0, -1);
        return cut + '…';
    });
}

function roundRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
}

function radial(ctx, x, y, r, color) {
    const g = ctx.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, color);
    g.addColorStop(1, 'rgba(255, 255, 255, 0)');
    ctx.fillStyle = g;
    ctx.fillRect(x - r, y - r, r * 2, r * 2);
}

async function loadImage(url) {
    if (!url) return null;
    return new Promise(resolve => {
        const img = new Image();
        img.onload = () => resolve(img);
        img.onerror = () => resolve(null);
        img.src = url;
    });
}
