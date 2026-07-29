// Client-side image compression: downscale to fit maxDim (keeping aspect ratio) and
// re-encode as high-quality WebP (visually lossless). Falls back to JPEG if WebP isn't supported.
window.scImage = {
    compress: function (dataUrl, maxDim, quality) {
        return new Promise(function (resolve, reject) {
            var img = new Image();
            img.onload = function () {
                var w = img.width, h = img.height;
                if (maxDim && (w > maxDim || h > maxDim)) {
                    var scale = Math.min(maxDim / w, maxDim / h);
                    w = Math.max(1, Math.round(w * scale));
                    h = Math.max(1, Math.round(h * scale));
                }
                var canvas = document.createElement('canvas');
                canvas.width = w;
                canvas.height = h;
                var ctx = canvas.getContext('2d');
                ctx.drawImage(img, 0, 0, w, h);
                var out = null;
                try { out = canvas.toDataURL('image/webp', quality); } catch (e) { out = null; }
                if (!out || out.indexOf('data:image/webp') !== 0) {
                    out = canvas.toDataURL('image/jpeg', quality);
                }
                resolve(out);
            };
            img.onerror = function () { reject('image load error'); };
            img.src = dataUrl;
        });
    }
};
