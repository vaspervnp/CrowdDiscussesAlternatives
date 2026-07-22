using CDA.Application.Abstractions;
using CDA.Application.Topics;
using CDA.Domain.Discussion;
using CDA.Domain.Topics;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Discussion;

/// <summary>A comment together with the author details needed to display it.</summary>
public sealed record CommentView(
    Guid Id,
    Guid AuthorId,
    string AuthorDisplayName,
    string Body,
    DateTime CreatedAtUtc,
    DateTime? EditedAtUtc,
    bool IsDeleted,
    bool CanEdit,
    bool CanDelete);

public sealed record CommentResult(bool Succeeded, string? Error = null)
{
    public static readonly CommentResult Ok = new(true);

    public static CommentResult Refused(string reason) => new(false, reason);
}

public sealed class CommentService(CdaDbContext database, IClock clock)
{
    /// <summary>
    /// Posts a comment to a topic's discussion.
    /// </summary>
    /// <remarks>
    /// Posting to a public topic joins it. Requiring a separate "join" click before speaking
    /// adds a step that carries no decision — anyone who may read a public topic may take part
    /// in it — and it leaves people who have clearly engaged outside the membership list the
    /// rest of the platform reasons about. Invite-only topics are unaffected: the access
    /// policy has already refused a non-member by this point.
    /// </remarks>
    public async Task<CommentResult> PostToTopicAsync(
        Guid topicId,
        Guid authorId,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return CommentResult.Refused("A comment cannot be empty.");
        }

