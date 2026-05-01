namespace PeakMetrics.Web.Models;

public sealed class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Alert | Info | Warning</summary>
    public string Type { get; set; } = "Info";

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? KpiId { get; set; }

    // Navigation
    public AppUser User { get; set; } = null!;
    public Kpi? Kpi { get; set; }
}
