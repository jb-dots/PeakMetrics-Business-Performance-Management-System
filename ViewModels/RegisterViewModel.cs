using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.ViewModels;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(80, ErrorMessage = "Full name cannot exceed 80 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }

    /// <summary>"Staff" or "Manager" only.</summary>
    public string PendingRole { get; set; } = "Staff";
}
