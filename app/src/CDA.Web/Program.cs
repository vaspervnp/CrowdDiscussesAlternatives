using CDA.Infrastructure;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Persistence;
using CDA.Web.Presence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
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

builder.Services
    .AddIdentity<CdaUser, CdaRole>(DependencyInjection.ConfigureIdentity)
    .AddEntityFrameworkStores<CdaDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

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

// Without this the app formats dates and numbers in whatever culture the host machine
// happens to run, so the same page renders differently on a developer's laptop and on the
// server. Pinned to English for now; Phase 13 makes the culture a user choice and adds the
// translated strings to go with it.
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en-GB")
    .AddSupportedCultures("en-GB", "el-GR")
    .AddSupportedUICultures("en-GB", "el-GR"));

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// After authentication, so the signed-in user is known; before the endpoints, so it wraps
// the whole request and records activity once it completes.
app.UsePresenceTracking();

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
