using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PeakMetrics.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedExecutiveAndAdministrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "DepartmentId", "Email", "FullName", "IsActive", "LastLoginAt", "PasswordHash", "Role" },
                values: new object[,]
                {
                    { 6, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "executive@peakmetrics.com", "Executive User", true, null, "$2a$11$0yCucsKKCKwaqMLlZewNtugJYHERt1WN6Q7TaM51dHvjwEBKDOe/i", "Executive" },
                    { 7, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, "hradmin@peakmetrics.com", "HR Admin", true, null, "$2a$11$gIbViYapQPsEwJbISIDvQu/vBawKNXbhzVjJFLfxW9qCfZDAsWaDG", "Administrator" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 7);
        }
    }
}
