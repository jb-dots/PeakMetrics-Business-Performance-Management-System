namespace PeakMetrics.Web.ViewModels;

public sealed class AuditLogEntryViewModel
{
    public string Timestamp  { get; init; } = string.Empty;
    public string UserName   { get; init; } = string.Empty;
    public string Action     { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string Details    { get; init; } = string.Empty;
}
