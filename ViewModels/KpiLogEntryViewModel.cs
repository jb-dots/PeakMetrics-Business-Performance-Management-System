using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.ViewModels;

public sealed class KpiLogEntryViewModel
{
    [Required(ErrorMessage = "Please select a KPI.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a KPI.")]
    public int KpiId { get; set; }

    [Required(ErrorMessage = "Actual value is required.")]
    [Range(0, 9999999, ErrorMessage = "Enter a valid number.")]
    public decimal ActualValue { get; set; }

    [Required(ErrorMessage = "Period is required.")]
    public string Period { get; set; } = string.Empty;

    [Required(ErrorMessage = "Date is required.")]
    public DateTime LoggedAt { get; set; } = DateTime.Today;

    public string? Notes { get; set; }
}

/// <summary>Used by the JSON endpoint to populate the KPI detail panel.</summary>
public sealed class KpiDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Perspective { get; set; } = string.Empty;
    public decimal Target { get; set; }
    public string Unit { get; set; } = string.Empty;
}
