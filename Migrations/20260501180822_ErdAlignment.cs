using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PeakMetrics.Web.Migrations
{
    /// <inheritdoc />
    public partial class ErdAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Step 1: Create Perspectives table and seed rows ───────────────
            // Must exist before any FK columns that reference it are added.
            migrationBuilder.CreateTable(
                name: "Perspectives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Perspectives", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Perspectives_Name",
                table: "Perspectives",
                column: "Name",
                unique: true);

            migrationBuilder.InsertData(
                table: "Perspectives",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Financial" },
                    { 2, "Customer" },
                    { 3, "Internal Process" },
                    { 4, "Learning & Growth" }
                });

            // ── Step 2: Kpis — add PerspectiveId as nullable, populate, make NOT NULL, drop old column ──
            migrationBuilder.AddColumn<int>(
                name: "PerspectiveId",
                table: "Kpis",
                type: "int",
                nullable: true,   // nullable first so existing rows don't violate NOT NULL
                defaultValue: null);

            // Populate PerspectiveId from the old Perspective string
            migrationBuilder.Sql(@"
                UPDATE Kpis SET PerspectiveId = CASE Perspective
                    WHEN 'Financial'        THEN 1
                    WHEN 'Customer'         THEN 2
                    WHEN 'Internal Process' THEN 3
                    WHEN 'Learning & Growth' THEN 4
                    ELSE 1  -- fallback to Financial for any unrecognised value
                END
            ");

            // Now make it NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "PerspectiveId",
                table: "Kpis",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Drop the old free-text Perspective column
            migrationBuilder.DropColumn(
                name: "Perspective",
                table: "Kpis");

            // ── Step 3: Kpis — add Frequency, Status, CreatedByUserId ─────────
            migrationBuilder.AddColumn<string>(
                name: "Frequency",
                table: "Kpis",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Kpis",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "On Track");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Kpis",
                type: "int",
                nullable: true);

            // ── Step 4: StrategicGoals — add PerspectiveId as nullable, populate, make NOT NULL, drop old column ──
            migrationBuilder.AddColumn<int>(
                name: "PerspectiveId",
                table: "StrategicGoals",
                type: "int",
                nullable: true,
                defaultValue: null);

            // Populate PerspectiveId from the old Perspective string
            migrationBuilder.Sql(@"
                UPDATE StrategicGoals SET PerspectiveId = CASE Perspective
                    WHEN 'Financial'        THEN 1
                    WHEN 'Customer'         THEN 2
                    WHEN 'Internal Process' THEN 3
                    WHEN 'Learning & Growth' THEN 4
                    ELSE 1  -- fallback to Financial for any unrecognised value
                END
            ");

            // Now make it NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "PerspectiveId",
                table: "StrategicGoals",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Drop the old free-text Perspective column
            migrationBuilder.DropColumn(
                name: "Perspective",
                table: "StrategicGoals");

            // ── Step 5: StrategicGoals — add TargetYear, populate from DueDate, drop DueDate ──
            migrationBuilder.AddColumn<int>(
                name: "TargetYear",
                table: "StrategicGoals",
                type: "int",
                nullable: true);

            // Preserve the year component of any existing DueDate values
            migrationBuilder.Sql(@"
                UPDATE StrategicGoals
                SET TargetYear = YEAR(DueDate)
                WHERE DueDate IS NOT NULL
            ");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "StrategicGoals");

            // ── Step 6: Create GoalKpis junction table ────────────────────────
            migrationBuilder.CreateTable(
                name: "GoalKpis",
                columns: table => new
                {
                    GoalId = table.Column<int>(type: "int", nullable: false),
                    KpiId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalKpis", x => new { x.GoalId, x.KpiId });
                    table.ForeignKey(
                        name: "FK_GoalKpis_Kpis_KpiId",
                        column: x => x.KpiId,
                        principalTable: "Kpis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalKpis_StrategicGoals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "StrategicGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoalKpis_KpiId",
                table: "GoalKpis",
                column: "KpiId");

            // ── Step 7: Notifications — add Type, populate from Severity, drop Severity+Icon, add KpiId ──
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Info");

            // Populate Type from the old Severity values
            migrationBuilder.Sql(@"
                UPDATE Notifications SET [Type] = CASE Severity
                    WHEN 'Critical' THEN 'Alert'
                    WHEN 'Warning'  THEN 'Warning'
                    ELSE 'Info'
                END
            ");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Notifications");

            migrationBuilder.AddColumn<int>(
                name: "KpiId",
                table: "Notifications",
                type: "int",
                nullable: true);

            // ── Step 8: Update seed role values in Users table ────────────────
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValues: new object[] { 1, 3, 4, 5 },
                columns: new[] { "Role" },
                values: new object[,]
                {
                    { "Super Admin" },
                    { "Staff" },
                    { "Staff" },
                    { "Staff" },
                });

            // ── Step 9: Update seed data for Kpis ────────────────────────────
            migrationBuilder.UpdateData(
                table: "Kpis",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                columns: new[] { "CreatedByUserId", "Frequency", "PerspectiveId", "Status" },
                values: new object[,]
                {
                    { null, "Monthly", 1, "On Track" },
                    { null, "Monthly", 1, "On Track" },
                    { null, "Monthly", 4, "On Track" },
                    { null, "Monthly", 4, "On Track" },
                    { null, "Monthly", 2, "On Track" },
                    { null, "Monthly", 2, "On Track" },
                    { null, "Monthly", 3, "On Track" },
                    { null, "Monthly", 3, "On Track" },
                });

            // ── Step 10: Add indexes and foreign keys ─────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_StrategicGoals_PerspectiveId",
                table: "StrategicGoals",
                column: "PerspectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_KpiId",
                table: "Notifications",
                column: "KpiId");

            migrationBuilder.CreateIndex(
                name: "IX_Kpis_CreatedByUserId",
                table: "Kpis",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Kpis_PerspectiveId",
                table: "Kpis",
                column: "PerspectiveId");

            migrationBuilder.AddForeignKey(
                name: "FK_Kpis_Perspectives_PerspectiveId",
                table: "Kpis",
                column: "PerspectiveId",
                principalTable: "Perspectives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Kpis_Users_CreatedByUserId",
                table: "Kpis",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Kpis_KpiId",
                table: "Notifications",
                column: "KpiId",
                principalTable: "Kpis",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StrategicGoals_Perspectives_PerspectiveId",
                table: "StrategicGoals",
                column: "PerspectiveId",
                principalTable: "Perspectives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Drop foreign keys first ───────────────────────────────────────
            migrationBuilder.DropForeignKey(
                name: "FK_Kpis_Perspectives_PerspectiveId",
                table: "Kpis");

            migrationBuilder.DropForeignKey(
                name: "FK_Kpis_Users_CreatedByUserId",
                table: "Kpis");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Kpis_KpiId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_StrategicGoals_Perspectives_PerspectiveId",
                table: "StrategicGoals");

            migrationBuilder.DropTable(
                name: "GoalKpis");

            // ── Restore Notifications: add Severity+Icon back, populate from Type, drop Type+KpiId ──
            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Notifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "bi-info-circle");

            // Restore Severity and Icon from Type
            migrationBuilder.Sql(@"
                UPDATE Notifications SET
                    Severity = CASE [Type]
                        WHEN 'Alert'   THEN 'Critical'
                        WHEN 'Warning' THEN 'Warning'
                        ELSE 'Standard'
                    END,
                    Icon = CASE [Type]
                        WHEN 'Alert'   THEN 'bi-x-circle'
                        WHEN 'Warning' THEN 'bi-exclamation-triangle'
                        ELSE 'bi-info-circle'
                    END
            ");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_KpiId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "KpiId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Notifications");

            // ── Restore StrategicGoals: add Perspective+DueDate back, populate, drop new columns ──
            migrationBuilder.AddColumn<string>(
                name: "Perspective",
                table: "StrategicGoals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Restore Perspective string from PerspectiveId
            migrationBuilder.Sql(@"
                UPDATE sg SET sg.Perspective = p.Name
                FROM StrategicGoals sg
                INNER JOIN Perspectives p ON p.Id = sg.PerspectiveId
            ");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "StrategicGoals",
                type: "datetime2",
                nullable: true);

            // Restore DueDate from TargetYear (Jan 1 of that year)
            migrationBuilder.Sql(@"
                UPDATE StrategicGoals
                SET DueDate = DATETIMEFROMPARTS(TargetYear, 1, 1, 0, 0, 0, 0)
                WHERE TargetYear IS NOT NULL
            ");

            migrationBuilder.DropIndex(
                name: "IX_StrategicGoals_PerspectiveId",
                table: "StrategicGoals");

            migrationBuilder.DropColumn(
                name: "PerspectiveId",
                table: "StrategicGoals");

            migrationBuilder.DropColumn(
                name: "TargetYear",
                table: "StrategicGoals");

            // ── Restore Kpis: add Perspective string back, populate, drop new columns ──
            migrationBuilder.AddColumn<string>(
                name: "Perspective",
                table: "Kpis",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Restore Perspective string from PerspectiveId
            migrationBuilder.Sql(@"
                UPDATE k SET k.Perspective = p.Name
                FROM Kpis k
                INNER JOIN Perspectives p ON p.Id = k.PerspectiveId
            ");

            migrationBuilder.DropIndex(
                name: "IX_Kpis_CreatedByUserId",
                table: "Kpis");

            migrationBuilder.DropIndex(
                name: "IX_Kpis_PerspectiveId",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "PerspectiveId",
                table: "Kpis");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Kpis");

            // ── Drop Perspectives table (after all FKs referencing it are gone) ──
            migrationBuilder.DropTable(
                name: "Perspectives");

            // ── Restore seed data to original values ──────────────────────────
            migrationBuilder.UpdateData(
                table: "Kpis",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                columns: new[] { "Perspective" },
                values: new object[,]
                {
                    { "Financial" },
                    { "Financial" },
                    { "Learning & Growth" },
                    { "Learning & Growth" },
                    { "Customer" },
                    { "Customer" },
                    { "Internal Process" },
                    { "Internal Process" },
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValues: new object[] { 1, 3, 4, 5 },
                columns: new[] { "Role" },
                values: new object[,]
                {
                    { "Admin" },
                    { "User" },
                    { "User" },
                    { "User" },
                });
        }
    }
}
