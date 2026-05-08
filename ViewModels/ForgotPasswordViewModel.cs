using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.ViewModels;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
    public string Email { get; set; } = string.Empty;
}
