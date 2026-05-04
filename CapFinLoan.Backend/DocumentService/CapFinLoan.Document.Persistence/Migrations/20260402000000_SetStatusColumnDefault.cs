using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFinLoan.Document.Persistence.Migrations
{
    /// <summary>
    /// Ensures the Status column has a DB-level default of 'Pending'.
    /// The AddDocumentStatus migration added the column but the constraint
    /// was not explicitly set. This migration aligns the DB with the EF model.
    /// </summary>
    public partial class SetStatusColumnDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add a DEFAULT constraint so rows inserted without an explicit Status
            // value (e.g. raw SQL inserts, seed scripts) get 'Pending' automatically.
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Documents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Documents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: false,
                oldDefaultValue: "Pending");
        }
    }
}
