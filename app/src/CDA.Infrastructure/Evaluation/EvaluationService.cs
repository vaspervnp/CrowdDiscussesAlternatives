using CDA.Application.Abstractions;
using CDA.Domain.Evaluation;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Evaluation;

/// <summary>One requirement as it appears on the evaluation form.</summary>
public sealed record EvaluationRow(Guid RequirementId, string Text, int Weight, int Score);

/// <summary>A participant's private evaluation of one alternative.</summary>
public sealed record EvaluationView(
    Guid GroupId,
    string GroupDescription,
    IReadOnlyList<EvaluationRow> Rows,
    int Total,
    int? Percentage,
    bool HasBeenEvaluated,
    DateTime? UpdatedAtUtc);

/// <summary>One alternative's line in the side-by-side comparison.</summary>
public sealed record ComparisonRow(
    Guid GroupId,
    string Description,
    int Total,
    int? Percentage,
    bool HasBeenEvaluated,
    IReadOnlyDictionary<Guid, int> ScoreByRequirement);

public sealed record Comparison(
    IReadOnlyList<EvaluationRow> Requirements,
    IReadOnlyList<ComparisonRow> Alternatives);

public sealed record EvaluationResult(bool Succeeded, string? Error = null)
{
    public static readonly EvaluationResult Ok = new(true);

    public static EvaluationResult Refused(string reason) => new(false, reason);
}

/// <summary>
/// The weighing-up a participant does before voting on an alternative.
/// </summary>
/// <remarks>
/// Everything here is private to the person who recorded it. The vote is the public act; the
/// working behind it is not, and publishing it would turn a private weighing-up into one more
/// thing to be judged on.
/// </remarks>
public sealed class EvaluationService(CdaDbContext database, IClock clock)
{
    public async Task<EvaluationView?> GetAsync(
        Guid topicId,
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var group = await database.ProposalGroups
            .AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == groupId && g.TopicId == topicId, cancellationToken);

        if (group is null)
        {
            return null;
        }

        var requirements = await database.Requirements
            .AsNoTracking()
            .Where(r => r.TopicId == topicId)
            .OrderBy(r => r.Order)
            .ToListAsync(cancellationToken);

        var weights = await database.RequirementWeights
            .AsNoTracking()
            .Where(w => w.UserId == userId && w.TopicId == topicId)
            .ToDictionaryAsync(w => w.RequirementId, w => w.Weight, cancellationToken);

