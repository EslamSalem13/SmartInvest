using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RelaxProjectUniquenessConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubProjects_MainProjectId",
                table: "SubProjects");

            migrationBuilder.DropIndex(
                name: "IX_SubProjects_SubProjectName",
                table: "SubProjects");

            migrationBuilder.DropIndex(
                name: "IX_MainProjects_MainProjectCode",
                table: "MainProjects");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_MainProjectId_SubProjectName",
                table: "SubProjects",
                columns: new[] { "MainProjectId", "SubProjectName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MainProjects_MainProjectCode",
                table: "MainProjects",
                column: "MainProjectCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubProjects_MainProjectId_SubProjectName",
                table: "SubProjects");

            migrationBuilder.DropIndex(
                name: "IX_MainProjects_MainProjectCode",
                table: "MainProjects");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_MainProjectId",
                table: "SubProjects",
                column: "MainProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_SubProjectName",
                table: "SubProjects",
                column: "SubProjectName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MainProjects_MainProjectCode",
                table: "MainProjects",
                column: "MainProjectCode",
                unique: true,
                filter: "[MainProjectCode] IS NOT NULL");
        }
    }
}
