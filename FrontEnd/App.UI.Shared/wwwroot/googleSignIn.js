// Sign in with Google for the browser: loads Google Identity Services on demand, draws Google's
// own button into a container and hands the resulting ID token to .NET.
//
// On demand, not from index.html: the library is third-party code fetched from Google, and only
// the login page needs it. Rendering is idempotent per container, so a re-render of the page
// (a language switch re-creates it) simply draws the button again.
window.scGoogle = {
    _loading: null,

    load: function () {
        if (window.google && window.google.accounts && window.google.accounts.id) {
            return Promise.resolve();
        }
        if (!this._loading) {
            this._loading = new Promise(function (resolve, reject) {
                var script = document.createElement('script');
                script.src = 'https://accounts.google.com/gsi/client';
                script.async = true;
                script.defer = true;
                script.onload = function () { resolve(); };
                script.onerror = function () {
                    window.scGoogle._loading = null;
                    reject(new Error('Could not load Google Sign-In.'));
                };
                document.head.appendChild(script);
            });
        }
        return this._loading;
    },

    // container: the element to draw into; clientId: the Web application OAuth client id;
    // callback: a DotNetObjectReference with an OnCredential(idToken) method.
    render: async function (container, clientId, callback) {
        await this.load();

        google.accounts.id.initialize({
            client_id: clientId,
            callback: function (response) {
                if (response && response.credential) {
                    callback.invokeMethodAsync('OnCredential', response.credential);
                }
            },
            // A popup rather than a full-page redirect: the app is a single page whose state
            // (the login form, the language) would not survive a round trip through Google.
            ux_mode: 'popup',
            auto_select: false,
            itp_support: true
        });

        // Google requires a pixel width between 200 and 400; the container decides within that.
        var width = Math.max(200, Math.min(400, Math.round(container.clientWidth || 320)));
        var dark = document.documentElement.getAttribute('data-theme') === 'dark';

        google.accounts.id.renderButton(container, {
            type: 'standard',
            theme: dark ? 'filled_black' : 'outline',
            size: 'large',
            text: 'continue_with',
            shape: 'pill',
            logo_alignment: 'left',
            width: width
        });
    }
};
