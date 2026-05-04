using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFinLoan.Application.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEditTrackingAndWithdrawal : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── New columns on LoanApplications ──────────────────────────────────
        migrationBuilder.AddColumn<bool>(
            name: "IsEdited",
            schema: "core",
            table: "LoanApplications",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "EditCount",
            schema: "core",
            table: "LoanApplications",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastModifiedAt",
            schema: "core",
            table: "LoanApplications",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastModifiedBy",
            schema: "core",
            table: "LoanApplications",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WithdrawalReason",
            schema: "core",
            table: "LoanApplications",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "WithdrawnAtUtc",
            schema: "core",
            table: "LoanApplications",
            type: "datetime2",
            nullable: true);

        // ── ApplicationHistories table ────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ApplicationHistories",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                OldData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NewData = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApplicationHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApplicationHistories_LoanApplications_ApplicationId",
                    column: x => x.ApplicationId,
                    principalSchema: "core",
                    principalTable: "LoanApplications",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApplicationHistories_ApplicationId",
            schema: "core",
            table: "ApplicationHistories",
            column: "ApplicationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ApplicationHistories", schema: "core");

        migrationBuilder.DropColumn(name: "IsEdited",        schema: "core", table: "LoanApplications");
        migrationBuilder.DropColumn(name: "EditCount",       schema: "core", table: "LoanApplications");
        migrationBuilder.DropColumn(name: "LastModifiedAt",  schema: "core", table: "LoanApplications");
        migrationBuilder.DropColumn(name: "LastModifiedBy",  schema: "core", table: "LoanApplications");
        migrationBuilder.DropColumn(name: "WithdrawalReason",schema: "core", table: "LoanApplications");
        migrationBuilder.DropColumn(name: "WithdrawnAtUtc",  schema: "core", table: "LoanApplications");
    }
}
