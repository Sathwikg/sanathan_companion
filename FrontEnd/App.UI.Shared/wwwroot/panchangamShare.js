// Draws the day's Panchangam as one 4K portrait picture and hands it to the share sheet.
// Loaded on demand by PanchangamShare.razor as an ES module; nothing here runs at page load.
//
// Everything is drawn on a canvas rather than screenshotted from the DOM, so the picture is the
// same on the website, the phone preview and the MAUI app, and it can be far larger than any
// screen: 2160×3840 is the 9:16 frame stories and status posts want, at 4K.

const W = 2160;
const H = 3840;
const M = 120;                                   // outer margin

// The app's light palette (theme.css). Fixed on purpose: a shared picture should look the same
// whether the seeker was in light or dark mode.
const C = {
    bgTop: '#FDFBF7', bgMid: '#F8EFE0', bgBottom: '#F1E1C8',
    card: 'rgba(255, 255, 255, 0.64)',
    hairline: 'rgba(141, 78, 25, 0.22)',
    primary: '#8D4E19', accent: '#D47222', gold: '#E4A951',
    text: '#3D210A', secondary: '#705745',
    label: '#553A26',                           // row labels: darker than secondary, read at arm's length
};
const FONT_HEAD = 'Georgia, "Noto Serif", "Times New Roman", serif';
const FONT_BODY = 'system-ui, "Segoe UI", "Noto Sans", sans-serif';
// Cinzel is cut from Roman inscriptions, which is exactly the "carved" look the name wants; it is
// bundled under fonts/ and declared in app.css. Indic names fall through to the serif stack.
const FONT_NAME = '"Cinzel", Georgia, "Noto Serif", serif';

const state = { blob: null, fileName: null };

// ---------------------------------------------------------------- public API

/** Renders the picture, keeps the JPEG in memory, and returns a small preview as a data URL. */
export async function render(p) {
    await loadFonts();
    const deityImg = p.deity ? await loadImage(p.deity.imageUrl) : null;

    const canvas = document.createElement('canvas');
    canvas.width = W;
    canvas.height = H;
    const ctx = canvas.getContext('2d');
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';

    drawBackground(ctx, deityImg);
    const headerBottom = drawHeader(ctx, p, deityImg);
    // The footer runs from the ornament (nameY - 218, see drawFooter) down to the app's name; the
    // cards must stop a clear margin above the ornament.
    const footerTop = H - M - 420;
    drawSections(ctx, p.sections || [], headerBottom, footerTop);
    drawFooter(ctx, p);

    state.blob = await toBlob(canvas, 'image/jpeg', 0.92);
    state.fileName = p.fileName || 'panchangam.jpg';

    const preview = document.createElement('canvas');
    preview.width = 540;
    preview.height = 960;
    const pc = preview.getContext('2d');
    pc.imageSmoothingQuality = 'high';
    pc.drawImage(canvas, 0, 0, 540, 960);
    return preview.toDataURL('image/jpeg', 0.82);
}

/** The full-size JPEG, for a host that shares natively (the MAUI app). */
export async function getImageBytes() {
    if (!state.blob) throw new Error('Nothing has been rendered yet.');
    return new Uint8Array(await state.blob.arrayBuffer());
}

/** Whether this browser can put a file on the system share sheet (phones mostly; not Firefox). */
export function canShareFiles() {
    try {
        const probe = new File([new Blob(['x'])], 'probe.jpg', { type: 'image/jpeg' });
        return !!(navigator.canShare && navigator.canShare({ files: [probe] }));
    } catch {
        return false;
    }
}

