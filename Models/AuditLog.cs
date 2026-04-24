namespace PeakMetrics.Web.Models;

public sealed class AuditLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string Action { get; set; } = string.Empty; // e.g. "Updated KPI", "Logged Entry", "Login"

    public string EntityType { get; set; } = string.Empty; // e.g. "Kpi", "KpiLogEntry", "AppUser"

    public int? EntityId { get; set; }

    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AppUser? User { get; set; }
}
