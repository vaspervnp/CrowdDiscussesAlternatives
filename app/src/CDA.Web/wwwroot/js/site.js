// Dark-mode switch.
//
// The theme is applied to <html data-bs-theme> before paint by an inline script in the layout
// head; this file only keeps the header switch in step with it and remembers a change. The
// preference lives in a cookie (not localStorage) so the head script can read it synchronously on
// the next request and avoid a flash of the wrong theme.
(function () {
    "use strict";

    var COOKIE = "theme";

    function readCookie(name) {
        var match = document.cookie.match(new RegExp("(?:^|; )" + name + "=([^;]+)"));
        return match ? decodeURIComponent(match[1]) : null;
    }

    function writeCookie(name, value) {
        var oneYear = 60 * 60 * 24 * 365;
        document.cookie = name + "=" + encodeURIComponent(value) +
            ";path=/;max-age=" + oneYear + ";samesite=lax";
    }

    function currentTheme() {
        return document.documentElement.getAttribute("data-bs-theme") ||
            (readCookie(COOKIE) === "dark" ? "dark" : "light");
    }

    function apply(theme) {
        document.documentElement.setAttribute("data-bs-theme", theme);
        var toggle = document.getElementById("theme-switch");
        if (toggle) {
            toggle.checked = theme === "dark";
        }
    }

    document.addEventListener("DOMContentLoaded", function () {
        // Sync the switch to whatever the head script already applied (cookie or OS preference).
        apply(currentTheme());

        var toggle = document.getElementById("theme-switch");
        if (!toggle) {
            return;
        }

        toggle.addEventListener("change", function () {
            var theme = toggle.checked ? "dark" : "light";
            writeCookie(COOKIE, theme);
            apply(theme);
        });
    });
})();
