using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Localization;
using CDA.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDA.Web.Controllers;

/// <summary>
/// Lets an administrator correct the interface's translations without a redeployment.
/// </summary>
/// <remarks>
/// This is the "text will be in a table and translatable" of the specification made real: the
/// strings ship as a first guess and are refined here by someone who actually speaks the
/// language. English is not listed for editing — it is the source the keys are written in.
/// </remarks>
[Route("admin/translations")]
[Authorize(Roles = CdaRole.Administrator)]
public sealed class LocalizationController(LocalizationService localization) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(bool missingOnly = false)
    {
        var rows = await localization.RowsAsync(
            LocalizationService.GreekCulture, missingOnly, HttpContext.RequestAborted);

        // The counts describe the whole set even when the list is filtered, so progress is
        // visible at a glance.
        var all = missingOnly
            ? await localization.RowsAsync(LocalizationService.GreekCulture, false, HttpContext.RequestAborted)
            : rows;

        return View(new TranslationsViewModel
        {
            Rows = rows,
            Culture = LocalizationService.GreekCulture,
            CultureName = "Greek",
            MissingOnly = missingOnly,
            TotalCount = all.Count,
            MissingCount = all.Count(row => !row.IsTranslated),
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(string key, string? value, bool missingOnly = false)
    {
        await localization.SetAsync(
            key, LocalizationService.GreekCulture, value ?? string.Empty, HttpContext.RequestAborted);

        TempData["Saved"] = key;

        return RedirectToAction(nameof(Index), new { missingOnly });
    }
}
