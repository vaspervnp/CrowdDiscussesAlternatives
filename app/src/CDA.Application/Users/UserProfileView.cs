namespace CDA.Application.Users;

/// <summary>
/// A profile as one particular viewer is allowed to see it.
/// </summary>
/// <remarks>
/// A null property means "you may not see this", which is deliberately indistinguishable
/// from "the owner never filled it in". Reporting the difference would leak the existence
/// of hidden data, and there is nothing a viewer could do with that knowledge except infer.
/// </remarks>
public sealed record UserProfileView
{
    public required Guid UserId { get; init; }

    /// <summary>Always present — see <see cref="Domain.Users.ProfileField"/>.</summary>
    public required string DisplayName { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public string? RealName { get; init; }
    public string? Email { get; init; }
    public string? Location { get; init; }
    public string? Website { get; init; }
    public string? Biography { get; init; }

    /// <summary>Null when the owner does not expose their presence to this viewer.</summary>
    public bool? IsOnline { get; init; }

    /// <summary>True when the viewer is looking at their own profile.</summary>
    public required bool IsSelf { get; init; }
}
