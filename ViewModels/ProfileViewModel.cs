using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.ViewModels;

public sealed class ProfileViewModel
{
    // Display info (settable for re-render after POST)
    public string FullName       { get; set; } = string.Empty;
    public string Email          { get; set; } = string.Empty;
    public string Role           { get; set; } = string.Empty;
    public string Initials       { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? LastLoginAt   { get; set; }

    // Editable form fields
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(150)]
    public string NewFullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(256)]
    public string NewEmail { get; set; } = string.Empty;

    // Password change (all optional — only applied if CurrentPassword is filled)
    public string? CurrentPassword { get; set; }

    [MinLength(6, ErrorMessage = "New password must be at least 6 characters.")]
    public string? NewPassword { get; set; }

    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }
}
