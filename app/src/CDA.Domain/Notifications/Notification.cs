namespace CDA.Domain.Notifications;

/// <summary>What happened.</summary>
/// <remarks>Persisted — do not renumber.</remarks>
public enum NotificationKind
{
    /// <summary>Somebody commented on a proposal this person wrote.</summary>
    CommentOnMyProposal = 0,

    /// <summary>Somebody commented on an alternative this person assembled.</summary>
    CommentOnMyAlternative = 1,

    /// <summary>Somebody reported one of this person's proposals as a duplicate.</summary>
    SimilarityOnMyProposal = 2,

    /// <summary>A private message arrived.</summary>
    PrivateMessage = 3,
}

/// <summary>How often a person wants to hear about things by email.</summary>
public enum NotificationDelivery
{
    /// <summary>In the platform only; no email at all.</summary>
    None = 0,

    /// <summary>One email per event, as it happens.</summary>
    Immediate = 1,

    /// <summary>One email a day gathering everything that happened.</summary>
    Daily = 2,
}

/// <summary>
/// Something that happened which one particular person should know about.
/// </summary>
/// <remarks>
/// Recorded in the platform whatever the person's email preference — the list is always there
/// to look at. The preference governs only whether it is also pushed out by email, so turning
/// email off costs no information.
/// </remarks>
public sealed class Notification
{
    public const int SummaryMaxLength = 300;

    private Notification()
    {
        // EF Core.
        Summary = null!;
        Link = null!;
    }

    public Notification(
        Guid userId,
        NotificationKind kind,
        string summary,
        string link,
        DateTime createdAtUtc,
        Guid? topicId = null,
        Guid? actorId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(link);

        Id = Guid.NewGuid();
        UserId = userId;
        Kind = kind;
        Summary = summary.Trim();
        Link = link;
        CreatedAtUtc = createdAtUtc;
        TopicId = topicId;
        ActorId = actorId;
    }

    public Guid Id { get; private set; }

    /// <summary>Who is being told.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Who caused it, when that is a person.</summary>
    public Guid? ActorId { get; private set; }

    public Guid? TopicId { get; private set; }

    public NotificationKind Kind { get; private set; }

    public string Summary { get; private set; }

    /// <summary>Where to go to see it. Relative to the site root.</summary>
    public string Link { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    /// <summary>
    /// When this was included in an email, or null if it has not been sent.
    /// </summary>
    /// <remarks>
    /// This column is the outbox. A notification with no send time and an eligible preference is
    /// waiting to go out; stamping it is what marks it done. Keeping the queue in the same row
    /// as the notification means a delivery can never refer to something that was rolled back.
    /// </remarks>
    public DateTime? EmailedAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    public void MarkRead(DateTime atUtc) => ReadAtUtc ??= atUtc;

    public void MarkEmailed(DateTime atUtc) => EmailedAtUtc ??= atUtc;
}

/// <summary>One person's choice about being emailed.</summary>
public sealed class NotificationPreference
{
    private NotificationPreference()
    {
        // EF Core.
    }

    public NotificationPreference(Guid userId, NotificationDelivery delivery)
    {
        UserId = userId;
        Delivery = delivery;
    }

    public Guid UserId { get; private set; }

    /// <summary>
    /// Defaults to a daily digest rather than immediate mail.
    /// </summary>
    /// <remarks>
    /// A busy topic produces a great many events, and an inbox full of them is the fastest way
    /// to make someone stop reading any of them.
    /// </remarks>
    public NotificationDelivery Delivery { get; private set; } = NotificationDelivery.Daily;

    public void ChangeTo(NotificationDelivery delivery) => Delivery = delivery;
}
