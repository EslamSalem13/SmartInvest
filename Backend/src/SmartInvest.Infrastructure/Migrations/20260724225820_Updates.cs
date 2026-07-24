using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Updates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MainProjects_SubProgram_SubProgramId",
                table: "MainProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_Plan_FinancialYear_FinancialYearId",
                table: "Plan");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanProject_Plan_PlanId",
                table: "PlanProject");

            migrationBuilder.DropForeignKey(
                name: "FK_SubProgram_MainProgram_ProgramId",
                table: "SubProgram");

            migrationBuilder.DropForeignKey(
                name: "FK_SubProjectFinancialYear_FinancialYear_FinancialYearId",
                table: "SubProjectFinancialYear");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubProgram",
                table: "SubProgram");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Plan",
                table: "Plan");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MainProgram",
                table: "MainProgram");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FinancialYear",
                table: "FinancialYear");

            migrationBuilder.RenameTable(
                name: "SubProgram",
                newName: "SubPrograms");

            migrationBuilder.RenameTable(
                name: "Plan",
                newName: "Plans");

            migrationBuilder.RenameTable(
                name: "MainProgram",
                newName: "MainPrograms");

            migrationBuilder.RenameTable(
                name: "FinancialYear",
                newName: "FinancialYears");

            migrationBuilder.RenameIndex(
                name: "IX_SubProgram_ProgramId",
                table: "SubPrograms",
                newName: "IX_SubPrograms_ProgramId");

            migrationBuilder.RenameIndex(
                name: "IX_Plan_FinancialYearId",
                table: "Plans",
                newName: "IX_Plans_FinancialYearId");

            migrationBuilder.AlterColumn<int>(
                name: "PlanStatus",
                table: "Plans",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubPrograms",
                table: "SubPrograms",
                column: "SubProgramId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Plans",
                table: "Plans",
                column: "PlanId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MainPrograms",
                table: "MainPrograms",
                column: "ProgramId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FinancialYears",
                table: "FinancialYears",
                column: "FinancialYearId");

            migrationBuilder.AddForeignKey(
                name: "FK_MainProjects_SubPrograms_SubProgramId",
                table: "MainProjects",
                column: "SubProgramId",
                principalTable: "SubPrograms",
                principalColumn: "SubProgramId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanProject_Plans_PlanId",
                table: "PlanProject",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Plans_FinancialYears_FinancialYearId",
                table: "Plans",
                column: "FinancialYearId",
                principalTable: "FinancialYears",
                principalColumn: "FinancialYearId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubPrograms_MainPrograms_ProgramId",
                table: "SubPrograms",
                column: "ProgramId",
                principalTable: "MainPrograms",
                principalColumn: "ProgramId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubProjectFinancialYear_FinancialYears_FinancialYearId",
                table: "SubProjectFinancialYear",
                column: "FinancialYearId",
                principalTable: "FinancialYears",
                principalColumn: "FinancialYearId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MainProjects_SubPrograms_SubProgramId",
                table: "MainProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanProject_Plans_PlanId",
                table: "PlanProject");

            migrationBuilder.DropForeignKey(
                name: "FK_Plans_FinancialYears_FinancialYearId",
                table: "Plans");

            migrationBuilder.DropForeignKey(
                name: "FK_SubPrograms_MainPrograms_ProgramId",
                table: "SubPrograms");

            migrationBuilder.DropForeignKey(
                name: "FK_SubProjectFinancialYear_FinancialYears_FinancialYearId",
                table: "SubProjectFinancialYear");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubPrograms",
                table: "SubPrograms");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Plans",
                table: "Plans");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MainPrograms",
                table: "MainPrograms");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FinancialYears",
                table: "FinancialYears");

            migrationBuilder.RenameTable(
                name: "SubPrograms",
                newName: "SubProgram");

            migrationBuilder.RenameTable(
                name: "Plans",
                newName: "Plan");

            migrationBuilder.RenameTable(
                name: "MainPrograms",
                newName: "MainProgram");

            migrationBuilder.RenameTable(
                name: "FinancialYears",
                newName: "FinancialYear");

            migrationBuilder.RenameIndex(
                name: "IX_SubPrograms_ProgramId",
                table: "SubProgram",
                newName: "IX_SubProgram_ProgramId");

            migrationBuilder.RenameIndex(
                name: "IX_Plans_FinancialYearId",
                table: "Plan",
                newName: "IX_Plan_FinancialYearId");

            migrationBuilder.AlterColumn<string>(
                name: "PlanStatus",
                table: "Plan",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubProgram",
                table: "SubProgram",
                column: "SubProgramId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Plan",
                table: "Plan",
                column: "PlanId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MainProgram",
                table: "MainProgram",
                column: "ProgramId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FinancialYear",
                table: "FinancialYear",
                column: "FinancialYearId");

            migrationBuilder.AddForeignKey(
                name: "FK_MainProjects_SubProgram_SubProgramId",
                table: "MainProjects",
                column: "SubProgramId",
                principalTable: "SubProgram",
                principalColumn: "SubProgramId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Plan_FinancialYear_FinancialYearId",
                table: "Plan",
                column: "FinancialYearId",
                principalTable: "FinancialYear",
                principalColumn: "FinancialYearId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanProject_Plan_PlanId",
                table: "PlanProject",
                column: "PlanId",
                principalTable: "Plan",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubProgram_MainProgram_ProgramId",
                table: "SubProgram",
                column: "ProgramId",
                principalTable: "MainProgram",
                principalColumn: "ProgramId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SubProjectFinancialYear_FinancialYear_FinancialYearId",
                table: "SubProjectFinancialYear",
                column: "FinancialYearId",
                principalTable: "FinancialYear",
                principalColumn: "FinancialYearId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
