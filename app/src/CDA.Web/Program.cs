using CDA.Infrastructure;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Persistence;
using CDA.Web.Presence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// The database connection string is a secret, kept out of both the repository and the deployment
// folder. It is read from the user-secrets store — the same place `dotnet user-secrets set` uses
// in development, and the place the bundled `cda-configure` tool writes to on a target machine
// that has no .NET SDK. CreateBuilder only reads that store in Development, so it is added here
// for every environment. Environment variables are re-added last so an explicit
// ConnectionStrings__Cda still overrides the store, matching the deployment note in
// Documentation/devplan.md section 2.1.
builder.Configuration
    .AddUserSecrets(typeof(Program).Assembly, optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// MVC views and REST endpoints share one host, so they share one auth pipeline and
// need no CORS between them. See Documentation/devplan.md section 2.
// DataAnnotations validation messages resolve through the same DB-backed localizer that the
// views use, so a rejected form speaks the reader's language too.
builder.Services.AddControllersWithViews()
    .AddDataAnnotationsLocalization();
builder.Services.AddOpenApi();

// Emit non-Latin text as itself, not as &#x…; entities. The default HTML encoder escapes
// everything outside Basic Latin, which turns every Greek page into a wall of numeric character
// references — correct, but unreadable in the source and needlessly large. Allowing the full
// range is the right default for a platform whose whole point is a second alphabet.
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(
        System.Text.Unicode.UnicodeRanges.All));

// The languages the interface is offered in. English is the source (keys are English text);
// Greek ships translated and is editable through the admin screen.
var supportedCultures = new[] { "en-GB", "el-GR" };

builder.Services.Configure<RequestLocalizationOptions>(options => options
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

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

// Chooses the request's language, and with it how dates and numbers are formatted. The order
// the framework consults is query string, then the culture cookie, then the browser's
// Accept-Language — so a visitor's own language is honoured on first arrival, and a deliberate
// choice (which CultureController writes to the cookie) overrides it and sticks. The options
// are configured above, next to the supported-cultures list.
app.UseRequestLocalization();

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
