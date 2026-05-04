using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFinLoan.Document.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new Status column with default 'Pending'
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Documents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            // Migrate existing data: IsVerified=true => Verified, IsVerified=false => Pending
            migrationBuilder.Sql(
                "UPDATE Documents SET Status = 'Verified' WHERE IsVerified = 1");

            // Drop the old IsVerified column
            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "Documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add IsVerified column
            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "Documents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Migrate data back
            migrationBuilder.Sql(
                "UPDATE Documents SET IsVerified = 1 WHERE Status = 'Verified'");

            // Drop Status column
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Documents");
        }
    }
}