        var scores = await database.RequirementScores
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.GroupId == groupId)
            .ToListAsync(cancellationToken);

        var scoreByRequirement = scores.ToDictionary(s => s.RequirementId, s => s.Score);

        var rows = requirements
            .Select(r => new EvaluationRow(
                r.Id,
                r.Text,
                // A requirement nobody has weighted yet starts in the middle rather than at
                // zero: an unconsidered criterion is not the same as one judged irrelevant.
                weights.TryGetValue(r.Id, out var weight) ? weight : 3,
                scoreByRequirement.GetValueOrDefault(r.Id)))
            .ToList();

        var pairs = rows.Select(r => (r.Weight, r.Score)).ToList();

        return new EvaluationView(
            groupId,
            group.Description,
            rows,
            EvaluationMath.Total(pairs),
            EvaluationMath.Percentage(pairs),
            scores.Count > 0,
            scores.Count > 0 ? scores.Max(s => s.UpdatedAtUtc) : null);
    }

    /// <summary>
    /// Records or replaces an evaluation.
    /// </summary>
    /// <remarks>
    /// Weights are saved against the topic, not the alternative, so changing one here changes
    /// it everywhere in this topic. That is the point — see <see cref="RequirementWeight"/> —
    /// but it means the interface has to say so.
    /// </remarks>
    public async Task<EvaluationResult> SaveAsync(
        Guid topicId,
        Guid groupId,
        Guid userId,
        IReadOnlyDictionary<Guid, int> weights,
        IReadOnlyDictionary<Guid, int> scores,
        CancellationToken cancellationToken = default)
    {
        var group = await database.ProposalGroups
            .AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == groupId && g.TopicId == topicId, cancellationToken);

        if (group is null)
        {
            return EvaluationResult.Refused("No such alternative.");
        }

        var requirementIds = await database.Requirements
            .AsNoTracking()
            .Where(r => r.TopicId == topicId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (requirementIds.Count == 0)
        {
            return EvaluationResult.Refused("This topic has no requirements to evaluate against.");
        }

        // Ids arrive from a form; anything not belonging to this topic is discarded rather than
        // trusted.
        var known = requirementIds.ToHashSet();
        var now = clock.UtcNow;

        var existingWeights = await database.RequirementWeights
            .Where(w => w.UserId == userId && w.TopicId == topicId)
            .ToDictionaryAsync(w => w.RequirementId, cancellationToken);

        var existingScores = await database.RequirementScores
            .Where(s => s.UserId == userId && s.GroupId == groupId)
            .ToDictionaryAsync(s => s.RequirementId, cancellationToken);

        try
        {
            foreach (var (requirementId, weight) in weights.Where(w => known.Contains(w.Key)))
            {
                if (existingWeights.TryGetValue(requirementId, out var stored))
                {
                    stored.ChangeTo(weight, now);
                }
                else
                {
                    database.RequirementWeights.Add(
                        new RequirementWeight(userId, topicId, requirementId, weight, now));
                }
            }

            foreach (var (requirementId, score) in scores.Where(s => known.Contains(s.Key)))
            {
                if (existingScores.TryGetValue(requirementId, out var stored))
                {
                    stored.ChangeTo(score, now);
                }
                else
                {
                    database.RequirementScores.Add(
                        new RequirementScore(userId, groupId, topicId, requirementId, score, now));
                }
            }
        }
        catch (ArgumentOutOfRangeException error)
        {
            return EvaluationResult.Refused(error.Message);
        }

        await database.SaveChangesAsync(cancellationToken);

        return EvaluationResult.Ok;
    }

    /// <summary>
    /// Every alternative in the topic, side by side, under this participant's weights.
    /// </summary>
    /// <remarks>
    /// The comparison is only meaningful because the weights are shared across the topic: each
    /// column is scored on the same scale, so the totals can be read against each other.
    /// </remarks>
    public async Task<Comparison> CompareAsync(
        Guid topicId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var requirements = await database.Requirements
            .AsNoTracking()
            .Where(r => r.TopicId == topicId)
            .OrderBy(r => r.Order)
            .ToListAsync(cancellationToken);

        var weights = await database.RequirementWeights
            .AsNoTracking()
            .Where(w => w.UserId == userId && w.TopicId == topicId)
            .ToDictionaryAsync(w => w.RequirementId, w => w.Weight, cancellationToken);

        var requirementRows = requirements
            .Select(r => new EvaluationRow(
                r.Id, r.Text, weights.TryGetValue(r.Id, out var weight) ? weight : 3, 0))
            .ToList();

        var groups = await database.ProposalGroups
            .AsNoTracking()
            .Where(g => g.TopicId == topicId)
            .Select(g => new { g.Id, g.Description })
            .ToListAsync(cancellationToken);

        var allScores = await database.RequirementScores
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.TopicId == topicId)
            .ToListAsync(cancellationToken);

        var alternatives = groups
            .Select(group =>
            {
                var scores = allScores
                    .Where(s => s.GroupId == group.Id)
                    .ToDictionary(s => s.RequirementId, s => s.Score);

                var pairs = requirementRows
                    .Select(r => (r.Weight, Score: scores.GetValueOrDefault(r.RequirementId)))
                    .ToList();

                return new ComparisonRow(
                    group.Id,
                    group.Description,
                    EvaluationMath.Total(pairs),
                    EvaluationMath.Percentage(pairs),
                    scores.Count > 0,
                    scores);
            })
            // Evaluated ones first, best first; the unevaluated have nothing to say yet.
            .OrderByDescending(row => row.HasBeenEvaluated)
            .ThenByDescending(row => row.Total)
            .ToList();

        return new Comparison(requirementRows, alternatives);
    }
}
