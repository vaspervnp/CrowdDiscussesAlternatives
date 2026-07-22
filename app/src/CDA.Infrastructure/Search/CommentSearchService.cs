using CDA.Domain.Search;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Search;

public enum SearchResultMode
{
    /// <summary>The matching comments themselves.</summary>
    Comments,

    /// <summary>The proposals whose comments matched — the tagging workflow.</summary>
    Proposals,
}

public sealed record CommentHit(
    Guid CommentId,
    string Body,
    string AuthorDisplayName,
    Guid AuthorId,
    DateTime CreatedAtUtc,
    Guid? ProposalId,
    string? ProposalText,
    Guid? GroupId,
    string? GroupDescription);

public sealed record ProposalHit(
    Guid ProposalId,
    string Text,
    string AuthorDisplayName,
    int MatchingComments,
    IReadOnlyList<string> Excerpts);

public sealed record SearchResults(
    IReadOnlyList<CommentHit> Comments,
    IReadOnlyList<ProposalHit> Proposals,
    IReadOnlyList<string> IgnoredShortTerms,
    string? Error)
{
    public static SearchResults Failed(string error, IReadOnlyList<string>? ignored = null) =>
        new([], [], ignored ?? [], error);
}

/// <summary>
/// Searching what people have said, within one topic.
/// </summary>
/// <remarks>
/// <para>
/// This exists for more than finding a half-remembered remark. The platform's documents
/// describe using it to tag and categorise proposals: write <c>pros</c> or <c>cons</c> in a
/// comment, then pull back every proposal whose comments carry that word. That is why the
/// results can be returned as proposals rather than as comments.
/// </para>
/// <para>
/// Matching is done by MariaDB's full-text index in boolean mode. Terms of one or two
/// characters are not in the index and can never match; the parser reports them so the caller
/// can say so rather than returning nothing without explanation.
/// </para>
/// </remarks>
public sealed class CommentSearchService(CdaDbContext database)
{
    public const int MaxResults = 100;

    public async Task<SearchResults> SearchAsync(
        Guid topicId,
        string? rawQuery,
        Guid? authorId,
        SearchResultMode mode,
        CancellationToken cancellationToken = default)
    {
        var query = CommentQueryParser.Parse(rawQuery);

        if (!query.IsUsable)
        {
            return SearchResults.Failed(query.Error!, query.IgnoredShortTerms);
        }

        // FromSqlRaw carries the MATCH, which EF cannot express; the rest composes on top of it
        // as an ordinary query, so scoping and paging stay in LINQ.
        var matching = database.Comments
            .FromSqlRaw(
                "SELECT * FROM Comments WHERE MATCH(Body) AGAINST ({0} IN BOOLEAN MODE)",
                query.BooleanExpression)
            .AsNoTracking()
            .Where(c => c.OwningTopicId == topicId && c.DeletedAtUtc == null);

        if (authorId is { } author)
        {
            matching = matching.Where(c => c.AuthorId == author);
        }

        var rows = await matching
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(MaxResults)
            .Join(database.UserProfiles.AsNoTracking(),
                c => c.AuthorId, profile => profile.Id,
                (c, profile) => new
                {
                    c.Id,
                    c.Body,
                    c.AuthorId,
                    profile.DisplayName,
                    c.CreatedAtUtc,
                    c.ProposalId,
                    c.GroupId,
                })
            .ToListAsync(cancellationToken);

        var proposalIds = rows.Where(r => r.ProposalId != null).Select(r => r.ProposalId!.Value).Distinct().ToList();
        var groupIds = rows.Where(r => r.GroupId != null).Select(r => r.GroupId!.Value).Distinct().ToList();

        var proposalText = await database.Proposals.AsNoTracking()
            .Where(p => proposalIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Text, cancellationToken);

        var groupText = await database.ProposalGroups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Description, cancellationToken);

        var comments = rows
            .Select(r => new CommentHit(
                r.Id,
                r.Body,
                r.DisplayName,
                r.AuthorId,
                r.CreatedAtUtc,
                r.ProposalId,
                r.ProposalId is { } pid ? proposalText.GetValueOrDefault(pid) : null,
                r.GroupId,
                r.GroupId is { } gid ? groupText.GetValueOrDefault(gid) : null))
            .ToList();

        if (mode == SearchResultMode.Comments)
        {
            return new SearchResults(comments, [], query.IgnoredShortTerms, null);
        }

        var authors = await database.Proposals.AsNoTracking()
            .Where(p => proposalIds.Contains(p.Id))
            .Join(database.UserProfiles.AsNoTracking(),
                p => p.AuthorId, profile => profile.Id,
                (p, profile) => new { p.Id, profile.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var proposals = comments
            .Where(hit => hit.ProposalId is not null)
            .GroupBy(hit => hit.ProposalId!.Value)
            .Select(group => new ProposalHit(
                group.Key,
                proposalText.GetValueOrDefault(group.Key, "(removed)"),
                authors.GetValueOrDefault(group.Key, "(unknown)"),
                group.Count(),
                // The matching comments themselves, so a reader can see why it came back —
                // which is the whole point when the words are being used as tags.
                [.. group.Select(hit => hit.Body).Take(3)]))
            .OrderByDescending(hit => hit.MatchingComments)
            .ToList();

        return new SearchResults([], proposals, query.IgnoredShortTerms, null);
    }
}
