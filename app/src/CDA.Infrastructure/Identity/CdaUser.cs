using Microsoft.AspNetCore.Identity;

namespace CDA.Infrastructure.Identity;

/// <summary>
/// The authentication record: credentials and nothing else.
/// </summary>
/// <remarks>
/// Profile data lives in <see cref="Domain.Users.UserProfile"/> instead, keyed by the same
/// id. Keeping them apart means the discussion side of the system can load and project a
/// participant without ever touching a password hash or a security stamp.
/// </remarks>
public sealed class CdaUser : IdentityUser<Guid>
{
    public CdaUser()
    {
    }

    public CdaUser(string userName) : base(userName)
    {
    }
}

/// <summary>
/// Platform-wide roles. Note that being a topic's facilitator is <em>not</em> one of these:
/// that is per topic and will be modelled as topic membership in Phase 2.
/// </summary>
public sealed class CdaRole : IdentityRole<Guid>
{
    /// <summary>Moderation and support. Can see every profile field.</summary>
    public const string Administrator = nameof(Administrator);

    public CdaRole()
    {
    }

    public CdaRole(string roleName) : base(roleName)
    {
    }
}
