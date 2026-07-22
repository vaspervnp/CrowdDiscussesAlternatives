using CDA.Application.Abstractions;
using CDA.Application.Topics;
using CDA.Infrastructure.Discussion;
using CDA.Infrastructure.Groups;
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

[Route("topics/{topicId:guid}/alternatives")]
public sealed class GroupsController(
    CdaDbContext database,
    TopicService topics,
    GroupService groups,
    GroupVotingService voting,
    ProposalService proposals,
    CommentService comments,
    IClock clock) : Controller
{
    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(Guid topicId, GroupSort sort = GroupSort.Score, string? cursor = null)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var page = await groups.ListAsync(topicId, viewer, sort, cursor, HttpContext.RequestAborted);

        return View(new GroupListViewModel
        {
            Topic = TopicView.Project(topic, viewer, clock.UtcNow),
            Groups = page.Items,
            Sort = sort,
            NextCursor = page.NextCursor,
            TopCiters = page.TopCiters,
            CanAssemble = topic.Phase == Domain.Topics.TopicPhase.Proposing
                && !topic.IsClosedAt(clock.UtcNow)
                && viewer.IsSignedIn,
        });
    }

    [HttpGet("assemble")]
    [Authorize]
    public async Task<IActionResult> Assemble(Guid topicId, Guid? improves = null)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        // Every proposal in the pool, so the whole set can be picked from in one place.
        var pool = await proposals.ListAsync(
            topicId, viewer, ProposalSort.Score, null, null, null, HttpContext.RequestAborted);

        return View(new AssembleGroupViewModel
        {
            Topic = TopicView.Project(topic, viewer, clock.UtcNow),
            Pool = pool.Items,
            ImprovesGroupId = improves,
            ImprovesDescription = improves is { } parent
                ? await database.ProposalGroups.AsNoTracking()
                    .Where(g => g.Id == parent && g.TopicId == topicId)
                    .Select(g => g.Description)
                    .SingleOrDefaultAsync(HttpContext.RequestAborted)
                : null,
        });
    }

    [HttpPost("assemble")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assemble(
        Guid topicId,
        string description,
        Guid[] proposalIds,
        Guid? improvesGroupId)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var result = await groups.CreateAsync(
            topicId, viewer.UserId!.Value, description ?? string.Empty,
            proposalIds ?? [], improvesGroupId, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Assemble), new { topicId, improves = improvesGroupId });
        }

        return RedirectToAction(nameof(Details), new { topicId, id = result.Id });
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

        var group = await groups.GetAsync(topicId, id, viewer, HttpContext.RequestAborted);

        if (group is null)
        {
            return NotFound();
        }

        return View(new GroupDetailsViewModel
        {
            Topic = TopicView.Project(topic, viewer, clock.UtcNow),
            Group = group,
            Comments = await comments.ForGroupAsync(id, viewer, HttpContext.RequestAborted),
            CanComment = viewer.IsSignedIn && !topic.IsClosedAt(clock.UtcNow),
        });
    }

    [HttpPost("{id:guid}/edit")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid topicId, Guid id, string description)
    {
        var result = await groups.EditAsync(
            topicId, id, User.GetUserId()!.Value, description ?? string.Empty, null,
            HttpContext.RequestAborted);

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

        // The group id comes from the route, so confirm it belongs to this topic.
        var belongs = await database.ProposalGroups.AsNoTracking()
            .AnyAsync(g => g.Id == id && g.TopicId == topicId, HttpContext.RequestAborted);

        if (!belongs)
        {
            return NotFound();
        }

        await voting.CastAsync(id, viewer.UserId!.Value, value, HttpContext.RequestAborted);

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

        var result = await comments.PostToGroupAsync(
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
