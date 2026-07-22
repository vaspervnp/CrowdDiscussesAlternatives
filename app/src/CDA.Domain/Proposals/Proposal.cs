namespace CDA.Domain.Proposals;

/// <summary>
/// A single building block of a solution — roughly one sentence.
/// </summary>
/// <remarks>
/// <para>
/// Proposals are deliberately small. A whole solution written by one person leaves everyone
/// else with nothing to do but agree or disagree with the entire thing; broken into
/// sentence-sized pieces, the parts people accept can be reused in an alternative that fixes
/// the parts they do not.
/// </para>
/// <para>
/// Each proposal has an editing window, during which its author may improve it in response to
/// comments. **Voting is refused while that window is open, and commenting is encouraged** —
/// otherwise votes would attach to wording that later changes underneath them.
/// </para>
/// </remarks>
public sealed class Proposal
{
    public const int TextMaxLength = 500;

    /// <summary>How long a new proposal stays editable unless its author chooses otherwise.</summary>
    public static readonly TimeSpan DefaultEditingWindow = TimeSpan.FromDays(3);

    /// <summary>
    /// The longest editing window anyone may choose.
    /// </summary>
    /// <remarks>
    /// A proposal that never locks can never be voted on, so without a ceiling an author could
    /// park a proposal in the pool indefinitely and keep it beyond judgement.
    /// </remarks>
    public static readonly TimeSpan MaximumEditingWindow = TimeSpan.FromDays(30);

    private Proposal()
    {
        // EF Core.
        Text = null!;
    }

    public Proposal(
        Guid id,
        Guid topicId,
        Guid authorId,
        string text,
        DateTime createdAtUtc,
        DateTime? editableUntilUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var deadline = editableUntilUtc ?? createdAtUtc + DefaultEditingWindow;

        if (deadline > createdAtUtc + MaximumEditingWindow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(editableUntilUtc),
                deadline,
                $"A proposal may stay editable for at most {MaximumEditingWindow.TotalDays:0} days. " +
                "Until it locks it cannot be voted on.");
        }

        Id = id;
        TopicId = topicId;
        AuthorId = authorId;
        Text = text.Trim();
        CreatedAtUtc = createdAtUtc;
        EditableUntilUtc = deadline < createdAtUtc ? createdAtUtc : deadline;
    }

    public Guid Id { get; private set; }

    public Guid TopicId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Text { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? EditedAtUtc { get; private set; }

    /// <summary>When the editing window closes and voting opens.</summary>
    public DateTime EditableUntilUtc { get; private set; }

    /// <summary>Set when the author locks early, before the window would have expired.</summary>
    public bool ManuallyLocked { get; private set; }

    // Maintained transactionally alongside every vote and comment; see the voting service.
    public int ScoreSum { get; private set; }

    public int VoteCount { get; private set; }

    public int CommentCount { get; private set; }

    /// <summary>Drives the "show me what was discussed most recently" ordering.</summary>
    public DateTime? LastCommentAtUtc { get; private set; }

    /// <summary>
    /// Whether the wording is final and voting is open.
    /// </summary>
    /// <remarks>Computed rather than stored, so no scheduled job is needed for correctness.</remarks>
    public bool IsLockedAt(DateTime utcNow) => ManuallyLocked || EditableUntilUtc <= utcNow;

    public void Edit(string text, DateTime atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (IsLockedAt(atUtc))
        {
            throw new InvalidOperationException(
                "This proposal has locked. Its wording is what people voted on, so it can no " +
                "longer be changed — add a new proposal instead.");
        }

        Text = text.Trim();
        EditedAtUtc = atUtc;
    }

    /// <summary>
    /// Shortens the editing window, or ends it now.
    /// </summary>
    /// <remarks>
    /// The window can only ever be brought forward. Extending it would let an author who
    /// dislikes the way opinion is forming keep their proposal out of reach of a vote.
    /// </remarks>
    public void BringLockForward(DateTime newDeadlineUtc, DateTime atUtc)
    {
        if (IsLockedAt(atUtc))
        {
            throw new InvalidOperationException("This proposal has already locked.");
        }

        if (newDeadlineUtc >= EditableUntilUtc)
        {
            throw new InvalidOperationException(
                "The editing window can be shortened but not extended.");
        }

        EditableUntilUtc = newDeadlineUtc;
    }

    public void LockNow(DateTime atUtc)
    {
        ManuallyLocked = true;
        EditableUntilUtc = EditableUntilUtc > atUtc ? atUtc : EditableUntilUtc;
    }

    public void ApplyVoteDelta(int scoreDelta, int countDelta)
    {
        ScoreSum += scoreDelta;
        VoteCount += countDelta;
    }

    public void RecordComment(DateTime atUtc)
    {
        CommentCount++;
        LastCommentAtUtc = atUtc;
    }
}
