using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialYearOwnershipAndExecutionCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutionCompletedAt",
                table: "SubProjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinancialYearId",
                table: "PresentationMemos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubProjectFinancialYearId",
                table: "ExecutionStages",
                type: "int",
                nullable: true);

            // Legacy backfill is deliberately deterministic. A memo is assigned only
            // when all of its linked projects resolve to exactly one distinct year.
            // Ambiguous rows remain NULL and are excluded from year-filtered screens.
            migrationBuilder.Sql("""
                UPDATE memo
                SET FinancialYearId = resolved.FinancialYearId
                FROM PresentationMemos AS memo
                CROSS APPLY (
                    SELECT MIN(spfy.FinancialYearId) AS FinancialYearId,
                           COUNT(DISTINCT spfy.FinancialYearId) AS YearCount,
                           COUNT(DISTINCT link.SubProjectId) AS LinkedProjectCount,
                           COUNT(DISTINCT CASE WHEN spfy.FinancialYearId IS NOT NULL THEN link.SubProjectId END) AS ResolvedProjectCount
                    FROM PresentationMemoSubProjects AS link
                    LEFT JOIN SubProjectFinancialYear AS spfy
                        ON spfy.SubProjectId = link.SubProjectId
                    WHERE link.PresentationMemoId = memo.Id
                ) AS resolved
                WHERE resolved.YearCount = 1
                  AND resolved.LinkedProjectCount = resolved.ResolvedProjectCount;
                """);

            // Execution stages are assigned only when their project belongs to one
            // financial year. Duplicate legacy final-delivery rows remain unassigned
            // to avoid fabricating ownership or violating the per-cycle invariant.
            migrationBuilder.Sql("""
                UPDATE stage
                SET SubProjectFinancialYearId = resolved.SubProjectFinancialYearId
                FROM ExecutionStages AS stage
                CROSS APPLY (
                    SELECT MIN(spfy.SubProjectFinancialYearId) AS SubProjectFinancialYearId,
                           COUNT(*) AS YearCount
                    FROM SubProjectFinancialYear AS spfy
                    WHERE spfy.SubProjectId = stage.SubProjectId
                ) AS resolved
                WHERE resolved.YearCount = 1
                  AND (
                      stage.IsFinalDelivery = 0
                      OR 1 = (
                          SELECT COUNT(*)
                          FROM ExecutionStages AS finalStage
                          WHERE finalStage.SubProjectId = stage.SubProjectId
                            AND finalStage.IsFinalDelivery = 1
                      )
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PresentationMemos_FinancialYearId",
                table: "PresentationMemos",
                column: "FinancialYearId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionStages_SubProjectFinancialYearId_IsFinalDelivery",
                table: "ExecutionStages",
                columns: new[] { "SubProjectFinancialYearId", "IsFinalDelivery" },
                unique: true,
                filter: "[IsFinalDelivery] = 1 AND [SubProjectFinancialYearId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionStages_SubProjectFinancialYear_SubProjectFinancialYearId",
                table: "ExecutionStages",
                column: "SubProjectFinancialYearId",
                principalTable: "SubProjectFinancialYear",
                principalColumn: "SubProjectFinancialYearId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PresentationMemos_FinancialYears_FinancialYearId",
                table: "PresentationMemos",
                column: "FinancialYearId",
                principalTable: "FinancialYears",
                principalColumn: "FinancialYearId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionStages_SubProjectFinancialYear_SubProjectFinancialYearId",
                table: "ExecutionStages");

            migrationBuilder.DropForeignKey(
                name: "FK_PresentationMemos_FinancialYears_FinancialYearId",
                table: "PresentationMemos");

            migrationBuilder.DropIndex(
                name: "IX_PresentationMemos_FinancialYearId",
                table: "PresentationMemos");

            migrationBuilder.DropIndex(
                name: "IX_ExecutionStages_SubProjectFinancialYearId_IsFinalDelivery",
                table: "ExecutionStages");

            migrationBuilder.DropColumn(
                name: "ExecutionCompletedAt",
                table: "SubProjects");

            migrationBuilder.DropColumn(
                name: "FinancialYearId",
                table: "PresentationMemos");

            migrationBuilder.DropColumn(
                name: "SubProjectFinancialYearId",
                table: "ExecutionStages");
        }
    }
}
