using CDA.Application.Abstractions;
using CDA.Domain.Notifications;
using CDA.Infrastructure.Notifications;
using CDA.Web.Models;
using CDA.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDA.Web.Controllers;

[Route("notifications")]
[Authorize]
public sealed class NotificationsController(
    NotificationService notifications,
    IEmailSender email) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = User.GetUserId()!.Value;

        return View(new NotificationsViewModel
        {
            Notifications = await notifications.ForUserAsync(userId, false, HttpContext.RequestAborted),
            Delivery = await notifications.DeliveryFor(userId, HttpContext.RequestAborted),
            // The page says plainly when there is no mail host, rather than offering a choice
            // that would quietly do nothing.
            EmailWorks = email.CanDeliver,
        });
    }

    [HttpPost("read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        await notifications.MarkAllReadAsync(User.GetUserId()!.Value, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delivery")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDelivery(NotificationDelivery delivery)
    {
        await notifications.SetDeliveryAsync(
            User.GetUserId()!.Value, delivery, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Index));
    }
}
