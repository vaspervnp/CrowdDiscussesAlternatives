namespace CDA.Domain.Messaging;

/// <summary>
/// A message from one participant to another, outside any topic.
/// </summary>
/// <remarks>
/// Deliberately thin. Discussion belongs in topics where everyone can see it and it counts
/// towards a conclusion; this is for the things that genuinely are between two people —
/// arranging to work on something together, or asking a question without derailing a thread.
/// </remarks>
public sealed class PrivateMessage
{
    public const int BodyMaxLength = 4000;

    private PrivateMessage()
    {
        // EF Core.
        Body = null!;
    }

    public PrivateMessage(Guid fromUserId, Guid toUserId, string body, DateTime sentAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (fromUserId == toUserId)
        {
            throw new ArgumentException("A message needs someone else to go to.", nameof(toUserId));
        }

        Id = Guid.NewGuid();
        FromUserId = fromUserId;
        ToUserId = toUserId;
        Body = body.Trim();
        SentAtUtc = sentAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid FromUserId { get; private set; }

    public Guid ToUserId { get; private set; }

    public string Body { get; private set; }

    public DateTime SentAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    /// <summary>
    /// Marks the message read.
    /// </summary>
    /// <remarks>
    /// Only meaningful for the recipient; the sender opening their own sent message does not
    /// mean it has been read, which is why the caller has to establish who is looking.
    /// </remarks>
    public void MarkRead(DateTime atUtc) => ReadAtUtc ??= atUtc;
}
