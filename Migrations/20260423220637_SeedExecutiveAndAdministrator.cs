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
    public partial class SeedExecutiveAndAdministrator : Migration
    {
        // Placeholder bcrypt hash — not a real password. Seed accounts must change password on first login.
        private const string PlaceholderHash = "$2a$11$PLACEHOLDER000000000000000000000000000000000000000000000";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "DepartmentId", "Email", "FullName", "IsActive", "LastLoginAt", "PasswordHash", "Role" },
                values: new object[,]
                {
                    { 6, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "executive@peakmetrics.com", "Executive User", true, null, PlaceholderHash, "Executive" },
                    { 7, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, "hradmin@peakmetrics.com", "HR Admin", true, null, PlaceholderHash, "Administrator" }
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

#pragma warning restore S2068
