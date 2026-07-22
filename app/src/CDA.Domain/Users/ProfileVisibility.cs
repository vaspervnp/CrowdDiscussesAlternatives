namespace CDA.Domain.Users;

/// <summary>
/// Who may see a single profile field.
/// </summary>
/// <remarks>
/// Ordered from least to most exposed so that comparisons read naturally
/// (<c>visibility >= ProfileVisibility.Members</c>). Do not renumber: the values are
/// persisted.
/// </remarks>
public enum ProfileVisibility
{
    /// <summary>Only the owner (and administrators).</summary>
    Private = 0,

    /// <summary>Any signed-in user of the platform.</summary>
    Members = 1,

    /// <summary>Anyone, including anonymous visitors.</summary>
    Public = 2,
}

/// <summary>
/// The profile fields whose exposure the owner controls.
/// </summary>
/// <remarks>
/// <see cref="UserProfile.DisplayName"/> is deliberately absent: it appears on every
/// proposal, comment and vote its owner makes, so it cannot be hidden. Offering a switch
/// that the rest of the system would ignore would be worse than offering none.
/// </remarks>
public enum ProfileField
{
    RealName = 0,
    Email = 1,
    Location = 2,
    Website = 3,
    Biography = 4,

    /// <summary>Whether others can see that this user is currently online.</summary>
    OnlineStatus = 5,
}
