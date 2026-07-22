using CDA.Application.Abstractions;
using CDA.Application.Users;
using CDA.Domain.Users;
using CDA.Infrastructure.Persistence;
using CDA.Web.Models;
using CDA.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CDA.Web.Controllers;

[Route("profiles")]
public sealed class ProfileController(CdaDbContext database, IClock clock) : Controller
{
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(Guid id)
    {
        var profile = await database.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == id, HttpContext.RequestAborted);

        if (profile is null)
        {
            return NotFound();
        }

        // Projected before it reaches the view, so a template cannot render a hidden field
        // even by mistake.
        return View(ProfileVisibilityPolicy.Project(profile, User.AsProfileViewer(), clock.UtcNow));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Edit()
    {
        var profile = await CurrentProfileAsync();
        if (profile is null)
        {
            return NotFound();
        }

        return View(new EditProfileViewModel
        {
            RealName = profile.RealName,
            Email = profile.Email,
            Location = profile.Location,
            Website = profile.Website,
            Biography = profile.Biography,
            RealNameVisibility = profile.RealNameVisibility,
            EmailVisibility = profile.EmailVisibility,
            LocationVisibility = profile.LocationVisibility,
            WebsiteVisibility = profile.WebsiteVisibility,
            BiographyVisibility = profile.BiographyVisibility,
            OnlineStatusVisibility = profile.OnlineStatusVisibility,
        });
    }

    [HttpPost("me")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Loaded by the signed-in user's own id — the form carries no identifier, so there
        // is nothing for a caller to tamper with in order to edit someone else's profile.
        var profile = await CurrentProfileAsync();
        if (profile is null)
        {
            return NotFound();
        }

        profile.EditDetails(model.RealName, model.Email, model.Location, model.Website, model.Biography);

        profile.SetVisibility(ProfileField.RealName, model.RealNameVisibility);
        profile.SetVisibility(ProfileField.Email, model.EmailVisibility);
        profile.SetVisibility(ProfileField.Location, model.LocationVisibility);
        profile.SetVisibility(ProfileField.Website, model.WebsiteVisibility);
        profile.SetVisibility(ProfileField.Biography, model.BiographyVisibility);
        profile.SetVisibility(ProfileField.OnlineStatus, model.OnlineStatusVisibility);

        await database.SaveChangesAsync(HttpContext.RequestAborted);

        TempData["Saved"] = true;
        return RedirectToAction(nameof(Edit));
    }

    private async Task<UserProfile?> CurrentProfileAsync()
    {
        if (User.GetUserId() is not { } userId)
        {
            return null;
        }

        return await database.UserProfiles
            .SingleOrDefaultAsync(p => p.Id == userId, HttpContext.RequestAborted);
    }
}
