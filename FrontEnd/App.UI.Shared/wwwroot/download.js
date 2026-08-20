// Saves a string the app produced as a file.
//
// Used by the data export. A blob URL is created, clicked and revoked immediately: the payload is
// personal data, so it never goes near a server round trip or a query string just to be downloaded.
window.scDownload = {
    text: function (filename, contentType, text) {
        try {
            var blob = new Blob([text], { type: contentType || 'application/octet-stream' });
            var url = URL.createObjectURL(blob);

            var a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);

            // Give the browser a moment to start the save before the blob goes away.
            setTimeout(function () { URL.revokeObjectURL(url); }, 2000);
            return true;
        } catch (e) {
            return false;
        }
    }
};
