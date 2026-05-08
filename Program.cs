using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PeakMetrics.Web.Data;
using PeakMetrics.Web.Models;
using PeakMetrics.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Forwarded headers (reverse proxy / shared hosting support) ───────────────
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust all proxies on shared hosting (MonsterASP sits behind IIS/nginx)
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── MVC + Anti-forgery ────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly  = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // Lax (not Strict) so the cookie is sent on same-site POST navigations
    // and works correctly behind reverse proxies on shared hosting.
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ── Database ──────────────────────────────────────────────────────────────────
// Use LocalConnection for local development, DefaultConnection for production.
var isProduction = !builder.Environment.IsDevelopment();
var connectionString =
    Environment.GetEnvironmentVariable("PEAKMETRICS_CONNECTION_STRING")
    ?? (isProduction
        ? builder.Configuration.GetConnectionString("DefaultConnection")
        : builder.Configuration.GetConnectionString("LocalConnection")
          ?? builder.Configuration.GetConnectionString("DefaultConnection"))
    ?? throw new InvalidOperationException(
        "No connection string found. Set the PEAKMETRICS_CONNECTION_STRING environment variable.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sql => sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)
    ));

// ── Email ─────────────────────────────────────────────────────────────────────
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// ── Markdown ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IMarkdownService, MarkdownService>();

// ── Session ───────────────────────────────────────────────────────────────────
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // Lax so session cookie is sent on same-site navigations behind reverse proxy
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var app = builder.Build();

// ── Auto-apply migrations on startup ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed at startup. The application will continue, but the schema may be out of date.");
    }
}

// ── Error handling ────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// ── Security headers (must come before UseAuthentication) ────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options",  "nosniff");
    context.Response.Headers.Append("X-Frame-Options",         "DENY");
    context.Response.Headers.Append("X-XSS-Protection",        "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy",         "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy",      "geolocation=(), microphone=(), camera=()");
    await next();
});

// ── HTTPS redirection (before authentication) ─────────────────────────────────
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseStatusCodePagesWithReExecute("/Home/AccessDenied");
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Landing}/{action=Index}/{id?}");

app.MapControllers(); // enables [ApiController] attribute routing for ApiController

app.Run();
