using CDA.Infrastructure;
using CDA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CDA.IntegrationTests;

/// <summary>
/// Boots the real web host against the disposable test database.
/// </summary>
public sealed class CdaWebApplicationFactory(DatabaseFixture database)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // The DbContext registration is replaced outright rather than by overriding the
        // ConnectionStrings:Cda configuration value. Configuration override is not reliable
        // here: the host runs as Development and loads CDA.Web's own user secrets, which
        // hold the *development* connection string, and it won that race — the suite
        // silently wrote its fixtures into the development database. Replacing the
        // registration in ConfigureTestServices runs after all application registrations
        // and cannot be outvoted by a configuration source.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<CdaDbContext>>();
            services.RemoveAll<DbContextOptions>();

            services.AddDbContext<CdaDbContext>(options =>
                options.UseMySql(database.ConnectionString, DependencyInjection.ServerVersion));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Belt and braces, and the lesson of the bug above: verify where the *host* actually
        // ended up pointing, not merely what we asked for. Done here rather than in a helper
        // a test could forget to call.
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CdaDbContext>();
        TestDatabase.EnsureIsTestDatabase(context.Database.GetConnectionString()!);

        return host;
    }
}
