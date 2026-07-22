namespace CDA.IntegrationTests;

public class TestDatabaseGuardTests
{
    private const string Template =
        "Server=example.test;Port=3306;User ID=u;Password=p;SslMode=VerifyFull;Database=";

    [Theory]
    [InlineData("CrowdDiscussesAlternatives")]
    [InlineData("CrowdDiscussesAlternatives_test")] // casing matters — Linux schemas are case-sensitive
    [InlineData("Test_CrowdDiscussesAlternatives")]
    [InlineData("mysql")]
    public void Refuses_any_database_that_is_not_marked_disposable(string database)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TestDatabase.EnsureIsTestDatabase(Template + database));

        Assert.Contains(database, exception.Message);
    }

    [Fact]
    public void Refuses_a_connection_string_with_no_database()
    {
        Assert.Throws<InvalidOperationException>(
            () => TestDatabase.EnsureIsTestDatabase("Server=example.test;User ID=u;Password=p;"));
    }

    [Fact]
    public void Accepts_the_disposable_test_database()
    {
        var connectionString = Template + "CrowdDiscussesAlternatives_Test";

        Assert.Equal(connectionString, TestDatabase.EnsureIsTestDatabase(connectionString));
    }
}
