namespace CDA.Domain.Topics;

/// <summary>
/// Where a topic is in its lifecycle.
/// </summary>
/// <remarks>
/// Mirrors the Excel version's flow: the facilitator opens a discussion to agree what a
/// solution must achieve, then the pool of proposals opens, then the topic closes.
/// Persisted — do not renumber.
/// </remarks>
public enum TopicPhase
{
    /// <summary>Agreeing the subject and the requirements a solution must satisfy.</summary>
    Discussing = 0,

    /// <summary>Requirements are settled; proposals and groups are being built.</summary>
    Proposing = 1,

    /// <summary>Finished. Read-only.</summary>
    Closed = 2,
}

/// <summary>Who may read a topic. Writing always requires membership.</summary>
public enum TopicVisibility
{
    /// <summary>Readable by anyone, joinable by any signed-in user.</summary>
    Public = 0,

    /// <summary>
    /// Readable only by its members — the equivalent of the Excel version's "the
    /// facilitator shares the workbook".
    /// </summary>
    InviteOnly = 1,
}

/// <summary>
/// A participant's standing in one topic.
/// </summary>
/// <remarks>
/// Deliberately not an Identity role. Facilitating is per topic: the same person may run one
/// discussion and merely take part in another.
/// </remarks>
public enum TopicRole
{
    Member = 0,

    /// <summary>Sets the requirements, moves the phase, closes the topic.</summary>
    Facilitator = 1,
}
