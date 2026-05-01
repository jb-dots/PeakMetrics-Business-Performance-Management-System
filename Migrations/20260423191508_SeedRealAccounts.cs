using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

// SonarQube S2068: PasswordHash values below are placeholder hashes for seed accounts only.
// Real passwords must be set via the application's Change Password feature after first login.
// These hashes do not represent any real credential and cannot be used to authenticate.
#pragma warning disable S2068 // Credentials should not be hard-coded

namespace PeakMetrics.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedRealAccounts : Migration
    {
        // Placeholder bcrypt hash — not a real password. Seed accounts must change password on first login.
        private const string PlaceholderHash = "$2a$11$PLACEHOLDER000000000000000000000000000000000000000000000";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: PlaceholderHash);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "DepartmentId", "Email", "FullName", "IsActive", "LastLoginAt", "PasswordHash", "Role" },
                values: new object[,]
                {
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, "manager@peakmetrics.com", "Maria Santos", true, null, PlaceholderHash, "Manager" },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, "sarah@peakmetrics.com", "Sarah Johnson", true, null, PlaceholderHash, "User" },
                    { 4, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, "michael@peakmetrics.com", "Michael Chen", true, null, PlaceholderHash, "User" },
                    { 5, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, "emily@peakmetrics.com", "Emily Davis", true, null, PlaceholderHash, "User" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: PlaceholderHash);
        }
    }
}

#pragma warning restore S2068
