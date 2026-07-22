namespace CDA.Domain.Users;

/// <summary>
/// Everything about a participant that the discussion side of the platform cares about.
/// </summary>
/// <remarks>
/// Deliberately separate from the authentication record. Credentials, password hashes and
/// security stamps live in the Identity tables and the domain never sees them; this holds
/// only what other participants might read. <see cref="Id"/> is the same value as the
/// Identity user id, so the two are always one-to-one.
/// </remarks>
public sealed class UserProfile
{
    /// <summary>How long after their last request a user still counts as online.</summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);

    public const int DisplayNameMaxLength = 40;
    public const int RealNameMaxLength = 100;
    public const int LocationMaxLength = 100;
    public const int WebsiteMaxLength = 400;
    public const int BiographyMaxLength = 2000;

    private UserProfile()
    {
        // EF Core.
        DisplayName = null!;
    }

    public UserProfile(Guid id, string displayName, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = id;
        DisplayName = displayName.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Same value as the Identity user id.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The name shown wherever this user appears. Unique across the platform, because a
    /// duplicate would let one participant be mistaken for another in a discussion.
    /// </summary>
    public string DisplayName { get; private set; }

    public string? RealName { get; private set; }
    public string? Email { get; private set; }
    public string? Location { get; private set; }
    public string? Website { get; private set; }
    public string? Biography { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Last request from this user, in UTC. Null until their first authenticated request.
    /// </summary>
    public DateTime? LastSeenAtUtc { get; private set; }

    // Least exposure by default: a new account reveals nothing beyond its display name
    // until its owner decides otherwise.
    public ProfileVisibility RealNameVisibility { get; private set; } = ProfileVisibility.Private;
    public ProfileVisibility EmailVisibility { get; private set; } = ProfileVisibility.Private;
    public ProfileVisibility LocationVisibility { get; private set; } = ProfileVisibility.Private;
    public ProfileVisibility WebsiteVisibility { get; private set; } = ProfileVisibility.Private;
    public ProfileVisibility BiographyVisibility { get; private set; } = ProfileVisibility.Private;
    public ProfileVisibility OnlineStatusVisibility { get; private set; } = ProfileVisibility.Private;

    public ProfileVisibility VisibilityOf(ProfileField field) => field switch
    {
        ProfileField.RealName => RealNameVisibility,
        ProfileField.Email => EmailVisibility,
        ProfileField.Location => LocationVisibility,
        ProfileField.Website => WebsiteVisibility,
        ProfileField.Biography => BiographyVisibility,
        ProfileField.OnlineStatus => OnlineStatusVisibility,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown profile field."),
    };

    public void SetVisibility(ProfileField field, ProfileVisibility visibility)
    {
        if (!Enum.IsDefined(visibility))
        {
            throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unknown visibility.");
        }

        switch (field)
        {
            case ProfileField.RealName: RealNameVisibility = visibility; break;
            case ProfileField.Email: EmailVisibility = visibility; break;
            case ProfileField.Location: LocationVisibility = visibility; break;
            case ProfileField.Website: WebsiteVisibility = visibility; break;
            case ProfileField.Biography: BiographyVisibility = visibility; break;
            case ProfileField.OnlineStatus: OnlineStatusVisibility = visibility; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown profile field.");
        }
    }

    public void Rename(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
    }

    public void EditDetails(string? realName, string? email, string? location, string? website, string? biography)
    {
        RealName = Blank(realName);
        Email = Blank(email);
        Location = Blank(location);
        Website = Blank(website);
        Biography = Blank(biography);
    }

    /// <summary>Records that the user was active. Ignores clock movement backwards.</summary>
    public void Seen(DateTime atUtc)
    {
        if (LastSeenAtUtc is null || atUtc > LastSeenAtUtc)
        {
            LastSeenAtUtc = atUtc;
        }
    }

    public bool IsOnlineAt(DateTime utcNow) =>
        LastSeenAtUtc is { } seen && utcNow - seen < OnlineWindow;

    /// <summary>Empty input is stored as null, so "unset" has one representation rather than two.</summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
