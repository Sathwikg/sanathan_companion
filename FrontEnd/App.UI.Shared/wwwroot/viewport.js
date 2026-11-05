// Answers "is the viewport narrow right now?" for a page that chooses between a side panel and a
// bottom sheet. CSS hides the panel on narrow screens; this is how the Razor side learns the same
// fact, so it opens the sheet instead of updating a panel nobody can see.
window.scViewport = {
    matches: function (query) {
        return !!(window.matchMedia && window.matchMedia(query).matches);
    }
};
