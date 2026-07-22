using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CDA.Web.Controllers;

/// <summary>Remembers the language a visitor picked.</summary>
[Route("culture")]
public sealed class CultureController : Controller
{
    /// <summary>
    /// Writes the chosen culture to a cookie and returns the visitor to where they were.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A POST, not a link, so that a page on another site cannot silently switch someone's
    /// language by embedding a request. The return address is checked with
    /// <see cref="Url.IsLocalUrl"/> — a value carried in from the page must never become an
    /// open redirect to somewhere else.
    /// </para>
    /// <para>
    /// The cookie is the framework's own culture cookie, which the localization middleware reads
    /// on the next request; nothing about the choice needs an account, so it works signed out.
    /// </para>
    /// </remarks>
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public IActionResult Set(string culture, string? returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true, // a language choice is not something to gate behind consent
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
            });

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
