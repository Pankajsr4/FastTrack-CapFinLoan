using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFinLoan.Admin.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[core].[Decisions]', N'U') IS NULL
BEGIN
    CREATE TABLE [core].[Decisions]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [LoanApplicationId] UNIQUEIDENTIFIER NOT NULL,
        [AdminUserId] UNIQUEIDENTIFIER NOT NULL,
        [DecisionStatus] NVARCHAR(30) NOT NULL,
        [Remarks] NVARCHAR(1000) NOT NULL,
        [SanctionAmount] DECIMAL(18,2) NULL,
        [InterestRate] DECIMAL(5,2) NULL,
        [DecisionAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [PK_Decisions] PRIMARY KEY ([Id])
    );
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Decisions_LoanApplicationId'
      AND object_id = OBJECT_ID(N'[core].[Decisions]')
)
BEGIN
    CREATE INDEX [IX_Decisions_LoanApplicationId]
        ON [core].[Decisions]([LoanApplicationId]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[core].[Decisions]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [core].[Decisions];
END
");
        }
    }
}
