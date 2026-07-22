namespace CDA.Domain.Topics;

/// <summary>
/// A problem the crowd is working on: the container for requirements, proposals and the
/// alternative solutions assembled from them.
/// </summary>
public sealed class Topic
{
    public const int SubjectMaxLength = 200;
    public const int DescriptionMaxLength = 8000;

    /// <summary>Default minimum votes for a similarity report to take effect in this topic.</summary>
    public const int DefaultSimilarityThresholdDefault = 1;

    private Topic()
    {
        // EF Core.
        Subject = null!;
        Description = null!;
    }

    public Topic(
        Guid id,
        string subject,
        string description,
        Guid createdByUserId,
        DateTime createdAtUtc,
        TopicVisibility visibility,
        DateTime? closesAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        Id = id;
        Subject = subject.Trim();
        Description = description?.Trim() ?? string.Empty;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        Visibility = visibility;
        ClosesAtUtc = closesAtUtc;
        Phase = TopicPhase.Discussing;
        DefaultSimilarityThreshold = DefaultSimilarityThresholdDefault;
    }

    public Guid Id { get; private set; }

    public string Subject { get; private set; }

    public string Description { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>When the discussion is meant to have finished. Informational until reached.</summary>
    public DateTime? ClosesAtUtc { get; private set; }

    public TopicPhase Phase { get; private set; }

    public TopicVisibility Visibility { get; private set; }

    /// <summary>
    /// Suppresses the vote tallies while the topic is open.
    /// </summary>
    /// <remarks>
    /// Ranking still happens; only the numbers are withheld. Seeing that something already
    /// has forty votes changes how people vote, which is the effect this exists to avoid.
    /// </remarks>
    public bool HideVoteCountsUntilClose { get; private set; }

    /// <summary>Topic-wide default for the similarity filter; a reader may override it for themselves.</summary>
    public int DefaultSimilarityThreshold { get; private set; }

    // Maintained transactionally alongside every vote write. Ordering thousands of topics or
    // proposals by score must never mean aggregating the votes table.
    public int ScoreSum { get; private set; }

    public int VoteCount { get; private set; }

    /// <summary>
    /// Whether the topic has finished, either because it was closed or because its target
    /// date has passed.
    /// </summary>
    /// <remarks>Computed rather than stored, so no scheduled job is needed for correctness.</remarks>
    public bool IsClosedAt(DateTime utcNow) =>
        Phase == TopicPhase.Closed || (ClosesAtUtc is { } closes && closes <= utcNow);

    public void Edit(string subject, string description, DateTime? closesAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        Subject = subject.Trim();
        Description = description?.Trim() ?? string.Empty;
        ClosesAtUtc = closesAtUtc;
    }

    public void SetVisibility(TopicVisibility visibility) => Visibility = visibility;

    public void SetVoteCountsHidden(bool hidden) => HideVoteCountsUntilClose = hidden;

    public void SetSimilarityThreshold(int threshold) =>
        DefaultSimilarityThreshold = threshold < 0
            ? throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Must not be negative.")
            : threshold;

    /// <summary>
    /// Whether the requirement list may still be changed.
    /// </summary>
    /// <remarks>
    /// Only during the discussion phase. Once proposals start arriving they are written
    /// against a published list, and groups are later scored against it; moving the goalposts
    /// afterwards invalidates evaluations that people have already made without telling them.
    /// </remarks>
    public bool RequirementsAreEditable => Phase == TopicPhase.Discussing;

    /// <summary>
    /// Ends the discussion phase and opens the pool of proposals.
    /// </summary>
    /// <param name="requirementCount">How many requirements the topic has concluded on.</param>
    /// <remarks>
    /// This is the DISCUSS-to-TOPIC step from the spreadsheet the platform grew out of. A
    /// topic cannot open for proposals with an empty requirement list: the list is what
    /// alternative solutions are judged against, and scoring a group against nothing is not a
    /// meaningful act.
    /// </remarks>
    public void OpenForProposals(int requirementCount)
    {
        RejectBackwardsMove(TopicPhase.Proposing);

        if (requirementCount <= 0)
        {
            throw new InvalidOperationException(
                "Agree at least one requirement before opening the topic for proposals — " +
                "it is what the alternative solutions will be judged against.");
        }

        Phase = TopicPhase.Proposing;
    }

    public void Close()
    {
        RejectBackwardsMove(TopicPhase.Closed);
        Phase = TopicPhase.Closed;
    }

    // Phases only move forward. Reopening a closed topic would resurrect votes that were cast
    // on the understanding that the discussion had ended.
    private void RejectBackwardsMove(TopicPhase target)
    {
        if (target < Phase)
        {
            throw new InvalidOperationException(
                $"A topic cannot move backwards from {Phase} to {target}.");
        }
    }

    /// <summary>
    /// Applies a change in this topic's own importance score.
    /// </summary>
    /// <remarks>
    /// Used by the in-memory path and by tests. The database path applies the same deltas as
    /// a relative UPDATE so that concurrent voters cannot lose each other's changes; see
    /// the voting service.
    /// </remarks>
    public void ApplyVoteDelta(int scoreDelta, int countDelta)
    {
        ScoreSum += scoreDelta;
        VoteCount += countDelta;
    }
}
