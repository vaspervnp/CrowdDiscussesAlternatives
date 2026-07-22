using CDA.Domain.Users;

namespace CDA.Application.Users;

/// <summary>
/// Decides which parts of a profile a given viewer may see, and builds the view that
/// contains only those parts.
/// </summary>
/// <remarks>
/// This is the single place the privacy filters are applied. Doing it here rather than in
/// a Razor view or a controller is the whole point: the same profile is reachable through
/// MVC pages and the REST API, and a rule enforced in one presentation layer is not
/// enforced at all. Callers receive a <see cref="UserProfileView"/> that has already had
/// the hidden fields removed, so there is no way to render data the viewer should not have.
/// </remarks>
public static class ProfileVisibilityPolicy
{
    public static bool CanSee(UserProfile profile, ProfileField field, ProfileViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(viewer);

        // Owners see their own data; administrators need it for moderation.
        if (viewer.UserId == profile.Id || viewer.IsAdministrator)
        {
            return true;
        }

        var visibility = profile.VisibilityOf(field);

        return viewer.UserId is null
            ? visibility == ProfileVisibility.Public
            : visibility >= ProfileVisibility.Members;
    }

    public static UserProfileView Project(UserProfile profile, ProfileViewer viewer, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(viewer);

        return new UserProfileView
        {
            UserId = profile.Id,
            DisplayName = profile.DisplayName,
            CreatedAtUtc = profile.CreatedAtUtc,
            IsSelf = viewer.UserId == profile.Id,

            RealName = Visible(ProfileField.RealName) ? profile.RealName : null,
            Email = Visible(ProfileField.Email) ? profile.Email : null,
            Location = Visible(ProfileField.Location) ? profile.Location : null,
            Website = Visible(ProfileField.Website) ? profile.Website : null,
            Biography = Visible(ProfileField.Biography) ? profile.Biography : null,
            IsOnline = Visible(ProfileField.OnlineStatus) ? profile.IsOnlineAt(utcNow) : null,
        };

        bool Visible(ProfileField field) => CanSee(profile, field, viewer);
    }
}
