using CDA.Application.Abstractions;
using CDA.Application.Topics;
using CDA.Domain.Proposals;
using CDA.Infrastructure.Discussion;
using CDA.Infrastructure.Persistence;
using CDA.Infrastructure.Proposals;
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
    IClock clock) : Controller
{
    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        Guid topicId,
        ProposalSort sort = ProposalSort.Score,
        Guid? author = null,
        string? cursor = null)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var page = await proposals.ListAsync(
            topicId, viewer, sort, author, cursor, HttpContext.RequestAborted);

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
        });
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
