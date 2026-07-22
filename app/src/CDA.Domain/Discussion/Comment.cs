namespace CDA.Domain.Discussion;

/// <summary>
/// A remark in a discussion, as opposed to a proposal.
/// </summary>
/// <remarks>
/// <para>
/// Keeping the two apart is the platform's founding complaint about forums: proposals get
/// buried inside the conversation around them. A comment never becomes part of a solution;
/// it argues about one.
/// </para>
/// <para>
/// Like votes, all comments share one table with a nullable foreign key per target and a
/// check constraint requiring exactly one — topics now, proposals, groups and similarity
/// reports later. Comments are flat: the documents describe a discussion that is searched and
/// filtered rather than one that is navigated as a tree.
/// </para>
/// </remarks>
public sealed class Comment
{
    public const int BodyMaxLength = 8000;

    private Comment()
    {
        // EF Core.
        Body = null!;
    }

    private Comment(Guid authorId, string body, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        Id = Guid.NewGuid();
        AuthorId = authorId;
        Body = body.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public static Comment OnTopic(Guid topicId, Guid authorId, string body, DateTime createdAtUtc) =>
        new(authorId, body, createdAtUtc) { TopicId = topicId };

    public Guid Id { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Body { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? EditedAtUtc { get; private set; }

    /// <summary>
    /// When the comment was withdrawn, or null while it stands.
    /// </summary>
    /// <remarks>
    /// Removed comments are marked, never deleted. A discussion that people have already
    /// replied to becomes incoherent if earlier remarks vanish from under the replies, and the
    /// history of how a topic reached its conclusion is part of what the platform is for.
    /// </remarks>
    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc is not null;

    // Exactly one target is set. See the remarks on the class.
    public Guid? TopicId { get; private set; }

    public void Edit(string body, DateTime atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        Body = body.Trim();
        EditedAtUtc = atUtc;
    }

    public void Delete(DateTime atUtc) => DeletedAtUtc ??= atUtc;
}
