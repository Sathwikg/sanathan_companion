// Thin wrapper over the browser Geolocation API for the Panchangam page.
window.scGeo = {
    current: function () {
        return new Promise(function (resolve) {
            if (!navigator.geolocation) {
                resolve({ latitude: 0, longitude: 0, accuracy: 0, error: 'Geolocation is not supported by this browser.' });
                return;
            }
            navigator.geolocation.getCurrentPosition(
                function (pos) {
                    resolve({
                        latitude: pos.coords.latitude,
                        longitude: pos.coords.longitude,
                        accuracy: pos.coords.accuracy,
                        error: null
                    });
                },
                function (err) {
                    var msg = 'Could not get your location.';
                    if (err && err.code === 1) msg = 'Location permission was denied.';
                    else if (err && err.code === 2) msg = 'Your location is currently unavailable.';
                    else if (err && err.code === 3) msg = 'Getting your location timed out.';
                    resolve({ latitude: 0, longitude: 0, accuracy: 0, error: msg });
                },
                { enableHighAccuracy: true, timeout: 10000, maximumAge: 300000 }
            );
        });
    }
};
