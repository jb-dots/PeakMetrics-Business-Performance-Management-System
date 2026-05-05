using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeakMetrics.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSeedEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename Super Admin: admin@peakmetrics.com → superadmin@peakmetrics.com
            migrationBuilder.Sql(
                "UPDATE Users SET Email = 'superadmin@peakmetrics.com' WHERE Email = 'admin@peakmetrics.com' AND Role = 'Super Admin'");

            // Rename Administrator: hradmin@peakmetrics.com → admin@peakmetrics.com
            migrationBuilder.Sql(
                "UPDATE Users SET Email = 'admin@peakmetrics.com' WHERE Email = 'hradmin@peakmetrics.com' AND Role = 'Administrator'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert Administrator: admin@peakmetrics.com → hradmin@peakmetrics.com
            migrationBuilder.Sql(
                "UPDATE Users SET Email = 'hradmin@peakmetrics.com' WHERE Email = 'admin@peakmetrics.com' AND Role = 'Administrator'");

            // Revert Super Admin: superadmin@peakmetrics.com → admin@peakmetrics.com
            migrationBuilder.Sql(
                "UPDATE Users SET Email = 'admin@peakmetrics.com' WHERE Email = 'superadmin@peakmetrics.com' AND Role = 'Super Admin'");
        }
    }
}
