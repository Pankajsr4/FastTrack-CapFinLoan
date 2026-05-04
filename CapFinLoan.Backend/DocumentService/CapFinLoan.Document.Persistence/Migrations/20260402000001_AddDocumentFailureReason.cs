using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFinLoan.Document.Persistence.Migrations
{
    /// <summary>
    /// Adds FailureReason column to Documents table.
    /// Populated when Status = 'Failed' to describe why processing failed.
    /// </summary>
    public partial class AddDocumentFailureReason : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Documents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Documents");
        }
    }
}
