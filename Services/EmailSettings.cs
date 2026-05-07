namespace PeakMetrics.Web.Services;

/// <summary>SMTP configuration bound from appsettings.json → "EmailSettings".</summary>
public sealed class EmailSettings
{
    public string SmtpHost  { get; set; } = string.Empty;
    public int    SmtpPort  { get; set; } = 587;
    public string SmtpUser  { get; set; } = string.Empty;
    public string SmtpPass  { get; set; } = string.Empty;
    public string FromName  { get; set; } = "PeakMetrics";
    public string FromEmail { get; set; } = string.Empty;
}