        if (body.Length > Comment.BodyMaxLength)
        {
            return CommentResult.Refused($"A comment is limited to {Comment.BodyMaxLength} characters.");
        }

        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == topicId, cancellationToken);

        if (topic is null)
        {
            return CommentResult.Refused("No such topic.");
        }

        var now = clock.UtcNow;

        if (topic.IsClosedAt(now))
        {
            return CommentResult.Refused("This topic has closed.");
        }

        var isMember = await database.TopicMembers
            .AnyAsync(m => m.TopicId == topicId && m.UserId == authorId, cancellationToken);

        if (!isMember)
        {
            if (topic.Visibility != TopicVisibility.Public)
            {
                return CommentResult.Refused("Only members can take part in this topic.");
            }

            database.TopicMembers.Add(new TopicMember(topicId, authorId, TopicRole.Member, now));
        }

        database.Comments.Add(Comment.OnTopic(topicId, authorId, body, now));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Joined concurrently by another request of theirs; the membership key settled it.
            database.ChangeTracker.Clear();
            database.Comments.Add(Comment.OnTopic(topicId, authorId, body, now));
            await database.SaveChangesAsync(cancellationToken);
        }

        return CommentResult.Ok;
    }

    /// <summary>
    /// Posts a comment on a proposal.
    /// </summary>
    /// <remarks>
    /// Allowed throughout, including while the proposal is still editable — that is the point
    /// of the editing window. Voting waits for the wording to settle; discussing it is how the
    /// author learns what to improve.
    /// </remarks>
    public async Task<CommentResult> PostToProposalAsync(
        Guid topicId,
        Guid proposalId,
        Guid authorId,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return CommentResult.Refused("A comment cannot be empty.");
        }

        if (body.Length > Comment.BodyMaxLength)
        {
            return CommentResult.Refused($"A comment is limited to {Comment.BodyMaxLength} characters.");
        }

        // Scoped to the topic the caller was authorised against.
        var proposal = await database.Proposals
            .SingleOrDefaultAsync(p => p.Id == proposalId && p.TopicId == topicId, cancellationToken);

        if (proposal is null)
        {
            return CommentResult.Refused("No such proposal.");
        }

        var topic = await database.Topics
            .AsNoTracking()
            .SingleAsync(t => t.Id == topicId, cancellationToken);

        var now = clock.UtcNow;

        if (topic.IsClosedAt(now))
        {
            return CommentResult.Refused("This topic has closed.");
        }

        var isMember = await database.TopicMembers
            .AnyAsync(m => m.TopicId == topicId && m.UserId == authorId, cancellationToken);

        if (!isMember)
        {
            if (topic.Visibility != TopicVisibility.Public)
            {
                return CommentResult.Refused("Only members can take part in this topic.");
            }

            database.TopicMembers.Add(new TopicMember(topicId, authorId, TopicRole.Member, now));
        }

        database.Comments.Add(Comment.OnProposal(proposalId, authorId, body, now));

        // Kept in step with the comment in the same save: the "most recently discussed"
        // ordering reads this column, and a comment without it would leave the proposal
        // sorted as though nothing had been said.
        proposal.RecordComment(now);

        await database.SaveChangesAsync(cancellationToken);

        return CommentResult.Ok;
    }

    /// <summary>Reads a proposal's comments, oldest first.</summary>
    public Task<List<CommentView>> ForProposalAsync(
        Guid proposalId,
        TopicViewer viewer,
        CancellationToken cancellationToken = default) =>
        ReadAsync(database.Comments.Where(c => c.ProposalId == proposalId), viewer, cancellationToken);

    /// <summary>
    /// Reads a topic's discussion, oldest first.
    /// </summary>
    /// <remarks>
    /// Withdrawn comments are returned as tombstones rather than dropped: replies below them
    /// stop making sense when the remark they answer disappears.
    /// </remarks>
    public Task<List<CommentView>> ForTopicAsync(
        Guid topicId,
        TopicViewer viewer,
        CancellationToken cancellationToken = default) =>
        ReadAsync(database.Comments.Where(c => c.TopicId == topicId), viewer, cancellationToken);

    private async Task<List<CommentView>> ReadAsync(
        IQueryable<Comment> comments,
        TopicViewer viewer,
        CancellationToken cancellationToken)
    {
        var rows = await comments
            .AsNoTracking()
            .OrderBy(c => c.CreatedAtUtc)
            .Join(
                database.UserProfiles.AsNoTracking(),
                comment => comment.AuthorId,
                profile => profile.Id,
                (comment, profile) => new { comment, profile.DisplayName })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new CommentView(
            row.comment.Id,
            row.comment.AuthorId,
            row.DisplayName,
            row.comment.IsDeleted ? string.Empty : row.comment.Body,
            row.comment.CreatedAtUtc,
            row.comment.EditedAtUtc,
            row.comment.IsDeleted,
            CanEdit: !row.comment.IsDeleted && viewer.UserId == row.comment.AuthorId,
            CanDelete: !row.comment.IsDeleted
                && (viewer.UserId == row.comment.AuthorId || viewer.IsFacilitator || viewer.IsAdministrator)))];
    }

    public async Task<CommentResult> EditAsync(
        Guid topicId,
        Guid commentId,
        Guid userId,
        string body,
        CancellationToken cancellationToken = default)
    {
        var comment = await database.Comments
            .SingleOrDefaultAsync(c => c.Id == commentId && c.TopicId == topicId, cancellationToken);

        if (comment is null || comment.IsDeleted)
        {
            return CommentResult.Refused("No such comment.");
        }

        // Only the author, whatever anyone's role: editing someone else's words would put
        // statements in their name.
        if (comment.AuthorId != userId)
        {
            return CommentResult.Refused("Only the author can edit a comment.");
        }

        comment.Edit(body, clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);

        return CommentResult.Ok;
    }

    /// <summary>Withdraws a comment. Authors may remove their own; facilitators may moderate.</summary>
    /// <remarks>
    /// Scoped to the topic the caller was authorised against, not looked up by comment id
    /// alone. <paramref name="viewer"/> carries a facilitator role for <em>that</em> topic, so
    /// without the scope a facilitator of one topic could moderate every other topic by
    /// quoting a foreign comment id.
    /// </remarks>
    public async Task<CommentResult> DeleteAsync(
        Guid topicId,
        Guid commentId,
        TopicViewer viewer,
        CancellationToken cancellationToken = default)
    {
        var comment = await database.Comments
            .SingleOrDefaultAsync(c => c.Id == commentId && c.TopicId == topicId, cancellationToken);

        if (comment is null)
        {
            return CommentResult.Ok;
        }

        var allowed = viewer.UserId == comment.AuthorId || viewer.IsFacilitator || viewer.IsAdministrator;

        if (!allowed)
        {
            return CommentResult.Refused("You cannot remove this comment.");
        }

        comment.Delete(clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);

        return CommentResult.Ok;
    }
}
