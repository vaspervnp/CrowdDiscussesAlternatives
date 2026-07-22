using CDA.Application.Abstractions;
using CDA.Domain.Users;
using CDA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Identity;

public sealed record RegistrationResult(bool Succeeded, Guid UserId, IReadOnlyList<string> Errors)
{
    public static RegistrationResult Success(Guid userId) => new(true, userId, []);

    public static RegistrationResult Failure(IEnumerable<string> errors) =>
        new(false, Guid.Empty, [.. errors]);
}

/// <summary>
/// Creates accounts, keeping the authentication record and the profile in step.
/// </summary>
public sealed class UserAccountService(
    UserManager<CdaUser> users,
    CdaDbContext database,
    IClock clock)
{
    /// <summary>
    /// Registers a new participant.
    /// </summary>
    /// <remarks>
    /// The Identity user and the profile are written in one transaction. Without it a
    /// failure between the two — a duplicate display name, most likely — would leave an
    /// account that can sign in but has no profile, and every page that renders an author
    /// would have to defend against that. The transaction runs inside an execution
    /// strategy because the connection is configured to retry, and EF refuses to combine
    /// user-initiated transactions with retries otherwise.
    /// </remarks>
    public async Task<RegistrationResult> RegisterAsync(
        string email,
        string displayName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var strategy = database.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            var user = new CdaUser(email) { Email = email };

            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return RegistrationResult.Failure(created.Errors.Select(error => error.Description));
            }

            database.UserProfiles.Add(new UserProfile(user.Id, displayName, clock.UtcNow));

            try
            {
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (IsDuplicateDisplayName(displayName))
            {
                await transaction.RollbackAsync(cancellationToken);
                return RegistrationResult.Failure([$"The display name '{displayName}' is already taken."]);
            }

            await transaction.CommitAsync(cancellationToken);
            return RegistrationResult.Success(user.Id);
        });

        // Checked lazily so the happy path costs nothing.
        bool IsDuplicateDisplayName(string name) =>
            database.UserProfiles.AsNoTracking().Any(profile => profile.DisplayName == name);
    }
}
