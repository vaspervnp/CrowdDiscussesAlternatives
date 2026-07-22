using CDA.Application.Abstractions;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CDA.Infrastructure.Identity;

/// <summary>
/// Keeps <see cref="Domain.Users.UserProfile.LastSeenAtUtc"/> roughly current so the
/// platform can show who is online.
/// </summary>
/// <remarks>
/// The naive version — write the timestamp on every request — turns each page view into a
/// database write, on a shared server with 151 connections. Instead a write happens at most
/// once per <see cref="WriteInterval"/> per user, tracked in memory. The cost is that
/// "last seen" can lag by up to that interval, which is invisible against the five-minute
/// window that defines being online.
/// </remarks>
public sealed class PresenceTracker(CdaDbContext database, IMemoryCache cache, IClock clock)
{
    /// <summary>Shortest gap between two presence writes for the same user.</summary>
    public static readonly TimeSpan WriteInterval = TimeSpan.FromMinutes(2);

    public async Task RecordActivityAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"presence:{userId}";

        if (cache.TryGetValue(cacheKey, out _))
        {
            return;
        }

        // Set the marker first: a failed write should not make every subsequent request
        // retry against a database that is evidently unhappy.
        cache.Set(cacheKey, true, WriteInterval);

        var now = clock.UtcNow;

        // Updated without loading the entity — this runs on every request path and the
        // profile is not otherwise needed.
        await database.UserProfiles
            .Where(profile => profile.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(profile => profile.LastSeenAtUtc, now),
                cancellationToken);
    }
}
