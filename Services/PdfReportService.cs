using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PeakMetrics.Web.ViewModels;

namespace PeakMetrics.Web.Services;

/// <summary>
/// Generates a PDF Executive Performance Report using QuestPDF.
/// </summary>
public sealed class PdfReportService
{
    // Status colours (RGB)
    private static readonly string ColorOnTrack = "#16a34a";
    private static readonly string ColorAtRisk  = "#d97706";
    private static readonly string ColorBehind  = "#dc2626";
    private static readonly string ColorNoData  = "#6b7280";
    private static readonly string ColorPrimary = "#1e40af";
    private static readonly string ColorLight   = "#f8fafc";
    private static readonly string ColorBorder  = "#e2e8f0";

    public byte[] GenerateExecutiveReport(ExecutiveReportingViewModel model)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeContent(c, model));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    // ── Header ────────────────────────────────────────────────────────────────
    private void ComposeHeader(IContainer container)
    {
        container
            .Background(ColorPrimary)
            .Padding(16)
            .Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("PeakMetrics")
                        .FontSize(18).Bold().FontColor(Colors.White);
                    col.Item().Text("Executive Performance Report")
                        .FontSize(11).FontColor("#bfdbfe");
                });

                row.ConstantItem(120).AlignRight().AlignMiddle()
                    .Text($"Generated: {DateTime.Now:MMM d, yyyy}")
                    .FontSize(8).FontColor("#93c5fd");
            });
    }

    // ── Footer ────────────────────────────────────────────────────────────────
    private void ComposeFooter(IContainer container)
    {
        container
            .BorderTop(1).BorderColor(ColorBorder)
            .PaddingTop(6)
            .Row(row =>
            {
                row.RelativeItem().Text("PeakMetrics — Confidential")
                    .FontSize(7).FontColor(ColorNoData);
                row.ConstantItem(60).AlignRight()
                    .Text(x =>
                    {
                        x.Span("Page ").FontSize(7).FontColor(ColorNoData);
                        x.CurrentPageNumber().FontSize(7).FontColor(ColorNoData);
                        x.Span(" of ").FontSize(7).FontColor(ColorNoData);
                        x.TotalPages().FontSize(7).FontColor(ColorNoData);
                    });
            });
    }

    // ── Content ───────────────────────────────────────────────────────────────
    private void ComposeContent(IContainer container, ExecutiveReportingViewModel model)
    {
        container.Column(col =>
        {
            col.Spacing(14);

            // Period label
            if (!string.IsNullOrEmpty(model.SelectedPeriod))
            {
                col.Item()
                    .Background(ColorLight).Border(1).BorderColor(ColorBorder)
                    .Padding(8)
                    .Text($"Reporting Period: {model.SelectedPeriod}")
                    .FontSize(9).Bold().FontColor(ColorPrimary);
            }

            // ── Summary cards ──────────────────────────────────────────────
            col.Item().Element(c => ComposeSummaryCards(c, model));

            // ── KPI table ──────────────────────────────────────────────────
            if (model.Kpis.Any())
                col.Item().Element(c => ComposeKpiTable(c, model));

            // ── Scorecard ──────────────────────────────────────────────────
            if (model.Scorecards.Any())
                col.Item().Element(c => ComposeScorecard(c, model));

            // ── Strategic goals ────────────────────────────────────────────
            if (model.Goals.Any())
                col.Item().Element(c => ComposeGoals(c, model));
        });
    }

    // ── Summary cards ─────────────────────────────────────────────────────────
    private void ComposeSummaryCards(IContainer container, ExecutiveReportingViewModel model)
    {
        container.Column(col =>
        {
            col.Item().Text("Performance Summary").FontSize(11).Bold().FontColor(ColorPrimary);
            col.Item().Height(6);
            col.Item().Row(row =>
            {
                SummaryCard(row.RelativeItem(), "Overall Score", $"{model.OverallPct}%", ColorPrimary);
                row.ConstantItem(8);
                SummaryCard(row.RelativeItem(), "Total KPIs", model.TotalKpis.ToString(), ColorPrimary);
                row.ConstantItem(8);
                SummaryCard(row.RelativeItem(), "On Track", model.OnTrack.ToString(), ColorOnTrack);
                row.ConstantItem(8);
                SummaryCard(row.RelativeItem(), "At Risk", model.AtRisk.ToString(), ColorAtRisk);
                row.ConstantItem(8);
                SummaryCard(row.RelativeItem(), "Behind", model.Behind.ToString(), ColorBehind);
            });
        });
    }

    private static void SummaryCard(IContainer container, string label, string value, string color)
    {
        container
            .Background(ColorLight).Border(1).BorderColor(ColorBorder)
            .Padding(10)
            .Column(col =>
            {
                col.Item().Text(label).FontSize(7).FontColor(ColorNoData);
                col.Item().Text(value).FontSize(16).Bold().FontColor(color);
            });
    }

    // ── KPI table ─────────────────────────────────────────────────────────────
    private void ComposeKpiTable(IContainer container, ExecutiveReportingViewModel model)
    {
        container.Column(col =>
        {
            col.Item().Text("Key Performance Indicators").FontSize(11).Bold().FontColor(ColorPrimary);
            col.Item().Height(6);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);  // KPI Name
                    cols.RelativeColumn(2);  // Department
                    cols.RelativeColumn(1.5f); // Target
                    cols.RelativeColumn(1.5f); // Actual
                    cols.RelativeColumn(1.5f); // Variance
                    cols.RelativeColumn(1.5f); // Status
                });

                // Header row
                static IContainer HeaderCell(IContainer c) =>
                    c.Background(ColorPrimary).Padding(5);

                table.Header(header =>
                {
                    foreach (var h in new[] { "KPI Name", "Department", "Target", "Actual", "Variance", "Status" })
                    {
                        header.Cell().Element(HeaderCell)
                            .Text(h).FontSize(8).Bold().FontColor(Colors.White);
                    }
                });

                // Data rows
                var rowIndex = 0;
                foreach (var kpi in model.Kpis)
                {
                    var bg = rowIndex++ % 2 == 0 ? "#ffffff" : ColorLight;
                    var statusColor = kpi.Status switch
                    {
                        "On Track" => ColorOnTrack,
                        "At Risk"  => ColorAtRisk,
                        "Behind"   => ColorBehind,
                        _          => ColorNoData
                    };

                    IContainer DataCell(IContainer c) => c.Background(bg).Padding(5);

                    table.Cell().Element(DataCell).Text(kpi.Name).FontSize(8).Bold();
                    table.Cell().Element(DataCell).Text(kpi.Department).FontSize(8).FontColor(ColorNoData);
                    table.Cell().Element(DataCell).Text(kpi.Target).FontSize(8);
                    table.Cell().Element(DataCell).Text(kpi.Actual).FontSize(8).Bold();
                    table.Cell().Element(DataCell).Text(kpi.Variance).FontSize(8).FontColor(ColorNoData);
                    table.Cell().Element(DataCell)
                        .Text(kpi.Status).FontSize(8).Bold().FontColor(statusColor);
                }
            });
        });
    }

    // ── Scorecard ─────────────────────────────────────────────────────────────
    private void ComposeScorecard(IContainer container, ExecutiveReportingViewModel model)
    {
        container.Column(col =>
        {
            col.Item().Text("Balanced Scorecard Summary").FontSize(11).Bold().FontColor(ColorPrimary);
            col.Item().Height(6);
            col.Item().Row(row =>
            {
                foreach (var sc in model.Scorecards)
                {
                    row.RelativeItem()
                        .Background(ColorLight).Border(1).BorderColor(ColorBorder)
                        .Padding(8)
                        .Column(inner =>
                        {
                            inner.Item().Text(sc.Perspective).FontSize(8).Bold().FontColor(ColorPrimary);
                            inner.Item().Height(4);
                            inner.Item().Text($"✓ {sc.OnTrack} On Track").FontSize(8).FontColor(ColorOnTrack);
                            if (sc.AtRisk > 0)
                                inner.Item().Text($"⚠ {sc.AtRisk} At Risk").FontSize(8).FontColor(ColorAtRisk);
                            if (sc.Behind > 0)
                                inner.Item().Text($"✗ {sc.Behind} Behind").FontSize(8).FontColor(ColorBehind);
                            inner.Item().Text($"Total: {sc.Total}").FontSize(7).FontColor(ColorNoData);
                        });

                    row.ConstantItem(6);
                }
            });
        });
    }

    // ── Strategic goals ───────────────────────────────────────────────────────
    private void ComposeGoals(IContainer container, ExecutiveReportingViewModel model)
    {
        container.Column(col =>
        {
            col.Item().Text("Strategic Goals").FontSize(11).Bold().FontColor(ColorPrimary);
            col.Item().Height(6);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(5);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(1.5f);
                });

                static IContainer HeaderCell(IContainer c) =>
                    c.Background(ColorPrimary).Padding(5);

                table.Header(header =>
                {
                    foreach (var h in new[] { "Goal Title", "Status", "Target Year" })
                    {
                        header.Cell().Element(HeaderCell)
                            .Text(h).FontSize(8).Bold().FontColor(Colors.White);
                    }
                });

                var rowIndex = 0;
                foreach (var goal in model.Goals)
                {
                    var bg = rowIndex++ % 2 == 0 ? "#ffffff" : ColorLight;
                    var statusColor = goal.Status switch
                    {
                        "In Progress" => ColorOnTrack,
                        "Completed"   => ColorPrimary,
                        "Cancelled"   => ColorBehind,
                        _             => ColorNoData
                    };

                    IContainer DataCell(IContainer c) => c.Background(bg).Padding(5);

                    table.Cell().Element(DataCell).Text(goal.Title).FontSize(8).Bold();
                    table.Cell().Element(DataCell).Text(goal.Status).FontSize(8).FontColor(statusColor);
                    table.Cell().Element(DataCell)
                        .Text(goal.TargetYear.HasValue ? goal.TargetYear.Value.ToString() : "—")
                        .FontSize(8).FontColor(ColorNoData);
                }
            });
        });
    }
}
