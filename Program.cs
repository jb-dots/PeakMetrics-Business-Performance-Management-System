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

// ── PDF Report ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<PdfReportService>();

// ── Email Configuration Validation (Startup Check) ──────────────────────────
{
    var emailSettings = builder.Configuration.GetSection("EmailSettings");
    Console.WriteLine("═══ EMAIL SETTINGS VALIDATION ═══");
    Console.WriteLine($"✓ SmtpHost: {(string.IsNullOrEmpty(emailSettings["SmtpHost"]) ? "❌ MISSING" : emailSettings["SmtpHost"])}");
    Console.WriteLine($"✓ SmtpPort: {(string.IsNullOrEmpty(emailSettings["SmtpPort"]) ? "❌ MISSING" : emailSettings["SmtpPort"])}");
    Console.WriteLine($"✓ SmtpUser: {(string.IsNullOrEmpty(emailSettings["SmtpUser"]) ? "❌ MISSING" : "✓ [present]")}");
    Console.WriteLine($"✓ SmtpPass: {(string.IsNullOrEmpty(emailSettings["SmtpPass"]) ? "❌ MISSING" : "✓ [present]")}");
    Console.WriteLine($"✓ FromName: {(string.IsNullOrEmpty(emailSettings["FromName"]) ? "❌ MISSING" : emailSettings["FromName"])}");
    Console.WriteLine($"✓ FromEmail: {(string.IsNullOrEmpty(emailSettings["FromEmail"]) ? "❌ MISSING" : emailSettings["FromEmail"])}");
    Console.WriteLine("════════════════════════════════════");
    
    // Log warnings for missing fields
    if (string.IsNullOrEmpty(emailSettings["SmtpHost"]))
        Console.WriteLine("⚠️ WARNING: EmailSettings.SmtpHost is missing or empty. Email sending will fail.");
    if (string.IsNullOrEmpty(emailSettings["SmtpPort"]))
        Console.WriteLine("⚠️ WARNING: EmailSettings.SmtpPort is missing or empty. Email sending will fail.");
    if (string.IsNullOrEmpty(emailSettings["SmtpUser"]))
        Console.WriteLine("⚠️ WARNING: EmailSettings.SmtpUser is missing or empty. Authentication will be skipped.");
    if (string.IsNullOrEmpty(emailSettings["SmtpPass"]))
        Console.WriteLine("⚠️ WARNING: EmailSettings.SmtpPass is missing or empty. Authentication will fail.");
    if (string.IsNullOrEmpty(emailSettings["FromName"]))
        Console.WriteLine("⚠️ WARNING: EmailSettings.FromName is missing or empty. Emails will have no sender name.");
    if (string.IsNullOrEmpty(emailSettings["FromEmail"]))
        Console.WriteLine("⚠️ WARNING: EmailSettings.FromEmail is missing or empty. Email sending will fail.");
}

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

// ── Ensure password reset columns exist (safe ALTER TABLE fallback) ───────────
// Runs raw SQL so the columns are added even if EF migration history is missing.
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'[dbo].[Users]')
                  AND name = 'PasswordResetToken'
            )
            BEGIN
                ALTER TABLE [dbo].[Users]
                ADD [PasswordResetToken] nvarchar(max) NULL;
            END
        ");

        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'[dbo].[Users]')
                  AND name = 'PasswordResetTokenExpiry'
            )
            BEGIN
                ALTER TABLE [dbo].[Users]
                ADD [PasswordResetTokenExpiry] datetime2(7) NULL;
            END
        ");

        logger.LogInformation("Password reset columns verified/added successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure password reset columns exist.");
    }
}

// ── Ensure PhoneNumber column exists (safe ALTER TABLE fallback) ──────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'[dbo].[Users]')
                  AND name = 'PhoneNumber'
            )
            BEGIN
                ALTER TABLE [dbo].[Users]
                ADD [PhoneNumber] nvarchar(20) NULL;
            END
        ");
        logger.LogInformation("PhoneNumber column verified/added successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure PhoneNumber column exists.");
    }
}

// ── Sample data seeder ────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await SampleDataSeeder.SeedAsync(db, logger);
        logger.LogInformation("Sample data seeding completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Sample data seeding failed.");
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
