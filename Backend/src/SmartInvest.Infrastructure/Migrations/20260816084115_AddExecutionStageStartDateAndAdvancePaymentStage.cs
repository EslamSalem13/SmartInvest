using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionStageStartDateAndAdvancePaymentStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdvancePayment",
                table: "ExecutionStages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "ExecutionStages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdvancePaymentDate",
                table: "ContractAwards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionStages_SubProjectFinancialYearId_IsAdvancePayment",
                table: "ExecutionStages",
                columns: new[] { "SubProjectFinancialYearId", "IsAdvancePayment" },
                unique: true,
                filter: "[IsAdvancePayment] = 1 AND [SubProjectFinancialYearId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExecutionStages_SubProjectFinancialYearId_IsAdvancePayment",
                table: "ExecutionStages");

            migrationBuilder.DropColumn(
                name: "IsAdvancePayment",
                table: "ExecutionStages");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "ExecutionStages");

            migrationBuilder.DropColumn(
                name: "AdvancePaymentDate",
                table: "ContractAwards");
        }
    }
}
