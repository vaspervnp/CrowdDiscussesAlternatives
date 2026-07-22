namespace CDA.Domain.Evaluation;

/// <summary>
/// How much one participant cares about one requirement.
/// </summary>
/// <remarks>
/// <para>
/// Held per person and per requirement — deliberately <em>not</em> per alternative. A weight
/// answers "how much does this criterion matter to me", which is a fact about the criterion and
/// the person, not about any particular answer.
/// </para>
/// <para>
/// The distinction is what makes comparison mean anything. If someone could weight the
/// requirements differently for each alternative they scored, the resulting totals would be
/// computed on different scales and putting them side by side would be meaningless — the tool
/// would let you reach any conclusion you liked by adjusting the weights to suit. Sharing the
/// weights across a topic makes the comparison sound by construction rather than by discipline.
/// </para>
/// </remarks>
public sealed class RequirementWeight
{
    public const int Minimum = 0;
    public const int Maximum = 5;

    private RequirementWeight()
    {
        // EF Core.
    }

    public RequirementWeight(Guid userId, Guid topicId, Guid requirementId, int weight, DateTime atUtc)
    {
        UserId = userId;
        TopicId = topicId;
        RequirementId = requirementId;
        Weight = Validated(weight);
        UpdatedAtUtc = atUtc;
    }

    public Guid UserId { get; private set; }

    public Guid TopicId { get; private set; }

    public Guid RequirementId { get; private set; }

    /// <summary>0 (this requirement does not matter to me) to 5 (it is decisive).</summary>
    public int Weight { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void ChangeTo(int weight, DateTime atUtc)
    {
        Weight = Validated(weight);
        UpdatedAtUtc = atUtc;
    }

    private static int Validated(int weight) => weight is >= Minimum and <= Maximum
        ? weight
        : throw new ArgumentOutOfRangeException(
            nameof(weight), weight, $"A weight must be between {Minimum} and {Maximum}.");
}

/// <summary>
/// One participant's judgement of how well one alternative satisfies one requirement.
/// </summary>
/// <remarks>
/// Private to whoever recorded it. This is a thinking tool used before voting — the vote is the
/// public act, and publishing the working behind it would turn a private weighing-up into
/// another thing to be judged on.
/// </remarks>
public sealed class RequirementScore
{
    public const int Minimum = 0;
    public const int Maximum = 5;

    private RequirementScore()
    {
        // EF Core.
    }

    public RequirementScore(
        Guid userId,
        Guid groupId,
        Guid topicId,
        Guid requirementId,
        int score,
        DateTime atUtc)
    {
        UserId = userId;
        GroupId = groupId;
        TopicId = topicId;
        RequirementId = requirementId;
        Score = Validated(score);
        UpdatedAtUtc = atUtc;
    }

    public Guid UserId { get; private set; }

    public Guid GroupId { get; private set; }

    public Guid TopicId { get; private set; }

    public Guid RequirementId { get; private set; }

    /// <summary>0 (does not satisfy this at all) to 5 (satisfies it fully).</summary>
    public int Score { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void ChangeTo(int score, DateTime atUtc)
    {
        Score = Validated(score);
        UpdatedAtUtc = atUtc;
    }

    private static int Validated(int score) => score is >= Minimum and <= Maximum
        ? score
        : throw new ArgumentOutOfRangeException(
            nameof(score), score, $"A score must be between {Minimum} and {Maximum}.");
}

/// <summary>
/// The arithmetic of a weighted evaluation.
/// </summary>
public static class EvaluationMath
{
    /// <summary>Weight times score, summed over the requirements.</summary>
    public static int Total(IEnumerable<(int Weight, int Score)> rows) =>
        rows.Sum(row => row.Weight * row.Score);

    /// <summary>
    /// The same total as a percentage of what a perfect answer would score.
    /// </summary>
    /// <remarks>
    /// Raw totals are hard to read: whether 34 is good depends on how many requirements there
    /// are and how heavily they are weighted. The percentage divides by the best achievable
    /// under the same weights, which is what makes two alternatives directly comparable.
    /// Returns null when every weight is zero — nothing matters, so nothing can be scored.
    /// </remarks>
    public static int? Percentage(IEnumerable<(int Weight, int Score)> rows)
    {
        var materialised = rows.ToList();
        var best = materialised.Sum(row => row.Weight * RequirementScore.Maximum);

        return best == 0 ? null : (int)Math.Round(100.0 * Total(materialised) / best);
    }
}
