namespace PeakMetrics.Web.Models;

public sealed class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Standard | Warning | Critical</summary>
    public string Severity { get; set; } = "Standard";

    public string Icon { get; set; } = "bi-info-circle";

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AppUser User { get; set; } = null!;
}
