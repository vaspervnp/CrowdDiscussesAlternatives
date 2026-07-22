using CDA.Infrastructure.Identity;
using CDA.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CDA.Web.Controllers;

// Anonymous access is granted per action rather than on the class: at class level it would
// silently override the [Authorize] on Logout.
public sealed class AccountController(
    UserAccountService accounts,
    SignInManager<CdaUser> signIn,
    UserManager<CdaUser> users,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await accounts.RegisterAsync(model.Email, model.DisplayName, model.Password, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        var user = await users.FindByIdAsync(result.UserId.ToString());
        await signIn.SignInAsync(user!, isPersistent: false);

        logger.LogInformation("New account registered for {UserId}", result.UserId);

        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signIn.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "This account is temporarily locked. Try again later.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            // One message for both "no such account" and "wrong password": distinguishing
            // them tells an attacker which addresses are registered.
            ModelState.AddModelError(string.Empty, "Incorrect email or password.");
            return View(model);
        }

        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Keeps redirects inside this application.
    /// </summary>
    /// <remarks>
    /// A returnUrl arrives from the query string, so an attacker can put anything in it.
    /// Without this check a crafted login link would send the user to another site
    /// immediately after they authenticate — the classic open-redirect phishing setup.
    /// </remarks>
    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}
