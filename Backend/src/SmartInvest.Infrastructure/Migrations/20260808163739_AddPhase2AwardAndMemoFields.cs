using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2AwardAndMemoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LegalAffairsDecisionUploadedAt",
                table: "PresentationMemoVersions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "LegalAffairsDecision_Content",
                table: "PresentationMemoVersions",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalAffairsDecision_FileExtension",
                table: "PresentationMemoVersions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalAffairsDecision_FileName",
                table: "PresentationMemoVersions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LegalAffairsDecision_FileSize",
                table: "PresentationMemoVersions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "AdvancePaymentProof_Content",
                table: "ContractAwardVersions",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdvancePaymentProof_FileExtension",
                table: "ContractAwardVersions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdvancePaymentProof_FileName",
                table: "ContractAwardVersions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AdvancePaymentProof_FileSize",
                table: "ContractAwardVersions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdvancePaymentBankAmount",
                table: "ContractAwards",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdvancePaymentPercentage",
                table: "ContractAwards",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdvancePaymentSelfAmount",
                table: "ContractAwards",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExecutionDurationDays",
                table: "ContractAwards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExecutionDurationMonths",
                table: "ContractAwards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PenaltyAmount",
                table: "ContractAwards",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectAssignmentId",
                table: "ContractAwards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SiteHandoverDate",
                table: "ContractAwards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SiteHandoverMode",
                table: "ContractAwards",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAwards_ProjectAssignmentId",
                table: "ContractAwards",
                column: "ProjectAssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContractAwards_ProjectAssignment_ProjectAssignmentId",
                table: "ContractAwards",
                column: "ProjectAssignmentId",
                principalTable: "ProjectAssignment",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContractAwards_ProjectAssignment_ProjectAssignmentId",
                table: "ContractAwards");

            migrationBuilder.DropIndex(
                name: "IX_ContractAwards_ProjectAssignmentId",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "LegalAffairsDecisionUploadedAt",
                table: "PresentationMemoVersions");

            migrationBuilder.DropColumn(
                name: "LegalAffairsDecision_Content",
                table: "PresentationMemoVersions");

            migrationBuilder.DropColumn(
                name: "LegalAffairsDecision_FileExtension",
                table: "PresentationMemoVersions");

            migrationBuilder.DropColumn(
                name: "LegalAffairsDecision_FileName",
                table: "PresentationMemoVersions");

            migrationBuilder.DropColumn(
                name: "LegalAffairsDecision_FileSize",
                table: "PresentationMemoVersions");

            migrationBuilder.DropColumn(
                name: "AdvancePaymentProof_Content",
                table: "ContractAwardVersions");

            migrationBuilder.DropColumn(
                name: "AdvancePaymentProof_FileExtension",
                table: "ContractAwardVersions");

            migrationBuilder.DropColumn(
                name: "AdvancePaymentProof_FileName",
                table: "ContractAwardVersions");

            migrationBuilder.DropColumn(
                name: "AdvancePaymentProof_FileSize",
                table: "ContractAwardVersions");

            migrationBuilder.DropColumn(
                name: "AdvancePaymentBankAmount",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "AdvancePaymentPercentage",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "AdvancePaymentSelfAmount",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "ExecutionDurationDays",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "ExecutionDurationMonths",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "PenaltyAmount",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "ProjectAssignmentId",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "SiteHandoverDate",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "SiteHandoverMode",
                table: "ContractAwards");
        }
    }
}
