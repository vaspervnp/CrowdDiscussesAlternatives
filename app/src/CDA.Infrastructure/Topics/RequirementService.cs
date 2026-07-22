using CDA.Application.Abstractions;
using CDA.Domain.Topics;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Topics;

public sealed record RequirementChange(bool Succeeded, string? Error = null)
{
    public static readonly RequirementChange Ok = new(true);

    public static RequirementChange Refused(string reason) => new(false, reason);
}

/// <summary>
/// Maintains a topic's requirement list — the criteria alternative solutions get judged
/// against.
/// </summary>
public sealed class RequirementService(CdaDbContext database, IClock clock)
{
    public Task<List<Requirement>> ListAsync(Guid topicId, CancellationToken cancellationToken = default) =>
        database.Requirements
            .AsNoTracking()
            .Where(r => r.TopicId == topicId)
            .OrderBy(r => r.Order)
            .ThenBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<RequirementChange> AddAsync(
        Guid topicId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == topicId, cancellationToken);

        if (topic is null)
        {
            return RequirementChange.Refused("No such topic.");
        }

        if (!topic.RequirementsAreEditable)
        {
            return Frozen();
        }

        var nextOrder = await database.Requirements
            .Where(r => r.TopicId == topicId)
            .Select(r => (int?)r.Order)
            .MaxAsync(cancellationToken) ?? 0;

        database.Requirements.Add(
            new Requirement(Guid.NewGuid(), topicId, text, nextOrder + 1, clock.UtcNow));

        await database.SaveChangesAsync(cancellationToken);

        return RequirementChange.Ok;
    }

    public async Task<RequirementChange> EditAsync(
        Guid topicId,
        Guid requirementId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var requirement = await FindInTopicAsync(topicId, requirementId, cancellationToken);

        if (requirement is null)
        {
            return RequirementChange.Refused("No such requirement.");
        }

        if (!await IsEditableAsync(requirement.TopicId, cancellationToken))
        {
            return Frozen();
        }

        requirement.Edit(text);
        await database.SaveChangesAsync(cancellationToken);

        return RequirementChange.Ok;
    }

    public async Task<RequirementChange> RemoveAsync(
        Guid topicId,
        Guid requirementId,
        CancellationToken cancellationToken = default)
    {
        var requirement = await FindInTopicAsync(topicId, requirementId, cancellationToken);

        if (requirement is null)
        {
            return RequirementChange.Ok;
        }

        if (!await IsEditableAsync(requirement.TopicId, cancellationToken))
        {
            return Frozen();
        }

        database.Requirements.Remove(requirement);
        await database.SaveChangesAsync(cancellationToken);

        return RequirementChange.Ok;
    }

    /// <summary>Moves one requirement up or down the list.</summary>
    public async Task<RequirementChange> MoveAsync(
        Guid topicId,
        Guid requirementId,
        bool up,
        CancellationToken cancellationToken = default)
    {
        var requirement = await FindInTopicAsync(topicId, requirementId, cancellationToken);

        if (requirement is null)
        {
            return RequirementChange.Refused("No such requirement.");
        }

        if (!await IsEditableAsync(requirement.TopicId, cancellationToken))
        {
            return Frozen();
        }

        var ordered = await database.Requirements
            .Where(r => r.TopicId == requirement.TopicId)
            .OrderBy(r => r.Order)
            .ThenBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var index = ordered.FindIndex(r => r.Id == requirementId);
        var target = up ? index - 1 : index + 1;

        if (index < 0 || target < 0 || target >= ordered.Count)
        {
            return RequirementChange.Ok;
        }

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);

        // The whole list is renumbered rather than the two rows swapped, so that Order stays
        // dense and gaps from earlier removals do not accumulate.
        for (var position = 0; position < ordered.Count; position++)
        {
            ordered[position].MoveTo(position + 1);
        }

        await database.SaveChangesAsync(cancellationToken);

        return RequirementChange.Ok;
    }

    /// <summary>
    /// Finds a requirement, but only within the topic the caller was authorised against.
    /// </summary>
    /// <remarks>
    /// The requirement id arrives in the route, so it is attacker-controlled. Looking it up by
    /// id alone would let the facilitator of one topic edit or delete another topic's
    /// requirements simply by pairing their own topic id with someone else's requirement id —
    /// the authorisation check upstream is against the topic, not the requirement.
    /// </remarks>
    private Task<Requirement?> FindInTopicAsync(
        Guid topicId,
        Guid requirementId,
        CancellationToken cancellationToken) =>
        database.Requirements
            .SingleOrDefaultAsync(r => r.Id == requirementId && r.TopicId == topicId, cancellationToken);

    private async Task<bool> IsEditableAsync(Guid topicId, CancellationToken cancellationToken)
    {
        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == topicId, cancellationToken);

        return topic?.RequirementsAreEditable ?? false;
    }

    private static RequirementChange Frozen() => RequirementChange.Refused(
        "The requirements were settled when this topic opened for proposals. Changing them " +
        "now would invalidate evaluations already made against them.");
}
