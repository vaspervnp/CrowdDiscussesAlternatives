using CDA.Application.Abstractions;
using CDA.Application.Topics;
using CDA.Domain.Proposals;
using CDA.Domain.References;
using CDA.Infrastructure.Discussion;
using CDA.Infrastructure.Persistence;
using CDA.Infrastructure.Proposals;
using CDA.Infrastructure.References;
using CDA.Infrastructure.Similarity;
using CDA.Infrastructure.Topics;
using CDA.Infrastructure.Voting;
using CDA.Web.Models;
using CDA.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CDA.Web.Controllers;

[Route("topics/{topicId:guid}/proposals")]
public sealed class ProposalsController(
    CdaDbContext database,
    TopicService topics,
    ProposalService proposals,
    ProposalVotingService voting,
    CommentService comments,
    ReferenceService references,
    ReferenceVotingService referenceVoting,
    SimilarityService similarity,
    SimilarityVotingService similarityVoting,
    IClock clock) : Controller
{
    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        Guid topicId,
        ProposalSort sort = ProposalSort.Score,
        Guid? author = null,
        string? cursor = null,
        bool? collapse = null,
        int? threshold = null)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        // Folding is off unless the reader turns it on, and the level is theirs: the platform
        // reports similarity rather than deciding it, so nothing vanishes unasked.
        var effectiveThreshold = collapse == true
            ? threshold ?? topic.DefaultSimilarityThreshold
            : (int?)null;

        var page = await proposals.ListAsync(
            topicId, viewer, sort, author, cursor, effectiveThreshold, HttpContext.RequestAborted);

        return View(new ProposalListViewModel
        {
            Topic = TopicView.Project(topic, viewer, clock.UtcNow),
            Proposals = page.Items,
            Sort = sort,
            AuthorFilter = author,
            AuthorFilterName = author is { } id
                ? await database.UserProfiles.AsNoTracking()
                    .Where(p => p.Id == id).Select(p => p.DisplayName)
                    .SingleOrDefaultAsync(HttpContext.RequestAborted)
                : null,
            NextCursor = page.NextCursor,
            CanAdd = topic.Phase == Domain.Topics.TopicPhase.Proposing
                && !topic.IsClosedAt(clock.UtcNow)
                && viewer.IsSignedIn,
            Collapse = collapse == true,
            Threshold = threshold ?? topic.DefaultSimilarityThreshold,
        });
    }

    [HttpPost("")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid topicId, string text, int? editableForDays)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var editableUntil = editableForDays is { } days and > 0
            ? clock.UtcNow.AddDays(days)
            : (DateTime?)null;

        var result = await proposals.CreateAsync(
            topicId, viewer.UserId!.Value, text ?? string.Empty, editableUntil, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Index), new { topicId, sort = ProposalSort.Newest });
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(Guid topicId, Guid id)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var proposal = await proposals.GetAsync(topicId, id, viewer, HttpContext.RequestAborted);

        if (proposal is null)
        {
            return NotFound();
        }

        return View(new ProposalDetailsViewModel
        {
            Topic = TopicView.Project(topic, viewer, clock.UtcNow),
            Proposal = proposal,
            Comments = await comments.ForProposalAsync(id, viewer, HttpContext.RequestAborted),
            References = await references.ForProposalAsync(id, viewer.UserId, HttpContext.RequestAborted),
            Similarities = await similarity.ForProposalAsync(
                id, viewer.UserId, topic.DefaultSimilarityThreshold, HttpContext.RequestAborted),
            CanCite = viewer.IsSignedIn && !topic.IsClosedAt(clock.UtcNow),
        });
    }

    [HttpPost("{id:guid}/similar")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportSimilar(
        Guid topicId,
        Guid id,
        Guid otherProposalId,
        Guid? betterWrittenProposalId,
        string? justification)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var result = await similarity.ReportAsync(
            topicId, id, otherProposalId, viewer.UserId!.Value,
            betterWrittenProposalId, justification, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Details), new { topicId, id });
    }

    [HttpPost("{id:guid}/similar/{similarityId:guid}/vote")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoteOnSimilarity(Guid topicId, Guid id, Guid similarityId, short value)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        // The report id comes from the route, so confirm it belongs to this topic.
        var belongs = await database.SimilarityReports.AsNoTracking()
            .AnyAsync(r => r.Id == similarityId && r.TopicId == topicId, HttpContext.RequestAborted);

        if (!belongs)
        {
            return NotFound();
        }

        await similarityVoting.CastAsync(similarityId, viewer.UserId!.Value, value, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Details), new { topicId, id });
    }

    [HttpPost("{id:guid}/references")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReference(Guid topicId, Guid id, string url, string description)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var result = await references.AttachAsync(
            topicId, id, viewer.UserId!.Value, url ?? string.Empty, description ?? string.Empty,
            HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Details), new { topicId, id });
    }

    [HttpPost("{id:guid}/references/{referenceId:guid}/vote")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoteOnReference(
        Guid topicId,
        Guid id,
        Guid referenceId,
        ReferenceAspect aspect,
        short value)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        // The reference id comes from the route, so confirm it belongs to this topic before
        // letting a vote touch it.
        var belongs = await database.References.AsNoTracking()
            .AnyAsync(r => r.Id == referenceId && r.TopicId == topicId, HttpContext.RequestAborted);

        if (!belongs)
        {
            return NotFound();
        }

        await referenceVoting.CastAsync(
            new ReferenceVoteTarget(referenceId, aspect), viewer.UserId!.Value, value,
            HttpContext.RequestAborted);

        return RedirectToAction(nameof(Details), new { topicId, id });
    }

    [HttpPost("{id:guid}/edit")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid topicId, Guid id, string text)
    {
        var result = await proposals.EditAsync(
            topicId, id, User.GetUserId()!.Value, text ?? string.Empty, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Details), new { topicId, id });
    }

    [HttpPost("{id:guid}/lock")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(Guid topicId, Guid id)
    {
        var result = await proposals.LockAsync(
            topicId, id, User.GetUserId()!.Value, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Details), new { topicId, id });
    }

    [HttpPost("{id:guid}/vote")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(Guid topicId, Guid id, short value)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var result = await voting.CastAsync(id, viewer.UserId!.Value, value, HttpContext.RequestAborted);

        TempData["Error"] = result.Outcome switch
        {
            VoteOutcome.NotOpenYet =>
                "This proposal is still open for improvement, so it cannot be voted on yet. " +
                "Comment on it instead — that is what the editing window is for.",
            VoteOutcome.Closed => "This topic has closed; votes are no longer accepted.",
            _ => null,
        };

        return RedirectToAction(nameof(Details), new { topicId, id });
    }

    [HttpPost("{id:guid}/vote/withdraw")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawVote(Guid topicId, Guid id)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        await voting.WithdrawAsync(id, viewer.UserId!.Value, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Details), new { topicId, id });
    }

    [HttpPost("{id:guid}/comments")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostComment(Guid topicId, Guid id, string body)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var result = await comments.PostToProposalAsync(
            topicId, id, viewer.UserId!.Value, body ?? string.Empty, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Details), new { topicId, id });
    }

    private async Task<(Domain.Topics.Topic? Topic, TopicViewer Viewer)> LoadTopicAsync(Guid topicId)
    {
        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == topicId, HttpContext.RequestAborted);

        if (topic is null)
        {
            return (null, TopicViewer.Anonymous);
        }

        var viewer = await topics.ViewerForAsync(
            topic, User.GetUserId(), User.IsPlatformAdministrator(), HttpContext.RequestAborted);

        return (topic, viewer);
    }
}
