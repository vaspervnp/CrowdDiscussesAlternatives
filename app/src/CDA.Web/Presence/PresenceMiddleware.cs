using CDA.Infrastructure.Identity;
using CDA.Web.Security;

namespace CDA.Web.Presence;

/// <summary>
/// Records that a signed-in user was active, so the platform can show who is online.
/// </summary>
public sealed class PresenceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PresenceTracker tracker)
    {
        // Run after the response so presence never delays a page, and never fails one:
        // a user's "last seen" is not worth a 500.
        await next(context);

        if (context.User.GetUserId() is not { } userId)
        {
            return;
        }

        try
        {
            await tracker.RecordActivityAsync(userId, context.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            // The client went away mid-request; nothing to record and nothing wrong.
        }
    }
}

public static class PresenceMiddlewareExtensions
{
    public static IApplicationBuilder UsePresenceTracking(this IApplicationBuilder app) =>
        app.UseMiddleware<PresenceMiddleware>();
}
