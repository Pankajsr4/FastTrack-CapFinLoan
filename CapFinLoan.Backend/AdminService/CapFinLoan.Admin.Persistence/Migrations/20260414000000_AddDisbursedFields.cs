using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFinLoan.Admin.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDisbursedFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "DisbursedAtUtc",
            schema: "core",
            table: "LoanApplications",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "DisbursedAmount",
            schema: "core",
            table: "LoanApplications",
            type: "decimal(18,2)",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DisbursedAtUtc",
            schema: "core",
            table: "LoanApplications");

        migrationBuilder.DropColumn(
            name: "DisbursedAmount",
            schema: "core",
            table: "LoanApplications");
    }
}
