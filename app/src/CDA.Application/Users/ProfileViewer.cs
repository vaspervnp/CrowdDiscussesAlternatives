namespace CDA.Application.Users;

/// <summary>
/// Who is asking to see a profile.
/// </summary>
/// <param name="UserId">The viewer's own id, or null when nobody is signed in.</param>
/// <param name="IsAdministrator">Whether the viewer holds the platform administrator role.</param>
public sealed record ProfileViewer(Guid? UserId, bool IsAdministrator = false)
{
    // The cast is required: without it `new(null)` binds to the record's copy constructor.
    public static readonly ProfileViewer Anonymous = new((Guid?)null);
}
