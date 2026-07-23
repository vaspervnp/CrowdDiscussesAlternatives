using CDA.Migrate;

namespace CDA.IntegrationTests;

/// <summary>
/// The migrator against a real database. The fixture has already brought the test schema fully up
/// to date, so these pin the "nothing to do" paths; the "apply a pending migration" path is EF
/// Core's own <c>MigrateAsync</c>, which the fixture itself relies on to exist.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MigrateTests(DatabaseFixture database)
{
    [Fact]
    public async Task Status_lists_the_applied_migrations_and_nothing_pending()
    {
        var status = await Migrator.StatusAsync(database.ConnectionString);

        Assert.NotEmpty(status.Applied);
        // The most recent migration at the time of writing; its presence proves the read reached
        // the real migrations history rather than an empty or stale view.
        Assert.Contains(status.Applied, migration => migration.EndsWith("_Localization"));
        Assert.Empty(status.Pending);
    }

    [Fact]
    public async Task Update_is_a_no_op_against_an_up_to_date_database()
    {
        var applied = await Migrator.UpdateAsync(database.ConnectionString);

        Assert.Empty(applied);
    }
}
