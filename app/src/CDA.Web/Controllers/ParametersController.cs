using CDA.Application.Topics;
using CDA.Domain.Parameters;
using CDA.Infrastructure.Parameters;
using CDA.Infrastructure.Persistence;
using CDA.Infrastructure.Topics;
using CDA.Web.Models;
using CDA.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CDA.Web.Controllers;

[Route("topics/{topicId:guid}/factors")]
public sealed class ParametersController(
    CdaDbContext database,
    TopicService topics,
    ParameterTableService tables) : Controller
{
    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(Guid topicId)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        return View(new ParameterListViewModel
        {
            TopicId = topicId,
            TopicSubject = topic.Subject,
            Tables = await tables.ListAsync(topicId, viewer.UserId, HttpContext.RequestAborted),
            CanCreate = viewer.IsSignedIn && !topic.IsClosedAt(DateTime.UtcNow),
        });
    }

    [HttpGet("new")]
    [Authorize]
    public async Task<IActionResult> Create(Guid topicId)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        return View(new ParameterListViewModel
        {
            TopicId = topicId,
            TopicSubject = topic.Subject,
            Tables = [],
            CanCreate = true,
        });
    }

    [HttpPost("new")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid topicId, string name, string factors)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        // One factor per line: simpler to type than a row of boxes, and easy to paste into.
        var names = (factors ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = await tables.CreateAsync(
            topicId, viewer.UserId!.Value, name ?? string.Empty, names, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Create), new { topicId });
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

        var table = await tables.GetAsync(topicId, id, viewer.UserId, HttpContext.RequestAborted);

        if (table is null)
        {
            // Unshared and not the caller's: the same answer as "no such table", so the
            // response does not confirm that someone has a private sketch here.
            return NotFound();
        }

        return View(new ParameterTableViewModel
        {
            TopicId = topicId,
            TopicSubject = topic.Subject,
            Table = table,
        });
    }

    [HttpPost("{id:guid}")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        Guid topicId,
        Guid id,
        Guid[] from,
        Guid[] to,
        InfluenceEffect[] effects,
        string[] notes)
    {
        if (from.Length != to.Length || from.Length != effects.Length || from.Length != notes.Length)
        {
            TempData["Error"] = "That form did not arrive intact. Please try again.";
            return RedirectToAction(nameof(Details), new { topicId, id });
        }

        var cells = new Dictionary<(Guid, Guid), (InfluenceEffect, string?)>();

        for (var index = 0; index < from.Length; index++)
        {
            cells[(from[index], to[index])] = (effects[index], notes[index]);
        }

        var result = await tables.SaveInfluencesAsync(
            topicId, id, User.GetUserId()!.Value, cells, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Details), new { topicId, id });
    }

    [HttpPost("{id:guid}/share")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Share(Guid topicId, Guid id, bool shared)
    {
        var result = await tables.ShareAsync(
            topicId, id, User.GetUserId()!.Value, shared, HttpContext.RequestAborted);

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
