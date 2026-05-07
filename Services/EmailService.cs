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

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task SendAsync(
        string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body    = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();

            // Try STARTTLS on port 587 first, fall back to SSL on port 465
            try
            {
                await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls, ct);
            }
            catch
            {
                // Port 587 may be blocked by the host — try SSL on 465
                if (!client.IsConnected)
                    await client.ConnectAsync(_settings.SmtpHost, 465, SecureSocketOptions.SslOnConnect, ct);
            }

            if (!string.IsNullOrWhiteSpace(_settings.SmtpUser))
                await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent to {ToEmail} with subject '{Subject}'", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} with subject '{Subject}'", toEmail, subject);
            // Do not rethrow — email failure must not block the registration flow
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
