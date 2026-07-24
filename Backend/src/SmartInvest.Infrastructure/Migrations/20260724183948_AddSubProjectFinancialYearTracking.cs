using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubProjectFinancialYearTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanProject_Plan_PlanId",
                table: "PlanProject");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectFollowUp_PlanProject_PlanProjectId",
                table: "ProjectFollowUp");

            migrationBuilder.DropIndex(
                name: "IX_PlanProject_PlanId",
                table: "PlanProject");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "PlanProject");

            migrationBuilder.RenameColumn(
                name: "PlanProjectId",
                table: "ProjectFollowUp",
                newName: "SubProjectFinancialYearId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectFollowUp_PlanProjectId",
                table: "ProjectFollowUp",
                newName: "IX_ProjectFollowUp_SubProjectFinancialYearId");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalDate",
                table: "Plan",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuggestionDate",
                table: "Plan",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "SubProjectFinancialYear",
                columns: table => new
                {
                    SubProjectFinancialYearId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubProjectId = table.Column<int>(type: "int", nullable: false),
                    FinancialYearId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubProjectFinancialYear", x => x.SubProjectFinancialYearId);
                    table.ForeignKey(
                        name: "FK_SubProjectFinancialYear_FinancialYear_FinancialYearId",
                        column: x => x.FinancialYearId,
                        principalTable: "FinancialYear",
                        principalColumn: "FinancialYearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubProjectFinancialYear_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanProject_PlanId_SubProjectId",
                table: "PlanProject",
                columns: new[] { "PlanId", "SubProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubProjectFinancialYear_FinancialYearId",
                table: "SubProjectFinancialYear",
                column: "FinancialYearId");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjectFinancialYear_SubProjectId_FinancialYearId",
                table: "SubProjectFinancialYear",
                columns: new[] { "SubProjectId", "FinancialYearId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanProject_Plan_PlanId",
                table: "PlanProject",
                column: "PlanId",
                principalTable: "Plan",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectFollowUp_SubProjectFinancialYear_SubProjectFinancialYearId",
                table: "ProjectFollowUp",
                column: "SubProjectFinancialYearId",
                principalTable: "SubProjectFinancialYear",
                principalColumn: "SubProjectFinancialYearId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanProject_Plan_PlanId",
                table: "PlanProject");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectFollowUp_SubProjectFinancialYear_SubProjectFinancialYearId",
                table: "ProjectFollowUp");

            migrationBuilder.DropTable(
                name: "SubProjectFinancialYear");

            migrationBuilder.DropIndex(
                name: "IX_PlanProject_PlanId_SubProjectId",
                table: "PlanProject");

            migrationBuilder.DropColumn(
                name: "ApprovalDate",
                table: "Plan");

            migrationBuilder.DropColumn(
                name: "SuggestionDate",
                table: "Plan");

            migrationBuilder.RenameColumn(
                name: "SubProjectFinancialYearId",
                table: "ProjectFollowUp",
                newName: "PlanProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_ProjectFollowUp_SubProjectFinancialYearId",
                table: "ProjectFollowUp",
                newName: "IX_ProjectFollowUp_PlanProjectId");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "PlanProject",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PlanProject_PlanId",
                table: "PlanProject",
                column: "PlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanProject_Plan_PlanId",
                table: "PlanProject",
                column: "PlanId",
                principalTable: "Plan",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectFollowUp_PlanProject_PlanProjectId",
                table: "ProjectFollowUp",
                column: "PlanProjectId",
                principalTable: "PlanProject",
                principalColumn: "PlanProjectId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
