using CDA.Application.Abstractions;
using CDA.Application.Topics;
using CDA.Domain.Topics;
using CDA.Infrastructure.Persistence;
using CDA.Infrastructure.Topics;
using CDA.Infrastructure.Voting;
using CDA.Web.Models;
using CDA.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CDA.Web.Controllers;

[Route("topics")]
public sealed class TopicsController(
    CdaDbContext database,
    TopicService topics,
    TopicVotingService voting,
    IClock clock) : Controller
{
    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(TopicSort sort = TopicSort.Importance, string? cursor = null)
    {
        var page = await topics.ListAsync(
            User.GetUserId(),
            User.IsPlatformAdministrator(),
            sort,
            cursor,
            HttpContext.RequestAborted);

        return View(new TopicListViewModel
        {
            Topics = page.Items,
            Sort = sort,
            NextCursor = page.NextCursor,
        });
    }

    [HttpGet("create")]
    [Authorize]
    public IActionResult Create() => View(new CreateTopicViewModel());

    [HttpPost("create")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTopicViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var topic = await topics.CreateAsync(
            model.Subject,
            model.Description,
            User.GetUserId()!.Value,
            model.Visibility,
            // The form collects a date; the domain stores UTC instants, and the end of the
            // chosen day is what "complete by this date" means.
            model.ClosesAt?.Date.AddDays(1).AddTicks(-1),
            HttpContext.RequestAborted);

        if (model.HideVoteCountsUntilClose)
        {
            topic.SetVoteCountsHidden(true);
            await database.SaveChangesAsync(HttpContext.RequestAborted);
        }

        return RedirectToAction(nameof(Details), new { id = topic.Id });
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(Guid id)
    {
        var (topic, viewer) = await LoadAsync(id);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            // Deliberately the same answer for "does not exist" and "you may not see it":
            // otherwise the response confirms that a private topic exists.
            return NotFound();
        }

        var myVote = viewer.UserId is { } userId
            ? await database.Votes.AsNoTracking()
                .Where(v => v.TopicId == id && v.UserId == userId)
                .Select(v => (short?)v.Value)
                .SingleOrDefaultAsync(HttpContext.RequestAborted)
            : null;

        return View(TopicView.Project(topic, viewer, clock.UtcNow, myVote));
    }

    [HttpPost("{id:guid}/vote")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(Guid id, short value)
    {
        var (topic, viewer) = await LoadAsync(id);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        if (!TopicAccessPolicy.CanVote(topic, viewer, clock.UtcNow))
        {
            return Forbid();
        }

        var result = await voting.CastAsync(id, viewer.UserId!.Value, value, HttpContext.RequestAborted);

        if (result.Outcome is VoteOutcome.Closed)
        {
            TempData["Error"] = "This topic has closed; votes are no longer accepted.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/vote/withdraw")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawVote(Guid id)
    {
        var (topic, viewer) = await LoadAsync(id);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        await voting.WithdrawAsync(id, viewer.UserId!.Value, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/join")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(Guid id)
    {
        var joined = await topics.JoinAsync(id, User.GetUserId()!.Value, HttpContext.RequestAborted);

        if (!joined)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/phase")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePhase(Guid id, TopicPhase phase)
    {
        var topic = await database.Topics.SingleOrDefaultAsync(t => t.Id == id, HttpContext.RequestAborted);

        if (topic is null)
        {
            return NotFound();
        }

        var viewer = await topics.ViewerForAsync(
            topic, User.GetUserId(), User.IsPlatformAdministrator(), HttpContext.RequestAborted);

        if (!TopicAccessPolicy.CanAdminister(topic, viewer))
        {
            return Forbid();
        }

        try
        {
            topic.MoveTo(phase);
        }
        catch (InvalidOperationException error)
        {
            TempData["Error"] = error.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        await database.SaveChangesAsync(HttpContext.RequestAborted);

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<(Topic? Topic, TopicViewer Viewer)> LoadAsync(Guid id)
    {
        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == id, HttpContext.RequestAborted);

        if (topic is null)
        {
            return (null, TopicViewer.Anonymous);
        }

        var viewer = await topics.ViewerForAsync(
            topic, User.GetUserId(), User.IsPlatformAdministrator(), HttpContext.RequestAborted);

        return (topic, viewer);
    }
}