/** Shares through the Web Share API when the browser allows it, otherwise downloads. */
export async function shareOrDownload(fileName, title, text) {
    if (!state.blob) throw new Error('Nothing has been rendered yet.');
    const file = new File([state.blob], fileName, { type: 'image/jpeg' });
    if (navigator.canShare && navigator.canShare({ files: [file] })) {
        try {
            await navigator.share({ files: [file], title, text });
            return 'shared';
        } catch (e) {
            if (e && e.name === 'AbortError') return 'cancelled';
            // Anything else (e.g. a share target that refused): fall through to the download.
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
    a.download = fileName || state.fileName || 'panchangam.jpg';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 15000);
}

// ---------------------------------------------------------------- drawing

function drawBackground(ctx, deityImg) {
    const g = ctx.createLinearGradient(0, 0, 0, H);
    g.addColorStop(0, C.bgTop);
    g.addColorStop(0.55, C.bgMid);
    g.addColorStop(1, C.bgBottom);
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, W, H);

    // The same two washes the app's hero uses: saffron high on the right, gold low on the left.
    radial(ctx, W * 0.86, 260, 900, 'rgba(212, 114, 34, 0.16)');
    radial(ctx, W * 0.14, H * 0.72, 1000, 'rgba(228, 169, 81, 0.16)');

    if (deityImg) {
        // The deity as a shadow. A photo is a rectangle, so it is first faded out towards its edges
        // on a scratch canvas (an oval mask) and only then laid behind the cards: what shows through
        // is the figure, not a pale block. A blurred, darker copy sits a little below and to the
        // right of it, which is what reads as a shadow. Centred on the middle of the page so it
        // shows between and through the cards and, below them, behind the carved name.
        const maxW = 1560, maxH = 1900;
        const s = Math.min(maxW / deityImg.naturalWidth, maxH / deityImg.naturalHeight);
        const dw = Math.round(deityImg.naturalWidth * s), dh = Math.round(deityImg.naturalHeight * s);
        const figure = feathered(deityImg, dw, dh);
        const x = (W - dw) / 2, y = H * 0.58 - dh / 2;
        ctx.save();
        if ('filter' in ctx) {
            ctx.filter = 'blur(34px)';
            ctx.globalAlpha = 0.22;
            ctx.drawImage(figure, x + 26, y + 48);
            ctx.filter = 'none';
        }
        ctx.globalAlpha = 0.18;
        ctx.drawImage(figure, x, y);
        ctx.restore();
    } else {
        ctx.save();
        ctx.globalAlpha = 0.07;
        ctx.fillStyle = C.primary;
        ctx.font = font(700, 1500, FONT_HEAD);
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText('ॐ', W / 2, H * 0.55);
        ctx.restore();
    }

    // A hairline frame, like the card edges in the app.
    ctx.save();
    ctx.strokeStyle = 'rgba(141, 78, 25, 0.30)';
    ctx.lineWidth = 3;
    roundRect(ctx, 48, 48, W - 96, H - 96, 60);
    ctx.stroke();
    ctx.restore();
}

/** Brand row, title, date and place on the left; the deity's portrait on the right. Returns the y below it. */
function drawHeader(ctx, p, deityImg) {
    let y = M + 30;

    // Brand row: the ॐ tile from the top bar, the app's name and its tagline.
    const tile = 96;
    ctx.save();
    roundRect(ctx, M, y, tile, tile, 26);
    const tg = ctx.createLinearGradient(M, y, M + tile, y + tile);
    tg.addColorStop(0, '#C0631A');
    tg.addColorStop(1, '#8D4E19');
    ctx.fillStyle = tg;
    ctx.fill();
    ctx.fillStyle = '#fff';
    ctx.font = font(700, 58, FONT_HEAD);
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('ॐ', M + tile / 2, y + tile / 2 + 4);
    ctx.textAlign = 'left';
    ctx.textBaseline = 'alphabetic';
    ctx.fillStyle = C.primary;
    ctx.font = font(700, 54, FONT_HEAD);
    ctx.fillText(p.appName || '', M + tile + 32, y + 44);
    ctx.fillStyle = C.secondary;
    ctx.font = font(400, 38, FONT_BODY);
    ctx.fillText(p.tagline || '', M + tile + 32, y + 90);
    ctx.restore();
    y += tile + 80;

    // Deity portrait, top right.
    const avatar = 330;
    const ax = W - M - avatar, ay = y;
    let avatarBottom = 0;
    if (p.deity) {
        drawAvatar(ctx, deityImg, ax, ay, avatar, p.deity.name || '', p.deityLabel || '');
        avatarBottom = ay + avatar + 150;
    }
    const textMax = p.deity ? ax - M - 70 : W - 2 * M;

    ctx.save();
    ctx.textAlign = 'left';
    ctx.textBaseline = 'alphabetic';

    ctx.fillStyle = C.text;
    ctx.font = font(700, 150, FONT_HEAD);
    for (const line of wrap(ctx, p.title || 'Panchangam', textMax)) {
        y += 150;
        ctx.fillText(line, M, y);
    }

    y += 34;
    ctx.fillStyle = C.accent;
    ctx.font = font(700, 84, FONT_HEAD);
    for (const line of wrap(ctx, p.dateLabel || '', textMax)) {
        y += 98;
        ctx.fillText(line, M, y);
    }

    if (p.locationLabel) {
        y += 26;
        ctx.fillStyle = C.secondary;
        ctx.font = font(500, 54, FONT_BODY);
        for (const line of wrap(ctx, '📍 ' + p.locationLabel, textMax)) {
            y += 68;
            ctx.fillText(line, M, y);
        }
    }
    ctx.restore();

    return Math.max(y, avatarBottom) + 70;
}

