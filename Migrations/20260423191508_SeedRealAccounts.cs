using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PeakMetrics.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedRealAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$K2GAaeAIPqKr7/DQp1xWIuSA95c53aTx071RgaoMS7U4nTO5P1LFG");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "DepartmentId", "Email", "FullName", "IsActive", "LastLoginAt", "PasswordHash", "Role" },
                values: new object[,]
                {
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, "manager@peakmetrics.com", "Maria Santos", true, null, "$2a$11$cA3Cig0PT.t2wVj5yONGl.kQHV4pczXahzNbmghQWOBiN8Q23o212", "Manager" },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, "sarah@peakmetrics.com", "Sarah Johnson", true, null, "$2a$11$GTTjD7ErxWlfvdNygzUlaOi0jamF3GIPWHEjSUNyMgXAs3EFm58O6", "User" },
                    { 4, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, "michael@peakmetrics.com", "Michael Chen", true, null, "$2a$11$GTTjD7ErxWlfvdNygzUlaOi0jamF3GIPWHEjSUNyMgXAs3EFm58O6", "User" },
                    { 5, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, "emily@peakmetrics.com", "Emily Davis", true, null, "$2a$11$GTTjD7ErxWlfvdNygzUlaOi0jamF3GIPWHEjSUNyMgXAs3EFm58O6", "User" }
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
                value: "$2a$11$K5Ow5Ow5Ow5Ow5Ow5Ow5OeK5Ow5Ow5Ow5Ow5Ow5Ow5Ow5Ow5Ow2");
        }
    }
}
