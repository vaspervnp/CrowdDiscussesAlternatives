using CDA.Application.Abstractions;
using CDA.Domain.Parameters;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Parameters;

/// <summary>One cell of the grid, as displayed.</summary>
public sealed record InfluenceCell(InfluenceEffect Effect, string? Note);

public sealed record ParameterTableView(
    Guid Id,
    string Name,
    Guid OwnerId,
    string OwnerDisplayName,
    bool IsShared,
    bool IsMine,
    DateTime UpdatedAtUtc,
    IReadOnlyList<Parameter> Parameters,
    IReadOnlyDictionary<(Guid From, Guid To), InfluenceCell> Influences);

public sealed record ParameterTableSummary(
    Guid Id, string Name, string OwnerDisplayName, bool IsShared, bool IsMine, int ParameterCount);

public sealed record ParameterResult(bool Succeeded, Guid Id = default, string? Error = null)
{
    public static ParameterResult Ok(Guid id = default) => new(true, id);

    public static ParameterResult Refused(string reason) => new(false, Error: reason);
}

public sealed class ParameterTableService(CdaDbContext database, IClock clock)
{
    /// <summary>
    /// The tables a participant may read in a topic: their own, plus everyone's shared ones.
    /// </summary>
    public async Task<List<ParameterTableSummary>> ListAsync(
        Guid topicId,
        Guid? viewerId,
        CancellationToken cancellationToken = default)
    {
        var rows = await database.ParameterTables
            .AsNoTracking()
            .Where(t => t.TopicId == topicId && (t.IsShared || t.OwnerId == viewerId))
            .Join(database.UserProfiles.AsNoTracking(),
                t => t.OwnerId, profile => profile.Id,
                (t, profile) => new { Table = t, profile.DisplayName })
            .OrderByDescending(x => x.Table.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Table.Id).ToList();

        var counts = await database.Parameters
            .AsNoTracking()
            .Where(p => ids.Contains(p.TableId))
            .GroupBy(p => p.TableId)
            .Select(g => new { TableId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TableId, x => x.Count, cancellationToken);

        return
        [
            .. rows.Select(r => new ParameterTableSummary(
                r.Table.Id,
                r.Table.Name,
                r.DisplayName,
                r.Table.IsShared,
                r.Table.OwnerId == viewerId,
                counts.GetValueOrDefault(r.Table.Id)))
        ];
    }

    /// <summary>
    /// Reads one table, if the caller is allowed to.
    /// </summary>
    /// <remarks>
    /// An unshared table belongs to its author alone. It is a working sketch of how they think
    /// the problem hangs together, and half-formed thinking should not be on show before its
    /// author decides it is worth showing.
    /// </remarks>
    public async Task<ParameterTableView?> GetAsync(
        Guid topicId,
        Guid tableId,
        Guid? viewerId,
        CancellationToken cancellationToken = default)
    {
        var row = await database.ParameterTables
            .AsNoTracking()
            .Where(t => t.Id == tableId && t.TopicId == topicId)
            .Join(database.UserProfiles.AsNoTracking(),
                t => t.OwnerId, profile => profile.Id,
                (t, profile) => new { Table = t, profile.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null || (!row.Table.IsShared && row.Table.OwnerId != viewerId))
        {
            return null;
        }

        var parameters = await database.Parameters
            .AsNoTracking()
            .Where(p => p.TableId == tableId)
            .OrderBy(p => p.Order)
            .ToListAsync(cancellationToken);

        var influences = await database.ParameterInfluences
            .AsNoTracking()
            .Where(i => i.TableId == tableId)
            .ToListAsync(cancellationToken);

        return new ParameterTableView(
            row.Table.Id,
            row.Table.Name,
            row.Table.OwnerId,
            row.DisplayName,
            row.Table.IsShared,
            row.Table.OwnerId == viewerId,
            row.Table.UpdatedAtUtc,
            parameters,
            influences.ToDictionary(
                i => (i.FromParameterId, i.ToParameterId),
                i => new InfluenceCell(i.Effect, i.Note)));
    }

    public async Task<ParameterResult> CreateAsync(
        Guid topicId,
        Guid ownerId,
        string name,
        IReadOnlyList<string> parameterNames,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ParameterResult.Refused("Give the table a name.");
        }

        var factors = parameterNames
            .Select(n => n?.Trim() ?? string.Empty)
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (factors.Count < 2)
        {
            return ParameterResult.Refused(
                "Name at least two factors — the table is about how they affect each other.");
        }

        if (factors.Count > ParameterTable.MaxParameters)
        {
            return ParameterResult.Refused(
                $"At most {ParameterTable.MaxParameters} factors. Beyond that the grid has more " +
                "cells than anyone can read; deciding which are actually key is part of the exercise.");
        }

        var topicExists = await database.Topics
            .AsNoTracking()
            .AnyAsync(t => t.Id == topicId, cancellationToken);

        if (!topicExists)
        {
            return ParameterResult.Refused("No such topic.");
        }

        var now = clock.UtcNow;
        var table = new ParameterTable(Guid.NewGuid(), topicId, ownerId, name, now);
        database.ParameterTables.Add(table);

        for (var index = 0; index < factors.Count; index++)
        {
            database.Parameters.Add(new Parameter(Guid.NewGuid(), table.Id, factors[index], index + 1));
        }

        await database.SaveChangesAsync(cancellationToken);

        return ParameterResult.Ok(table.Id);
    }

    /// <summary>Replaces the grid's contents. Owner only.</summary>
    public async Task<ParameterResult> SaveInfluencesAsync(
        Guid topicId,
        Guid tableId,
        Guid userId,
        IReadOnlyDictionary<(Guid From, Guid To), (InfluenceEffect Effect, string? Note)> cells,
        CancellationToken cancellationToken = default)
    {
        var table = await database.ParameterTables
            .SingleOrDefaultAsync(t => t.Id == tableId && t.TopicId == topicId, cancellationToken);

        if (table is null)
        {
            return ParameterResult.Refused("No such table.");
        }

        if (table.OwnerId != userId)
        {
            return ParameterResult.Refused("Only the person who made this table can change it.");
        }

        // Cell coordinates arrive from a form, so only parameters that belong to this table are
        // accepted; anything else is discarded rather than stored.
        var known = await database.Parameters
            .AsNoTracking()
            .Where(p => p.TableId == tableId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var valid = known.ToHashSet();

        var existing = await database.ParameterInfluences
            .Where(i => i.TableId == tableId)
            .ToListAsync(cancellationToken);

        foreach (var ((from, to), (effect, note)) in cells)
        {
            if (from == to || !valid.Contains(from) || !valid.Contains(to))
            {
                continue;
            }

            var stored = existing.SingleOrDefault(i => i.FromParameterId == from && i.ToParameterId == to);

            if (effect == InfluenceEffect.None && string.IsNullOrWhiteSpace(note))
            {
                // "No effect" with nothing to say is the default; storing a row for every such
                // cell would fill the table with noise.
                if (stored is not null)
                {
                    database.ParameterInfluences.Remove(stored);
                }

                continue;
            }

            if (stored is null)
            {
                database.ParameterInfluences.Add(new ParameterInfluence(tableId, from, to, effect, note));
            }
            else
            {
                stored.ChangeTo(effect, note);
            }
        }

        table.Touch(clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);

        return ParameterResult.Ok(tableId);
    }

    public async Task<ParameterResult> ShareAsync(
        Guid topicId,
        Guid tableId,
        Guid userId,
        bool shared,
        CancellationToken cancellationToken = default)
    {
        var table = await database.ParameterTables
            .SingleOrDefaultAsync(t => t.Id == tableId && t.TopicId == topicId, cancellationToken);

        if (table is null)
        {
            return ParameterResult.Refused("No such table.");
        }

        if (table.OwnerId != userId)
        {
            return ParameterResult.Refused("Only the person who made this table can share it.");
        }

        table.Share(shared, clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);

        return ParameterResult.Ok(tableId);
    }
}
