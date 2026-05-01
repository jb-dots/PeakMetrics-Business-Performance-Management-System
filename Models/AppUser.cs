namespace PeakMetrics.Web.Models;

public sealed class AppUser
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Staff"; // Super Admin | Administrator | Manager | Staff | Executive

    public int? DepartmentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public Department? Department { get; set; }
    public ICollection<KpiLogEntry> KpiLogEntries { get; set; } = new List<KpiLogEntry>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
