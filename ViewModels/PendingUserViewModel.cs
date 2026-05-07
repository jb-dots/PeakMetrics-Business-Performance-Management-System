namespace PeakMetrics.Web.ViewModels;

public sealed class PendingUserViewModel
{
    public int    Id               { get; set; }
    public string FullName         { get; set; } = string.Empty;
    public string Email            { get; set; } = string.Empty;
    public string? RequestedRole   { get; set; }
    public string? DepartmentName  { get; set; }
    public DateTime RegistrationDate { get; set; }
}
