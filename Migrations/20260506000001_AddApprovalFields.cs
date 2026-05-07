using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeakMetrics.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF NOT EXISTS guards so this migration is safe to run on any
            // database state — whether the columns already exist or not.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsApproved')
    ALTER TABLE [Users] ADD [IsApproved] bit NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ApprovedAt')
    ALTER TABLE [Users] ADD [ApprovedAt] datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ApprovedById')
    ALTER TABLE [Users] ADD [ApprovedById] nvarchar(max) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'EmailConfirmed')
    ALTER TABLE [Users] ADD [EmailConfirmed] bit NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PendingRole')
    ALTER TABLE [Users] ADD [PendingRole] nvarchar(max) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'PendingDepartmentId')
    ALTER TABLE [Users] ADD [PendingDepartmentId] nvarchar(max) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'ConfirmationToken')
    ALTER TABLE [Users] ADD [ConfirmationToken] nvarchar(max) NULL;

-- Approve and confirm all seeded accounts (Ids 1-6) so they can still log in
UPDATE [Users] SET [IsApproved] = 1, [EmailConfirmed] = 1 WHERE [Id] IN (1, 2, 3, 4, 5, 6);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsApproved",          table: "Users");
            migrationBuilder.DropColumn(name: "ApprovedAt",          table: "Users");
            migrationBuilder.DropColumn(name: "ApprovedById",        table: "Users");
            migrationBuilder.DropColumn(name: "EmailConfirmed",      table: "Users");
            migrationBuilder.DropColumn(name: "PendingRole",         table: "Users");
            migrationBuilder.DropColumn(name: "PendingDepartmentId", table: "Users");
            migrationBuilder.DropColumn(name: "ConfirmationToken",   table: "Users");
        }
    }
}
