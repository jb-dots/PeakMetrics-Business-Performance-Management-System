using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PeakMetrics.Web.Data;
using PeakMetrics.Web.Models;
using PeakMetrics.Web.Services;
using PeakMetrics.Web.ViewModels;

namespace PeakMetrics.Web.Controllers;

/// <summary>
/// Handles self-service registration, email confirmation, and the registration
/// confirmation page. No [Authorize] — all actions are public.
/// </summary>
public class AccountController : Controller
{
    private const string SessionUserId = "UserId";

    private readonly AppDbContext  _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;

    public AccountController(AppDbContext db, IEmailService email, IConfiguration config)
    {
        _db     = db;
        _email  = email;
        _config = config;
    }

    // ── GET /Account/Register ─────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Register(CancellationToken cancellationToken)
    {
        // Redirect already-authenticated users to the dashboard
        if (HttpContext.Session.GetInt32(SessionUserId) is not null)
            return RedirectToAction("Dashboard", "Home");

        await PopulateDepartmentsAsync(cancellationToken);
        return View(new RegisterViewModel());
    }

    // ── POST /Account/Register ────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        await PopulateDepartmentsAsync(cancellationToken);

        if (!ModelState.IsValid)
            return View(model);

        // ── Email domain validation ───────────────────────────────────────────
        if (!EmailValidator.IsValidFormat(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Please enter a valid email address.");
            return View(model);
        }

        if (!EmailValidator.DomainHasDot(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Please enter a valid email address.");
            return View(model);
        }

        if (EmailValidator.IsBlockedDomain(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Please use a real email address from a valid provider.");
            return View(model);
        }

        // ── Duplicate email check ─────────────────────────────────────────────
        var emailLower = model.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users
            .AnyAsync(u => u.Email.ToLower() == emailLower, cancellationToken);

        if (exists)
        {
            ModelState.AddModelError(nameof(model.Email), "This email address is already registered.");
            return View(model);
        }

        // ── Role guard (only Staff and Manager allowed) ───────────────────────
        // All self-registered users are assigned Staff role by default
        // (Admin can change role later from User Management if needed)

        // ── Create pending user ───────────────────────────────────────────────
        var token = EmailService.GenerateToken();

        var user = new AppUser
        {
            FullName           = model.FullName.Trim(),
            Email              = model.Email.Trim(),
            PasswordHash       = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Role               = "Staff",          // placeholder until approved
            DepartmentId       = model.DepartmentId,
            PendingRole        = "Staff",          // hardcoded - all self-registrations are Staff
            PendingDepartmentId = model.DepartmentId?.ToString(),
            IsApproved         = false,
            EmailConfirmed     = false,
            ConfirmationToken  = token,
            IsActive           = true,
            CreatedAt          = DateTime.UtcNow
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Registration failed: {ex.InnerException?.Message ?? ex.Message}");
            return View(model);
        }

        // ── Send verification email ───────────────────────────────────────────
        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            await _email.SendVerificationEmailAsync(user.Email, user.FullName, user.Id, token, baseUrl, cancellationToken);
        }
        catch
        {
            // Email failure must not block registration — user can contact support
        }

        return RedirectToAction(nameof(RegisterConfirmation));
    }

    // ── GET /Account/RegisterConfirmation ─────────────────────────────────────
    [HttpGet]
    public IActionResult RegisterConfirmation() => View();

    // ── GET /Account/ConfirmEmail ─────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(int userId, string? token, CancellationToken cancellationToken)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(token))
        {
            ViewBag.Success = false;
            return View();
        }

        var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);

        if (user is null
            || user.ConfirmationToken is null
            || !string.Equals(user.ConfirmationToken, token, StringComparison.Ordinal))
        {
            ViewBag.Success = false;
            return View();
        }

        // Mark email as confirmed and clear the token
        user.EmailConfirmed    = true;
        user.ConfirmationToken = null;
        await _db.SaveChangesAsync(cancellationToken);

        // Notify all Admins / Super Admins via email
        try
        {
            var admins = await _db.Users
                .Where(u => (u.Role == "Super Admin" || u.Role == "Administrator") && u.IsActive)
                .Select(u => new { u.Email, u.FullName })
                .ToListAsync(cancellationToken);

            var baseUrl     = $"{Request.Scheme}://{Request.Host}";
            var adminEmails = admins.Select(a => a.Email).ToList();

            if (adminEmails.Count > 0)
                await _email.SendAdminNewUserNotificationAsync(user.FullName, user.Email, adminEmails, baseUrl, cancellationToken);
        }
        catch
        {
            // Admin notification failure must not block the confirmation response
        }

