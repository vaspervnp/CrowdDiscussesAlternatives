using CDA.Infrastructure;
using CDA.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// MVC views and REST endpoints share one host, so they share one auth pipeline and
// need no CORS between them. See Documentation/devplan.md section 2.
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

// Errors leave the API as RFC 9457 problem details rather than as HTML or a bare status.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CdaDbContext>("database");

var app = builder.Build();

app.UseSerilogRequestLogging();

static bool IsApiRequest(HttpContext context) =>
    context.Request.Path.StartsWithSegments("/api");

// API failures answer with problem details; page failures render the MVC error view.
// In development the pages fall through to the developer exception page instead.
app.UseWhen(IsApiRequest, api => api.UseExceptionHandler());

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseWhen(context => !IsApiRequest(context), web => web.UseExceptionHandler("/Home/Error"));
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// Reports process liveness *and* that the database is actually reachable — the whole
// point of the Phase 0 exit criterion.
app.MapHealthChecks("/health");

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

/// <summary>Exposed so integration tests can drive the real host via WebApplicationFactory.</summary>
public partial class Program;
