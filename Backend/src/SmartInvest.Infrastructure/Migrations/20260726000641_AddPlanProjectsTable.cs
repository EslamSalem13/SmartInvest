using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanProjectsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanProject_Plans_PlanId",
                table: "PlanProject");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanProject_SubProjects_SubProjectId",
                table: "PlanProject");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlanProject",
                table: "PlanProject");

            migrationBuilder.RenameTable(
                name: "PlanProject",
                newName: "PlanProjects");

            migrationBuilder.RenameIndex(
                name: "IX_PlanProject_SubProjectId",
                table: "PlanProjects",
                newName: "IX_PlanProjects_SubProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_PlanProject_PlanId_SubProjectId",
                table: "PlanProjects",
                newName: "IX_PlanProjects_PlanId_SubProjectId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlanProjects",
                table: "PlanProjects",
                column: "PlanProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanProjects_Plans_PlanId",
                table: "PlanProjects",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanProjects_SubProjects_SubProjectId",
                table: "PlanProjects",
                column: "SubProjectId",
                principalTable: "SubProjects",
                principalColumn: "SubProjectId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanProjects_Plans_PlanId",
                table: "PlanProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanProjects_SubProjects_SubProjectId",
                table: "PlanProjects");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlanProjects",
                table: "PlanProjects");

            migrationBuilder.RenameTable(
                name: "PlanProjects",
                newName: "PlanProject");

            migrationBuilder.RenameIndex(
                name: "IX_PlanProjects_SubProjectId",
                table: "PlanProject",
                newName: "IX_PlanProject_SubProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_PlanProjects_PlanId_SubProjectId",
                table: "PlanProject",
                newName: "IX_PlanProject_PlanId_SubProjectId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlanProject",
                table: "PlanProject",
                column: "PlanProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanProject_Plans_PlanId",
                table: "PlanProject",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "PlanId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanProject_SubProjects_SubProjectId",
                table: "PlanProject",
                column: "SubProjectId",
                principalTable: "SubProjects",
                principalColumn: "SubProjectId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
