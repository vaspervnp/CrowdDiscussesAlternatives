using System.Security.Claims;
using CDA.Application.Users;
using CDA.Infrastructure.Identity;

namespace CDA.Web.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// Describes the caller for the profile privacy rules.
    /// </summary>
    /// <remarks>
    /// Every read of a profile goes through this, so that an anonymous request and a signed-in
    /// one cannot accidentally take different paths through the visibility checks.
    /// </remarks>
    public static ProfileViewer AsProfileViewer(this ClaimsPrincipal principal) =>
        principal.GetUserId() is { } id
            ? new ProfileViewer(id, principal.IsInRole(CdaRole.Administrator))
            : ProfileViewer.Anonymous;
}
