// Minimal rich-text bridge for RichTextEditor.razor. Uses a contenteditable div so the
// app needs no third-party editor dependency.
window.scRichText = {
    init: function (el, dotnetRef) {
        if (!el || el.dataset.scRteReady === '1') return;
        el.dataset.scRteReady = '1';

        var push = function () { dotnetRef.invokeMethodAsync('OnHtmlChanged', el.innerHTML); };
        el.addEventListener('input', push);
        el.addEventListener('blur', push);

        // Paste as plain text — keeps pasted Word/web markup out of the stored HTML.
        el.addEventListener('paste', function (e) {
            e.preventDefault();
            var text = ((e.clipboardData || window.clipboardData) || { getData: function () { return ''; } }).getData('text/plain');
            document.execCommand('insertText', false, text);
        });
    },

    setHtml: function (el, html) {
        if (el && el.innerHTML !== (html || '')) el.innerHTML = html || '';
    },

    exec: function (el, command, value) {
        if (!el) return '';
        el.focus();
        try { document.execCommand(command, false, value || null); } catch (e) { /* unsupported command */ }
        return el.innerHTML;
    },

    isEmpty: function (el) {
        return !el || el.textContent.trim().length === 0;
    }
};
