namespace PeakMetrics.Web.Models;

public sealed class StrategicGoal
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Perspective { get; set; } = string.Empty;

    /// <summary>Not Started | In Progress | Completed | Cancelled</summary>
    public string Status { get; set; } = "Not Started";

    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? OwnerUserId { get; set; }

    public bool IsArchived { get; set; } = false;

    // Navigation
    public AppUser? Owner { get; set; }
}
