using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeakMetrics.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategicGoalArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "StrategicGoals",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "StrategicGoals");
        }
    }
}