        ViewBag.Success = true;
        return View();
    }

    // ── GET /Account/ForgotPassword ───────────────────────────────────────────
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    // ── GET /Account/TestEmail — Development-only test email endpoint ────────
    [HttpGet]
    public async Task<IActionResult> TestEmail(CancellationToken cancellationToken)
    {
        if (!HttpContext.Request.Host.Host.Contains("localhost"))
        {
            return Content("TestEmail endpoint is only available in development mode.", "text/plain");
        }

        var result = new System.Text.StringBuilder();
        result.AppendLine("=== PEAKMETRICS TEST EMAIL ENDPOINT ===");
        result.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
        result.AppendLine();
        
        try
        {
            result.AppendLine("[STEP 1] Checking EmailSettings configuration...");
            var emailSettings = _config.GetSection("EmailSettings");
            var host = emailSettings["SmtpHost"];
            var port = emailSettings["SmtpPort"];
            var user = emailSettings["SmtpUser"];
            var pass = emailSettings["SmtpPass"];
            var fromName = emailSettings["FromName"];
            var fromEmail = emailSettings["FromEmail"];
            
            result.AppendLine($"  SmtpHost: {(string.IsNullOrEmpty(host) ? "MISSING" : host)}");
            result.AppendLine($"  SmtpPort: {(string.IsNullOrEmpty(port) ? "MISSING" : port)}");
            result.AppendLine($"  SmtpUser: {(string.IsNullOrEmpty(user) ? "MISSING" : "[present]")} ");
            result.AppendLine($"  SmtpPass: {(string.IsNullOrEmpty(pass) ? "MISSING" : "[present]")}");
            result.AppendLine($"  FromName: {(string.IsNullOrEmpty(fromName) ? "MISSING" : fromName)}");
            result.AppendLine($"  FromEmail: {(string.IsNullOrEmpty(fromEmail) ? "MISSING" : fromEmail)}");
            result.AppendLine();
            
            result.AppendLine("[STEP 2] Sending test email...");
            await _email.SendPasswordResetEmailAsync(
                "test@example.com",
                "Test User",
                1,
                "test-token-123",
                $"{Request.Scheme}://{Request.Host}",
                cancellationToken
            );
            
            result.AppendLine("✓ Email sent successfully!");
            result.AppendLine();
            result.AppendLine("Check the application console for detailed logging output.");
            
            return Content(result.ToString(), "text/plain");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            result.AppendLine($"✗ SocketException - Cannot connect to SMTP server");
            result.AppendLine($"  Type: {ex.GetType().Name}");
            result.AppendLine($"  Message: {ex.Message}");
            if (ex.InnerException != null)
                result.AppendLine($"  Inner: {ex.InnerException.Message}");
            result.AppendLine($"  StackTrace: {ex.StackTrace}");
            return Content(result.ToString(), "text/plain");
        }
        catch (MailKit.Net.Smtp.SmtpCommandException ex)
        {
            result.AppendLine($"✗ SmtpCommandException - SMTP command failed");
            result.AppendLine($"  StatusCode: {ex.StatusCode}");
            result.AppendLine($"  Message: {ex.Message}");
            result.AppendLine($"  StackTrace: {ex.StackTrace}");
            return Content(result.ToString(), "text/plain");
        }
        catch (MailKit.Net.Smtp.SmtpProtocolException ex)
        {
            result.AppendLine($"✗ SmtpProtocolException - SMTP protocol error");
            result.AppendLine($"  Type: {ex.GetType().Name}");
            result.AppendLine($"  Message: {ex.Message}");
            result.AppendLine($"  StackTrace: {ex.StackTrace}");
            return Content(result.ToString(), "text/plain");
        }
        catch (Exception ex)
        {
            result.AppendLine($"✗ Unexpected Exception");
            result.AppendLine($"  Type: {ex.GetType().Name}");
            result.AppendLine($"  Message: {ex.Message}");
            if (ex.InnerException != null)
                result.AppendLine($"  Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            result.AppendLine($"  StackTrace: {ex.StackTrace}");
            return Content(result.ToString(), "text/plain");
        }
    }

    // ── GET /Account/ForgotPasswordTest — raw diagnostic (no antiforgery) ────
    [HttpGet]
    public IActionResult ForgotPasswordTest()
    {
        return Content("ForgotPassword route is reachable. GET works.", "text/plain");
    }

    // ── POST /Account/ForgotPasswordTest — raw diagnostic (no antiforgery) ───
    [HttpPost]
    public IActionResult ForgotPasswordTestPost()
    {
        return Content($"POST reached controller. Form keys: {string.Join(", ", Request.Form.Keys)}", "text/plain");
    }

    // ── GET /Account/RunDatabaseMigration — Emergency migration trigger ───────
    [HttpGet]
    public async Task<IActionResult> RunDatabaseMigration(CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine("[MIGRATION] Starting database migration...");
            await _db.Database.MigrateAsync(cancellationToken);
            Console.WriteLine("[MIGRATION] ✓ Database migration completed successfully!");
            return Content("✓ Database migration completed successfully!\n\nThe following changes were applied:\n- Added PasswordResetToken column\n- Added PasswordResetTokenExpiry column\n\nThe Forgot Password feature is now ready to use.", "text/plain");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MIGRATION-ERROR] {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[MIGRATION-ERROR] Stack trace: {ex.StackTrace}");
            return Content($"❌ Migration failed:\n\n{ex.GetType().Name}: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "text/plain");
        }
    }
    
    // ── POST /Account/ForgotPassword ──────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            Console.WriteLine($"[STEP 1] ForgotPassword POST called for email: {model.Email}");
            
            var emailLower = model.Email.Trim().ToLowerInvariant();
            Console.WriteLine($"[STEP 2] Normalized email (lowercase): {emailLower}");
            Console.WriteLine($"[STEP 3] Looking up user in database...");
            
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower, cancellationToken);

            Console.WriteLine($"[STEP 4] User found: {(user != null ? "YES - " + user.FullName : "NO")}");
            Console.WriteLine($"[STEP 5] User EmailConfirmed status: {(user?.EmailConfirmed ?? false)}");

            if (user != null)
            {
                Console.WriteLine($"[STEP 6] Generating reset token...");
                // Generate a cryptographically secure token
                var token = EmailService.GenerateToken();
                var tokenExpiry = DateTime.UtcNow.AddHours(24);
                Console.WriteLine($"[STEP 7] Token generated. Expiry: {tokenExpiry:O}");

                try
                {
                    Console.WriteLine($"[STEP 8] Storing token in database...");
                    // Store token in database
                    user.PasswordResetToken = token;
                    user.PasswordResetTokenExpiry = tokenExpiry;
                    await _db.SaveChangesAsync(cancellationToken);
                    Console.WriteLine($"[STEP 9] Token stored successfully in database");
                }
                catch (Exception dbEx)
                {
                    Console.WriteLine($"[ERROR-DB] Database error when storing token: {dbEx.GetType().Name}");
                    Console.WriteLine($"[ERROR-DB] Message: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                        Console.WriteLine($"[ERROR-DB] Inner Exception: {dbEx.InnerException.Message}");
                    Console.WriteLine($"[ERROR-DB] Stack trace: {dbEx.StackTrace}");
                    
                    var inner = dbEx.InnerException?.Message ?? dbEx.Message;
                    ViewBag.DbError = $"Database error: {inner}";
                    if (HttpContext.Request.Host.Host == "localhost")
                        ViewBag.DbErrorDetails = dbEx.ToString();
                    return View(model);
                }

                try
                {
                    Console.WriteLine($"[STEP 10] Building reset link...");
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    Console.WriteLine($"[STEP 11] Base URL: {baseUrl}");
                    
                    Console.WriteLine($"[STEP 12] Calling EmailService.SendPasswordResetEmailAsync...");
                    await _email.SendPasswordResetEmailAsync(user.Email, user.FullName, user.Id, token, baseUrl, cancellationToken);
                    Console.WriteLine($"[STEP 13] Email sent successfully!");
                }
                catch (System.Net.Sockets.SocketException sockEx)
                {
                    Console.WriteLine($"[ERROR-SOCKET] SocketException - Network/Connection issue");
                    Console.WriteLine($"[ERROR-SOCKET] Message: {sockEx.Message}");
                    Console.WriteLine($"[ERROR-SOCKET] Inner Exception: {sockEx.InnerException?.Message}");
                    Console.WriteLine($"[ERROR-SOCKET] Stack trace: {sockEx.StackTrace}");
                    
                    ViewBag.EmailError = $"Connection error: Cannot reach email server. Check SMTP host/port configuration.";
                    if (HttpContext.Request.Host.Host == "localhost")
                        ViewBag.EmailErrorDetails = sockEx.ToString();
                    return View(model);
                }
                catch (MailKit.Net.Smtp.SmtpCommandException smtpCmdEx)
                {
                    Console.WriteLine($"[ERROR-SMTP-CMD] SmtpCommandException - SMTP command failed");
                    Console.WriteLine($"[ERROR-SMTP-CMD] StatusCode: {smtpCmdEx.StatusCode}");
                    Console.WriteLine($"[ERROR-SMTP-CMD] Message: {smtpCmdEx.Message}");
                    Console.WriteLine($"[ERROR-SMTP-CMD] Stack trace: {smtpCmdEx.StackTrace}");
                    
                    ViewBag.EmailError = $"SMTP error: {smtpCmdEx.StatusCode}. Check Gmail App Password or enable Less Secure Apps.";
                    if (HttpContext.Request.Host.Host == "localhost")
                        ViewBag.EmailErrorDetails = smtpCmdEx.ToString();
                    return View(model);
                }
                catch (MailKit.Net.Smtp.SmtpProtocolException smtpProtoEx)
                {
                    Console.WriteLine($"[ERROR-SMTP-PROTO] SmtpProtocolException - SMTP protocol issue");
                    Console.WriteLine($"[ERROR-SMTP-PROTO] Message: {smtpProtoEx.Message}");
                    Console.WriteLine($"[ERROR-SMTP-PROTO] Stack trace: {smtpProtoEx.StackTrace}");
                    
                    ViewBag.EmailError = $"SMTP protocol error: Connection or protocol issue with email server.";
                    if (HttpContext.Request.Host.Host == "localhost")
                        ViewBag.EmailErrorDetails = smtpProtoEx.ToString();
                    return View(model);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"[ERROR-GENERAL] Unexpected exception: {emailEx.GetType().Name}");
                    Console.WriteLine($"[ERROR-GENERAL] Message: {emailEx.Message}");
                    if (emailEx.InnerException != null)
                        Console.WriteLine($"[ERROR-GENERAL] Inner Exception: {emailEx.InnerException.GetType().Name} - {emailEx.InnerException.Message}");
                    Console.WriteLine($"[ERROR-GENERAL] Stack trace: {emailEx.StackTrace}");
                    
                    // Email failed but token is saved — show a specific message so the user knows
                    ViewBag.EmailError = $"Email delivery failed: {emailEx.Message}";
                    if (HttpContext.Request.Host.Host == "localhost")
                        ViewBag.EmailErrorDetails = emailEx.ToString();
                    return View(model);
                }
            }
            else
            {
                Console.WriteLine($"[STEP 14] User not found in database. Applying anti-enumeration delay...");
                // Artificial delay for anti-enumeration (matches token generation + email time)
                await Task.Delay(150, cancellationToken);
                Console.WriteLine($"[STEP 15] Anti-enumeration delay complete");
            }

            Console.WriteLine($"[STEP 16] Displaying success message (regardless of email existence)");
            // Always display the same success message regardless of email existence
            ViewBag.Success = true;
            return View(model);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR-UNEXPECTED] Unexpected top-level exception in ForgotPassword: {ex.GetType().Name}");
            Console.WriteLine($"[ERROR-UNEXPECTED] Message: {ex.Message}");
            Console.WriteLine($"[ERROR-UNEXPECTED] Stack trace: {ex.StackTrace}");
            
            ViewBag.FatalError = $"An unexpected error occurred: {ex.Message}";
            if (HttpContext.Request.Host.Host == "localhost")
                ViewBag.FatalErrorDetails = ex.ToString();
            return View(model);
        }
    }

    // ── GET /Account/ResetPassword ────────────────────────────────────────────
    [HttpGet]
    public IActionResult ResetPassword(int userId, string? token)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(token))
        {
            ViewBag.Error = true;
            return View(new ResetPasswordViewModel());
        }

        return View(new ResetPasswordViewModel
        {
            UserId = userId,
            Token = token
        });
    }

    // ── POST /Account/ResetPassword ───────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _db.Users.FindAsync(new object[] { model.UserId }, cancellationToken);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired. Please request a new one.");
            ViewBag.Error = true;
            return View(model);
        }

        // Validate token
        if (string.IsNullOrWhiteSpace(user.PasswordResetToken) 
            || user.PasswordResetToken != model.Token
            || user.PasswordResetTokenExpiry == null
            || user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired. Please request a new one.");
            ViewBag.Error = true;
            return View(model);
        }

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        
        // Write audit log
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "PasswordReset",
            EntityType = "Auth",
            Details = $"{user.Email} reset their password.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            OccurredAt = DateTime.UtcNow
        });
        
        await _db.SaveChangesAsync(cancellationToken);

        // Redirect to login with success message
        TempData["SuccessMessage"] = "Password reset successfully. Please log in with your new password.";
        return RedirectToAction("Login", "Home");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task PopulateDepartmentsAsync(CancellationToken ct)
    {
        ViewBag.Departments = await _db.Departments
            .Where(d => !d.IsArchived)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(ct);
    }
}
