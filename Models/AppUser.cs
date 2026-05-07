using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.Models;

public sealed class AppUser
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, ErrorMessage = "Full name cannot exceed 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the password.</summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Staff"; // Super Admin | Administrator | Manager | Staff | Executive

    public int? DepartmentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Number of consecutive failed login attempts since last successful login.</summary>
    public int? FailedLoginAttempts { get; set; } = 0;

    /// <summary>UTC time until which the account is locked out. Null means not locked.</summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>Set to true by an Admin after reviewing the account.</summary>
    public bool IsApproved { get; set; } = false;

    /// <summary>UTC timestamp when the account was approved.</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Id (as string) of the Admin who approved this account.</summary>
    public string? ApprovedById { get; set; }

    /// <summary>Set to true after the user clicks the email confirmation link.</summary>
    public bool EmailConfirmed { get; set; } = false;

    /// <summary>Role requested at registration time. Assigned to Role on approval.</summary>
    public string? PendingRole { get; set; } = "Staff";

    /// <summary>DepartmentId (as string) requested at registration. Assigned on approval.</summary>
    public string? PendingDepartmentId { get; set; }

    /// <summary>One-time token for email verification. Cleared after use.</summary>
    public string? ConfirmationToken { get; set; }

    // Navigation
    public Department? Department { get; set; }
    public ICollection<KpiLogEntry> KpiLogEntries { get; set; } = new List<KpiLogEntry>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