function drawAvatar(ctx, img, x, y, size, name, label) {
    const cx = x + size / 2, cy = y + size / 2;
    ctx.save();
    ctx.beginPath();
    ctx.arc(cx, cy, size / 2 + 12, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(228, 169, 81, 0.6)';
    ctx.fill();
    ctx.beginPath();
    ctx.arc(cx, cy, size / 2, 0, Math.PI * 2);
    ctx.closePath();
    ctx.clip();
    if (img) {
        const s = Math.max(size / img.naturalWidth, size / img.naturalHeight);
        const dw = img.naturalWidth * s, dh = img.naturalHeight * s;
        ctx.drawImage(img, x + (size - dw) / 2, y + (size - dh) / 2, dw, dh);
    } else {
        ctx.fillStyle = C.primary;
        ctx.fillRect(x, y, size, size);
        ctx.fillStyle = '#fff';
        ctx.font = font(700, 150, FONT_HEAD);
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText('ॐ', cx, cy + 8);
    }
    ctx.restore();

    ctx.save();
    ctx.textAlign = 'center';
    ctx.textBaseline = 'alphabetic';
    ctx.fillStyle = C.secondary;
    ctx.font = font(700, 32, FONT_BODY);
    spaced(ctx, upperIfLatin(label), cx, y + size + 58, 3);
    ctx.fillStyle = C.text;
    fitFont(ctx, name, size + 200, 700, 58, 36, FONT_HEAD);
    ctx.fillText(name, cx, y + size + 128);
    ctx.restore();
}

/**
 * The sections as stacked cards. Measured first so the whole list fits between the header and the
 * footer: when it would not, every size in the cards shrinks by one factor, never the header or
 * the name, so the day's facts stay readable and the frame stays intact.
 */
function drawSections(ctx, sections, top, bottom) {
    const avail = bottom - top;
    const needed = layoutTable(ctx, sections, 1, null);
    const s = needed > avail ? Math.max(0.5, avail / needed) : 1;
    layoutTable(ctx, sections, s, top);
}

/**
 * Each card's colour identity. Auspicious and Inauspicious are the two a reader acts on, so they
 * are the unmistakable pair — green and red — and each also carries a badge (✓ / !), so the
 * meaning survives for a reader who cannot tell the two colours apart. Calendar and Day & Elements
 * stay in the app's warm saffron and amber.
 */
const TONES = {
    calendar: { ink: '#C0631A', tint: 'rgba(212, 114, 34, 0.05)', edge: 'rgba(192, 99, 26, 0.30)' },
    day:      { ink: '#A86A12', tint: 'rgba(228, 169, 81, 0.09)', edge: 'rgba(168, 106, 18, 0.30)' },
    good:     { ink: '#2E7D32', tint: 'rgba(46, 125, 50, 0.08)',  edge: 'rgba(46, 125, 50, 0.38)', badge: 'check' },
    bad:      { ink: '#B3261E', tint: 'rgba(179, 38, 30, 0.07)',  edge: 'rgba(179, 38, 30, 0.36)', badge: 'alert' },
};

/**
 * A card's frame — base fill, tone tint, a stripe down the left edge, a tone border — and its title
 * with the short rule under it, both in the tone's ink. Returns the y where the rows start.
 */
function drawCardHead(ctx, section, x, y, w, h, pad, titleSize, s) {
    const tone = TONES[section.tone] || TONES.calendar;

    ctx.save();
    roundRect(ctx, x, y, w, h, 48);
    ctx.fillStyle = C.card;
    ctx.fill();
    ctx.fillStyle = tone.tint;
    ctx.fill();
    ctx.clip();
    ctx.fillStyle = tone.ink;
    ctx.fillRect(x, y, 16, h);
    ctx.restore();

    ctx.save();
    roundRect(ctx, x, y, w, h, 48);
    ctx.strokeStyle = tone.edge;
    ctx.lineWidth = 3;
    ctx.stroke();
    ctx.restore();

    const ty = y + pad + titleSize;
    let tx = x + pad;
    if (tone.badge) {
        const r = titleSize * 0.62;
        const cx = tx + r, cy = ty - titleSize * 0.36;
        drawBadge(ctx, tone.badge, cx, cy, r, tone.ink);
        tx += 2 * r + 18 * s;
    }
    ctx.textAlign = 'left';
    ctx.textBaseline = 'alphabetic';
    ctx.fillStyle = tone.ink;
    ctx.font = font(800, titleSize, FONT_BODY);
    spaced(ctx, upperIfLatin(section.title || ''), tx, ty, 5 * s, 'left');
    // Indic titles hang vowel signs and conjuncts below the baseline; the rule sits clear of them.
    ctx.fillRect(tx, ty + (isLatin(section.title || '') ? 14 : 30) * s, 90 * s, 5 * s);
    return ty + 22 * s + 18 * s;
}

/** A filled circle with a white check mark or exclamation mark, drawn as paths so no font is needed. */
function drawBadge(ctx, kind, cx, cy, r, color) {
    ctx.save();
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fillStyle = color;
    ctx.fill();
    ctx.strokeStyle = '#fff';
    ctx.fillStyle = '#fff';
    ctx.lineWidth = r * 0.24;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    if (kind === 'check') {
        ctx.beginPath();
        ctx.moveTo(cx - r * 0.42, cy + r * 0.02);
        ctx.lineTo(cx - r * 0.1, cy + r * 0.34);
        ctx.lineTo(cx + r * 0.46, cy - r * 0.3);
        ctx.stroke();
    } else {
        ctx.beginPath();
        ctx.moveTo(cx, cy - r * 0.46);
        ctx.lineTo(cx, cy + r * 0.12);
        ctx.stroke();
        ctx.beginPath();
        ctx.arc(cx, cy + r * 0.45, r * 0.13, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.restore();
}

/**
 * The aligned table. Each value starts right after the label column instead of at the far right
 * edge, so a label and its value read as one line. The label column is as wide as the longest
 * label on the whole picture, so every card's values line up on one edge.
 */
function layoutTable(ctx, sections, s, drawTop) {
    const draw = drawTop !== null;
    const cardX = M, cardW = W - 2 * M;
    const pad = 56 * s, titleSize = 44 * s, labelSize = 52 * s, valueSize = 60 * s;
    const lineH = valueSize * 1.32, rowGap = 26 * s, gap = 44 * s;
    const innerW = cardW - 2 * pad;
    const LABEL_WEIGHT = 600;

    ctx.font = font(LABEL_WEIGHT, labelSize, FONT_BODY);
    let widest = 0;
    for (const section of sections)
        for (const r of section.rows || [])
            widest = Math.max(widest, ctx.measureText(r.label || '').width);
    const labelW = Math.min(widest, innerW * 0.42);
    const colGap = 56 * s;
    const valueX = cardX + pad + labelW + colGap;
    const valueW = innerW - labelW - colGap;

    let y = draw ? drawTop : 0;
    let total = 0;
    for (const section of sections) {
        const rows = (section.rows || []).map(r => {
            ctx.font = font(LABEL_WEIGHT, labelSize, FONT_BODY);
            const labelLines = wrap(ctx, r.label || '', labelW);
            ctx.font = font(700, valueSize, FONT_BODY);
            const valueLines = wrap(ctx, r.value || '', valueW);
            return { labelLines, valueLines, h: Math.max(labelLines.length, valueLines.length) * lineH };
        });
        const rowsH = rows.reduce((a, r) => a + r.h + rowGap, 0) - (rows.length ? rowGap : 0);
        const cardH = pad + titleSize * 1.5 + 22 * s + rowsH + pad;

        if (draw) {
            ctx.save();
            let ry = drawCardHead(ctx, section, cardX, y, cardW, cardH, pad, titleSize, s);
            rows.forEach((r, i) => {
                if (i > 0) {
                    ctx.fillStyle = C.hairline;
                    ctx.fillRect(cardX + pad, ry - rowGap / 2, innerW, 2);
                }
                ctx.textAlign = 'left';
                ctx.fillStyle = C.label;
                ctx.font = font(LABEL_WEIGHT, labelSize, FONT_BODY);
                r.labelLines.forEach((l, n) => ctx.fillText(l, cardX + pad, ry + valueSize + n * lineH));
                ctx.fillStyle = C.text;
                ctx.font = font(700, valueSize, FONT_BODY);
                r.valueLines.forEach((l, n) => ctx.fillText(l, valueX, ry + valueSize + n * lineH));
                ry += r.h + rowGap;
            });
            ctx.restore();
        }
        y += cardH + gap;
        total += cardH + gap;
    }
    return total - (sections.length ? gap : 0);
}

/** Ornament, "Shared by", the seeker's name carved in stone, and the app's name last. */
function drawFooter(ctx, p) {
    const cx = W / 2;
    const bottom = H - M - 30;

    ctx.save();
    ctx.textAlign = 'center';
    ctx.textBaseline = 'alphabetic';

    // App name, smallest, at the very foot.
    ctx.fillStyle = C.primary;
    ctx.font = font(700, 40, FONT_HEAD);
    ctx.fillText('ॐ  ' + (p.appName || ''), cx, bottom);

    // The name: uppercase and spaced when the alphabet allows, carved with a light edge above and a
    // dark edge below, in bronze.
    const nameY = bottom - 96;
    const name = upperIfLatin(p.userName || '');
    fitFont(ctx, name, W - 2 * M - 60, 700, 132, 64, FONT_NAME);
    if ('letterSpacing' in ctx && isLatin(name)) ctx.letterSpacing = '14px';
    ctx.fillStyle = 'rgba(61, 33, 10, 0.55)';
    ctx.fillText(name, cx + 5, nameY + 5);
    ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
    ctx.fillText(name, cx - 3, nameY - 3);
    const ng = ctx.createLinearGradient(0, nameY - 130, 0, nameY + 16);
    ng.addColorStop(0, '#B8702A');
    ng.addColorStop(0.55, '#8D4E19');
    ng.addColorStop(1, '#5E300C');
    ctx.fillStyle = ng;
    ctx.fillText(name, cx, nameY);
    if ('letterSpacing' in ctx) ctx.letterSpacing = '0px';

    // "Shared by", small caps.
    ctx.fillStyle = C.secondary;
    ctx.font = font(700, 36, FONT_BODY);
    spaced(ctx, upperIfLatin(p.sharedByLabel || ''), cx, nameY - 150, 4);

    // Gold rule with a diamond, the ornament under the app's headings.
    const oy = nameY - 218;
    ctx.fillStyle = C.gold;
    ctx.fillRect(cx - 300, oy, 270, 3);
    ctx.fillRect(cx + 30, oy, 270, 3);
    ctx.save();
    ctx.translate(cx, oy + 1.5);
    ctx.rotate(Math.PI / 4);
    ctx.fillRect(-11, -11, 22, 22);
    ctx.restore();
    ctx.restore();
}

// ---------------------------------------------------------------- helpers

function font(weight, size, family) {
    return `${weight} ${Math.round(size)}px ${family}`;
}

function isLatin(text) {
    return /^[\x00-\x7FÀ-ɏ\s]*$/.test(String(text));
}

function upperIfLatin(text) {
    const t = String(text);
    return isLatin(t) ? t.toUpperCase() : t;
}

/** Sets ctx.font to the largest size in [min, start] at which text fits maxWidth. */
function fitFont(ctx, text, maxWidth, weight, start, min, family) {
    let size = start;
    ctx.font = font(weight, size, family);
    while (size > min && ctx.measureText(text).width > maxWidth) {
        size -= 2;
        ctx.font = font(weight, size, family);
    }
}

/** Letter-spaced text. Uses the canvas property where it exists, so complex scripts keep shaping. */
function spaced(ctx, text, x, y, spacing, align) {
    ctx.textAlign = align || 'center';
    // Latin only: tracking pulls an Indic word's conjuncts apart ("అ శు భ" instead of "అశుభ").
    if ('letterSpacing' in ctx && isLatin(text)) {
        ctx.letterSpacing = `${spacing}px`;
        ctx.fillText(text, x, y);
        ctx.letterSpacing = '0px';
    } else {
        ctx.fillText(text, x, y);
    }
}

/** Greedy word wrap; a single word longer than the line is split by characters. */
function wrap(ctx, text, maxWidth) {
    const words = String(text).split(/\s+/).filter(Boolean);
    const lines = [];
    let line = '';
    for (const word of words) {
        const candidate = line ? line + ' ' + word : word;
        if (ctx.measureText(candidate).width <= maxWidth) {
            line = candidate;
            continue;
        }
        if (line) lines.push(line);
        line = '';
        if (ctx.measureText(word).width <= maxWidth) {
            line = word;
            continue;
        }
        let piece = '';
        for (const ch of word) {
            if (piece && ctx.measureText(piece + ch).width > maxWidth) {
                lines.push(piece);
                piece = ch;
            } else {
                piece += ch;
            }
        }
        line = piece;
    }
    if (line) lines.push(line);
    return lines.length ? lines : [''];
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
    g.addColorStop(1, 'rgba(0, 0, 0, 0)');
    ctx.fillStyle = g;
    ctx.fillRect(x - r, y - r, 2 * r, 2 * r);
}

async function loadFonts() {
    try { await document.fonts.ready; } catch { /* no Font Loading API: system fonts it is */ }
    for (const f of ['700 132px Cinzel', '700 150px Georgia', '700 60px system-ui']) {
        try { await document.fonts.load(f); } catch { /* the fallback face draws instead */ }
    }
}

/** The picture with its edges faded to nothing: an oval mask turns a rectangular photo into a figure. */
function feathered(img, w, h) {
    const c = document.createElement('canvas');
    c.width = w;
    c.height = h;
    const g = c.getContext('2d');
    g.drawImage(img, 0, 0, w, h);
    g.globalCompositeOperation = 'destination-in';
    g.translate(w / 2, h / 2);
    g.scale(1, h / w);                            // a circle in this space is an oval on the picture
    const r = w * 0.5;
    const mask = g.createRadialGradient(0, 0, r * 0.35, 0, 0, r);
    mask.addColorStop(0, 'rgba(0, 0, 0, 1)');
    mask.addColorStop(0.72, 'rgba(0, 0, 0, 0.55)');
    mask.addColorStop(1, 'rgba(0, 0, 0, 0)');
    g.fillStyle = mask;
    g.fillRect(-w / 2, -w / 2, w, w);
    return c;
}

/** The deity picture, or null when it cannot be had; the picture then renders without it. */
async function loadImage(url) {
    if (!url) return null;
    // Fetched fresh in CORS mode rather than through <img crossorigin>. The dashboard's own <img>
    // loads the same URL without an Origin header, the API answers that without CORS headers, and
    // the browser then serves the cached copy to a CORS load, which fails: the picture came out with
    // the ॐ fallback instead of the deity. `no-store` skips that cached copy; a blob: URL is
    // same-origin, so the canvas stays exportable.
    let objectUrl = null;
    try {
        const res = await fetch(url, { mode: 'cors', cache: 'no-store' });
        if (!res.ok) return null;
        objectUrl = URL.createObjectURL(await res.blob());
        const img = new Image();
        img.decoding = 'async';
        img.src = objectUrl;
        await img.decode();
        return img;
    } catch {
        return null;
    } finally {
        // Safe once decoded: the Image keeps its pixels.
        if (objectUrl) setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
    }
}

function toBlob(canvas, type, quality) {
    return new Promise((resolve, reject) =>
        canvas.toBlob(b => b ? resolve(b) : reject(new Error('The picture could not be encoded.')), type, quality));
}
