using System.Security.Cryptography;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PeakMetrics.Web.Services;

/// <summary>MailKit-based implementation of <see cref="IEmailService"/>.</summary>
public sealed class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    // ── Public interface ──────────────────────────────────────────────────────

    public async Task SendVerificationEmailAsync(
        string toEmail, string toName, int userId, string token, string baseUrl, CancellationToken ct = default)
    {
        var confirmUrl = $"{baseUrl}/Account/ConfirmEmail?userId={userId}&token={Uri.EscapeDataString(token)}";

        var body = $@"
<div style=""font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.08);"">
  <div style=""background:linear-gradient(135deg,#2563eb,#1d4ed8);padding:36px 40px;text-align:center;"">
    <h1 style=""color:#fff;margin:0;font-size:1.8rem;font-weight:800;"">PeakMetrics</h1>
  </div>
  <div style=""padding:40px;"">
    <h2 style=""color:#0f172a;margin-top:0;"">Verify your email address</h2>
    <p style=""color:#475569;line-height:1.6;"">
      Welcome to PeakMetrics! Please verify your email by clicking the button below.
      After verification, an administrator will review your account before you can access the system.
    </p>
    <div style=""text-align:center;margin:32px 0;"">
      <a href=""{confirmUrl}""
         style=""background:#2563eb;color:#fff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:700;font-size:1rem;display:inline-block;"">
        Verify Email Address
      </a>
    </div>
    <p style=""color:#94a3b8;font-size:0.85rem;"">
      If you did not create an account, you can safely ignore this email.
    </p>
  </div>
  <div style=""background:#f8fafc;padding:20px 40px;text-align:center;"">
    <p style=""color:#94a3b8;font-size:0.8rem;margin:0;"">
      &copy; {DateTime.UtcNow.Year} PeakMetrics. All rights reserved.
    </p>
  </div>
</div>";

        await SendAsync(toEmail, toName, "Verify your PeakMetrics email address", body, ct);
    }

    public async Task SendAdminNewUserNotificationAsync(
        string applicantName, string applicantEmail, IEnumerable<string> adminEmails, string baseUrl, CancellationToken ct = default)
    {
        var body = $@"
<div style=""font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.08);"">
  <div style=""background:linear-gradient(135deg,#2563eb,#1d4ed8);padding:36px 40px;text-align:center;"">
    <h1 style=""color:#fff;margin:0;font-size:1.8rem;font-weight:800;"">PeakMetrics</h1>
  </div>
  <div style=""padding:40px;"">
    <h2 style=""color:#0f172a;margin-top:0;"">New account awaiting approval</h2>
    <p style=""color:#475569;line-height:1.6;"">
      A new user <strong>{applicantName}</strong> ({applicantEmail}) has verified their email
      and is waiting for account approval. Log in to approve or reject their account.
    </p>
    <div style=""text-align:center;margin:32px 0;"">
      <a href=""{baseUrl}/Home/PendingUsers""
         style=""background:#2563eb;color:#fff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:700;font-size:1rem;display:inline-block;"">
        Review Pending Users
      </a>
    </div>
  </div>
  <div style=""background:#f8fafc;padding:20px 40px;text-align:center;"">
    <p style=""color:#94a3b8;font-size:0.8rem;margin:0;"">
      &copy; {DateTime.UtcNow.Year} PeakMetrics. All rights reserved.
    </p>
  </div>
</div>";

        foreach (var adminEmail in adminEmails)
        {
            await SendAsync(adminEmail, "Administrator", "Action required: New user awaiting approval", body, ct);
        }
    }

    public async Task SendApprovalEmailAsync(
        string toEmail, string toName, string loginUrl, CancellationToken ct = default)
    {
        var body = $@"
<div style=""font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.08);"">
  <div style=""background:linear-gradient(135deg,#2563eb,#1d4ed8);padding:36px 40px;text-align:center;"">
    <h1 style=""color:#fff;margin:0;font-size:1.8rem;font-weight:800;"">PeakMetrics</h1>
  </div>
  <div style=""padding:40px;"">
    <h2 style=""color:#0f172a;margin-top:0;"">Your account has been approved! 🎉</h2>
    <p style=""color:#475569;line-height:1.6;"">
      Hi {toName}, your PeakMetrics account has been approved!
      You can now log in and start using the platform.
    </p>
    <div style=""text-align:center;margin:32px 0;"">
      <a href=""{loginUrl}""
         style=""background:#2563eb;color:#fff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:700;font-size:1rem;display:inline-block;"">
        Log In to PeakMetrics
      </a>
    </div>
  </div>
  <div style=""background:#f8fafc;padding:20px 40px;text-align:center;"">
    <p style=""color:#94a3b8;font-size:0.8rem;margin:0;"">
      &copy; {DateTime.UtcNow.Year} PeakMetrics. All rights reserved.
    </p>
  </div>
</div>";

        await SendAsync(toEmail, toName, "Your PeakMetrics account has been approved", body, ct);
    }

    public async Task SendRejectionEmailAsync(
        string toEmail, string toName, CancellationToken ct = default)
    {
        var body = $@"
<div style=""font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.08);"">
  <div style=""background:linear-gradient(135deg,#2563eb,#1d4ed8);padding:36px 40px;text-align:center;"">
    <h1 style=""color:#fff;margin:0;font-size:1.8rem;font-weight:800;"">PeakMetrics</h1>
  </div>
  <div style=""padding:40px;"">
    <h2 style=""color:#0f172a;margin-top:0;"">Account request update</h2>
    <p style=""color:#475569;line-height:1.6;"">
      Hi {toName}, your PeakMetrics account request has been reviewed and was not approved.
      Please contact your administrator for more information.
    </p>
  </div>
  <div style=""background:#f8fafc;padding:20px 40px;text-align:center;"">
    <p style=""color:#94a3b8;font-size:0.8rem;margin:0;"">
      &copy; {DateTime.UtcNow.Year} PeakMetrics. All rights reserved.
    </p>
  </div>
</div>";

        await SendAsync(toEmail, toName, "Your PeakMetrics account request", body, ct);
    }

    public async Task SendPasswordResetEmailAsync(
        string toEmail, string toName, int userId, string token, string baseUrl, CancellationToken ct = default)
    {
        var encodedToken = System.Net.WebUtility.UrlEncode(token);
        var resetLink = $"{baseUrl}/Account/ResetPassword?userId={userId}&token={encodedToken}";
        
        var body = $@"
<div style=""font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.08);"">
  <div style=""background:linear-gradient(135deg,#2563eb,#1d4ed8);padding:36px 40px;text-align:center;"">
    <h1 style=""color:#fff;margin:0;font-size:1.8rem;font-weight:800;"">PeakMetrics</h1>
  </div>
  <div style=""padding:40px;"">
    <h2 style=""color:#0f172a;margin-top:0;"">Password Reset Request</h2>
    <p style=""color:#475569;line-height:1.6;"">
      Hello {toName},
    </p>
    <p style=""color:#475569;line-height:1.6;"">
      We received a request to reset your password for your PeakMetrics account.
    </p>
    <p style=""color:#475569;line-height:1.6;"">
      Click the button below to reset your password:
    </p>
    <div style=""text-align:center;margin:32px 0;"">
      <a href=""{resetLink}""
         style=""background:#2563eb;color:#fff;padding:14px 32px;border-radius:8px;text-decoration:none;font-weight:700;font-size:1rem;display:inline-block;"">
        Reset Password
      </a>
    </div>
    <p style=""color:#475569;line-height:1.6;font-size:0.9rem;"">
      Or copy and paste this link into your browser:
    </p>
    <p style=""word-break:break-all;color:#64748b;font-size:0.85rem;background:#f8fafc;padding:12px;border-radius:6px;"">
      {resetLink}
    </p>
    <p style=""color:#94a3b8;font-size:0.85rem;margin-top:24px;"">
      This link will expire in 24 hours.
    </p>
    <p style=""color:#94a3b8;font-size:0.85rem;"">
      If you did not request a password reset, please ignore this email. Your password will remain unchanged.
    </p>
  </div>
  <div style=""background:#f8fafc;padding:20px 40px;text-align:center;"">
    <p style=""color:#94a3b8;font-size:0.8rem;margin:0;"">
      &copy; {DateTime.UtcNow.Year} PeakMetrics. All rights reserved.
    </p>
  </div>
</div>";

        await SendAsync(toEmail, toName, "Reset your PeakMetrics password", body, ct);
    }

    public async Task SendKpiAlertEmailAsync(
        string toEmail, string toName, string kpiName, string status,
        string period, string department, CancellationToken ct = default)
    {
        var statusColor = status == "Behind" ? "#dc2626" : "#d97706";
        var statusBg    = status == "Behind" ? "#fef2f2" : "#fffbeb";
        var emoji       = status == "Behind" ? "🔴" : "🟡";

        var body = $@"
<div style=""font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.08);"">
  <div style=""background:linear-gradient(135deg,#2563eb,#1d4ed8);padding:36px 40px;text-align:center;"">
    <h1 style=""color:#fff;margin:0;font-size:1.8rem;font-weight:800;"">PeakMetrics</h1>
    <p style=""color:#bfdbfe;margin:8px 0 0;font-size:0.95rem;"">KPI Performance Alert</p>
  </div>
  <div style=""padding:40px;"">
    <div style=""background:{statusBg};border-left:4px solid {statusColor};border-radius:6px;padding:16px 20px;margin-bottom:24px;"">
      <p style=""margin:0;font-size:1.1rem;font-weight:700;color:{statusColor};"">
        {emoji} {kpiName} is <strong>{status}</strong>
      </p>
    </div>
    <p style=""color:#475569;line-height:1.6;"">Hi {toName},</p>
    <p style=""color:#475569;line-height:1.6;"">
      A KPI in the <strong>{department}</strong> department requires your attention.
    </p>
    <table style=""width:100%;border-collapse:collapse;margin:20px 0;"">
      <tr style=""background:#f8fafc;"">
        <td style=""padding:10px 14px;font-weight:600;color:#374151;border:1px solid #e5e7eb;width:40%;"">KPI</td>
        <td style=""padding:10px 14px;color:#374151;border:1px solid #e5e7eb;"">{kpiName}</td>
      </tr>
      <tr>
        <td style=""padding:10px 14px;font-weight:600;color:#374151;border:1px solid #e5e7eb;"">Department</td>
        <td style=""padding:10px 14px;color:#374151;border:1px solid #e5e7eb;"">{department}</td>
      </tr>
      <tr style=""background:#f8fafc;"">
        <td style=""padding:10px 14px;font-weight:600;color:#374151;border:1px solid #e5e7eb;"">Period</td>
        <td style=""padding:10px 14px;color:#374151;border:1px solid #e5e7eb;"">{period}</td>
      </tr>
      <tr>
        <td style=""padding:10px 14px;font-weight:600;color:#374151;border:1px solid #e5e7eb;"">Status</td>
        <td style=""padding:10px 14px;font-weight:700;color:{statusColor};border:1px solid #e5e7eb;"">{status}</td>
      </tr>
    </table>
    <p style=""color:#475569;line-height:1.6;"">
      Please log in to PeakMetrics to review this KPI and take appropriate action.
    </p>
  </div>
  <div style=""background:#f8fafc;padding:20px 40px;text-align:center;"">
    <p style=""color:#94a3b8;font-size:0.8rem;margin:0;"">
      &copy; {DateTime.UtcNow.Year} PeakMetrics. All rights reserved.
    </p>
  </div>
</div>";

        var subject = $"{emoji} KPI Alert: {kpiName} is {status} — {period}";
        await SendAsync(toEmail, toName, subject, body, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task SendAsync(
        string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            Console.WriteLine($"[EmailService.SendAsync] Starting email send");
            Console.WriteLine($"[EmailService.SendAsync] To: {toEmail} ({toName})");
            Console.WriteLine($"[EmailService.SendAsync] Subject: {subject}");
            
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body    = new TextPart("html") { Text = htmlBody };
            
            Console.WriteLine($"[EmailService.SendAsync] MimeMessage created successfully");

            using var client = new SmtpClient();
            
            Console.WriteLine($"[EmailService.SendAsync] SmtpClient created");
            Console.WriteLine($"[EmailService.SendAsync] SMTP Host: {_settings.SmtpHost}");
            Console.WriteLine($"[EmailService.SendAsync] SMTP Port: {_settings.SmtpPort}");
            Console.WriteLine($"[EmailService.SendAsync] From: {_settings.FromEmail} ({_settings.FromName})");

            // Try STARTTLS on port 587 first, fall back to SSL on port 465
            try
            {
                Console.WriteLine($"[EmailService.SendAsync] Attempting to connect with STARTTLS on port {_settings.SmtpPort}...");
                await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls, ct);
                Console.WriteLine($"[EmailService.SendAsync] ✓ Connected successfully with STARTTLS");
            }
            catch (Exception connectEx)
            {
                // Port 587 may be blocked by the host — try SSL on 465
                Console.WriteLine($"[EmailService.SendAsync] × STARTTLS failed: {connectEx.GetType().Name} - {connectEx.Message}");
                
                if (!client.IsConnected)
                {
                    Console.WriteLine($"[EmailService.SendAsync] Attempting fallback: SSL on port 465...");
                    try
                    {
                        await client.ConnectAsync(_settings.SmtpHost, 465, SecureSocketOptions.SslOnConnect, ct);
                        Console.WriteLine($"[EmailService.SendAsync] ✓ Connected successfully with SSL on port 465");
                    }
                    catch (Exception fallbackEx)
                    {
                        Console.WriteLine($"[EmailService.SendAsync] × SSL fallback also failed: {fallbackEx.GetType().Name} - {fallbackEx.Message}");
                        throw;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_settings.SmtpUser))
            {
                Console.WriteLine($"[EmailService.SendAsync] Authenticating as: {_settings.SmtpUser}");
                try
                {
                    await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass, ct);
                    Console.WriteLine($"[EmailService.SendAsync] ✓ Authentication successful");
                }
                catch (Exception authEx)
                {
                    Console.WriteLine($"[EmailService.SendAsync] × Authentication failed: {authEx.GetType().Name}");
                    Console.WriteLine($"[EmailService.SendAsync] Message: {authEx.Message}");
                    throw;
                }
            }
            else
            {
                Console.WriteLine($"[EmailService.SendAsync] No authentication required (SmtpUser is empty)");
            }

            Console.WriteLine($"[EmailService.SendAsync] Sending message...");
            await client.SendAsync(message, ct);
            Console.WriteLine($"[EmailService.SendAsync] ✓ Message sent successfully");
            
            Console.WriteLine($"[EmailService.SendAsync] Disconnecting...");
            await client.DisconnectAsync(true, ct);
            Console.WriteLine($"[EmailService.SendAsync] ✓ Disconnected");

            _logger.LogInformation("✓ Email sent successfully to {ToEmail} with subject '{Subject}'", toEmail, subject);
        }
        catch (System.Net.Sockets.SocketException sockEx)
        {
            Console.WriteLine($"[EmailService.SendAsync-ERROR] SocketException (network/host connection issue)");
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Message: {sockEx.Message}");
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Inner Exception: {sockEx.InnerException?.Message}");
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Stack Trace: {sockEx.StackTrace}");
            _logger.LogError(sockEx, "SocketException when sending email to {ToEmail}", toEmail);
            throw;
        }
        catch (MailKit.Net.Smtp.SmtpCommandException smtpCmdEx)
        {
            Console.WriteLine($"[EmailService.SendAsync-ERROR] SmtpCommandException (SMTP server rejected command)");
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Status Code: {smtpCmdEx.StatusCode}");
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Message: {smtpCmdEx.Message}");
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Stack Trace: {smtpCmdEx.StackTrace}");
            _logger.LogError(smtpCmdEx, "SMTP command failed when sending email to {ToEmail}", toEmail);
            throw;
        }
        catch (MailKit.Net.Smtp.SmtpProtocolException smtpProtoEx)
        {
            Console.WriteLine($"[EmailService.SendAsync-ERROR] SmtpProtocolException (SMTP protocol error)");
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Message: {smtpProtoEx.Message}");
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Stack Trace: {smtpProtoEx.StackTrace}");
            _logger.LogError(smtpProtoEx, "SMTP protocol error when sending email to {ToEmail}", toEmail);
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Unexpected exception: {ex.GetType().Name}");
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Message: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"[EmailService.SendAsync-ERROR] Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            Console.WriteLine($"[EmailService.SendAsync-ERROR] Stack Trace: {ex.StackTrace}");
            _logger.LogError(ex, "Failed to send email to {ToEmail} with subject '{Subject}'", toEmail, subject);
            throw;
        }
    }

    /// <summary>Generates a URL-safe, cryptographically random token.</summary>
    public static string GenerateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
