using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFinLoan.Notification.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "notif");

        migrationBuilder.CreateTable(
            name: "Notifications",
            schema: "notif",
            columns: table => new
            {
                Id                = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                UserId            = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ApplicationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                Type              = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Title             = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Message           = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                IsRead            = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                CreatedAtUtc      = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Notifications", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_UserId",
            schema: "notif",
            table: "Notifications",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_CreatedAtUtc",
            schema: "notif",
            table: "Notifications",
            column: "CreatedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Notifications", schema: "notif");
    }
}
