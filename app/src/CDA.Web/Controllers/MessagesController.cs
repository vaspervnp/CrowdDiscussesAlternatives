using CDA.Infrastructure.Messaging;
using CDA.Infrastructure.Persistence;
using CDA.Web.Models;
using CDA.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CDA.Web.Controllers;

[Route("messages")]
[Authorize]
public sealed class MessagesController(
    CdaDbContext database,
    MessageService messages) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = User.GetUserId()!.Value;

        return View(new ConversationListViewModel
        {
            Conversations = await messages.ConversationsAsync(userId, HttpContext.RequestAborted),
        });
    }

    [HttpGet("{withUserId:guid}")]
    public async Task<IActionResult> Conversation(Guid withUserId)
    {
        var userId = User.GetUserId()!.Value;

        var partner = await database.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == withUserId, HttpContext.RequestAborted);

        if (partner is null || partner.Id == userId)
        {
            return NotFound();
        }

        return View(new ConversationViewModel
        {
            WithUserId = withUserId,
            WithDisplayName = partner.DisplayName,
            // Reading the conversation marks the messages addressed to this reader as read.
            Messages = await messages.ConversationAsync(userId, withUserId, HttpContext.RequestAborted),
        });
    }

    [HttpPost("{withUserId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(Guid withUserId, string body)
    {
        var result = await messages.SendAsync(
            User.GetUserId()!.Value, withUserId, body ?? string.Empty, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Conversation), new { withUserId });
    }
}
