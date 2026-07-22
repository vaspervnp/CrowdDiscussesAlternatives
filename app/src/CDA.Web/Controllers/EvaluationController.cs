using CDA.Application.Topics;
using CDA.Infrastructure.Evaluation;
using CDA.Infrastructure.Persistence;
using CDA.Infrastructure.Topics;
using CDA.Web.Models;
using CDA.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CDA.Web.Controllers;

/// <summary>
/// The private weighing-up a participant does before voting.
/// </summary>
/// <remarks>
/// Every action here requires signing in and reads only the caller's own rows. There is no
/// route by which one participant can see another's evaluation, which is deliberate: the vote
/// is the public act, the reasoning behind it is not.
/// </remarks>
[Route("topics/{topicId:guid}/evaluate")]
[Authorize]
public sealed class EvaluationController(
    CdaDbContext database,
    TopicService topics,
    EvaluationService evaluations) : Controller
{
    [HttpGet("{groupId:guid}")]
    public async Task<IActionResult> Evaluate(Guid topicId, Guid groupId)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var evaluation = await evaluations.GetAsync(
            topicId, groupId, viewer.UserId!.Value, HttpContext.RequestAborted);

        if (evaluation is null)
        {
            return NotFound();
        }

        return View(new EvaluateViewModel
        {
            TopicId = topicId,
            TopicSubject = topic.Subject,
            Evaluation = evaluation,
        });
    }

    [HttpPost("{groupId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Evaluate(
        Guid topicId,
        Guid groupId,
        Guid[] requirementIds,
        int[] weights,
        int[] scores)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        // The three arrays come from parallel form fields; a mismatch means a malformed post.
        if (requirementIds.Length != weights.Length || requirementIds.Length != scores.Length)
        {
            TempData["Error"] = "That form did not arrive intact. Please try again.";
            return RedirectToAction(nameof(Evaluate), new { topicId, groupId });
        }

        var weightMap = requirementIds.Zip(weights).ToDictionary(pair => pair.First, pair => pair.Second);
        var scoreMap = requirementIds.Zip(scores).ToDictionary(pair => pair.First, pair => pair.Second);

        var result = await evaluations.SaveAsync(
            topicId, groupId, viewer.UserId!.Value, weightMap, scoreMap, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Evaluate), new { topicId, groupId });
        }

        return RedirectToAction(nameof(Compare), new { topicId });
    }

    [HttpGet("")]
    public async Task<IActionResult> Compare(Guid topicId)
    {
        var (topic, viewer) = await LoadTopicAsync(topicId);

        if (topic is null || !TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        return View(new CompareViewModel
        {
            TopicId = topicId,
            TopicSubject = topic.Subject,
            Comparison = await evaluations.CompareAsync(
                topicId, viewer.UserId!.Value, HttpContext.RequestAborted),
        });
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
