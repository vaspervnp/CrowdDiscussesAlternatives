using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CDA.IntegrationTests;

/// <summary>
/// Boots the real web host, with the application's connection string pointed at the
/// disposable test database.
/// </summary>
public sealed class CdaWebApplicationFactory(DatabaseFixture database)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Cda"] = database.ConnectionString,
            }));
    }
}
