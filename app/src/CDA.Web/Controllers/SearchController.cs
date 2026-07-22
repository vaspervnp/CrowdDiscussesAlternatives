using CDA.Application.Topics;
using CDA.Infrastructure.Persistence;
using CDA.Infrastructure.Search;
using CDA.Infrastructure.Topics;
using CDA.Web.Models;
using CDA.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CDA.Web.Controllers;

[Route("topics/{topicId:guid}/search")]
public sealed class SearchController(
    CdaDbContext database,
    TopicService topics,
    CommentSearchService search) : Controller
{
    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(
        Guid topicId,
        string? q = null,
        Guid? author = null,
        SearchResultMode mode = SearchResultMode.Proposals)
    {
        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == topicId, HttpContext.RequestAborted);

        if (topic is null)
        {
            return NotFound();
        }

        var viewer = await topics.ViewerForAsync(
            topic, User.GetUserId(), User.IsPlatformAdministrator(), HttpContext.RequestAborted);

        if (!TopicAccessPolicy.CanView(topic, viewer))
        {
            return NotFound();
        }

        var results = string.IsNullOrWhiteSpace(q)
            ? null
            : await search.SearchAsync(topicId, q, author, mode, HttpContext.RequestAborted);

        return View(new SearchViewModel
        {
            TopicId = topicId,
            TopicSubject = topic.Subject,
            Query = q,
            Mode = mode,
            AuthorFilter = author,
            AuthorFilterName = author is { } id
                ? await database.UserProfiles.AsNoTracking()
                    .Where(p => p.Id == id).Select(p => p.DisplayName)
                    .SingleOrDefaultAsync(HttpContext.RequestAborted)
                : null,
            Results = results,
            // Only people who have said something in this topic can be filtered to; listing
            // every account would be both useless and a slow query.
            Contributors = await database.Comments.AsNoTracking()
                .Where(c => c.OwningTopicId == topicId && c.DeletedAtUtc == null)
                .Select(c => c.AuthorId)
                .Distinct()
                .Join(database.UserProfiles.AsNoTracking(),
                    authorId => authorId, profile => profile.Id,
                    (authorId, profile) => new { profile.Id, profile.DisplayName })
                .OrderBy(x => x.DisplayName)
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, HttpContext.RequestAborted),
        });
    }
}
