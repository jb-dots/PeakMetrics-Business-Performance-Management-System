namespace PeakMetrics.Web.Services;

/// <summary>Transactional email operations for the registration pipeline.</summary>
public interface IEmailService
{
    /// <summary>Sends the email verification link to a newly registered applicant.</summary>
    Task SendVerificationEmailAsync(string toEmail, string toName, int userId, string token, string baseUrl, CancellationToken ct = default);

    /// <summary>Notifies all Admins/SuperAdmins that a new user has verified their email.</summary>
    Task SendAdminNewUserNotificationAsync(string applicantName, string applicantEmail, IEnumerable<string> adminEmails, string baseUrl, CancellationToken ct = default);

    /// <summary>Sends an approval confirmation email to the newly approved user.</summary>
    Task SendApprovalEmailAsync(string toEmail, string toName, string loginUrl, CancellationToken ct = default);

    /// <summary>Sends a rejection notification email to the rejected applicant.</summary>
    Task SendRejectionEmailAsync(string toEmail, string toName, CancellationToken ct = default);

    /// <summary>Sends a password reset email with a token link.</summary>
    Task SendPasswordResetEmailAsync(string toEmail, string toName, int userId, string token, string baseUrl, CancellationToken ct = default);

    /// <summary>
    /// Sends a KPI alert email when a KPI status changes to At Risk or Behind.
    /// Sent to managers, admins, and super admins who should be notified.
    /// </summary>
    Task SendKpiAlertEmailAsync(string toEmail, string toName, string kpiName, string status, string period, string department, CancellationToken ct = default);
}
