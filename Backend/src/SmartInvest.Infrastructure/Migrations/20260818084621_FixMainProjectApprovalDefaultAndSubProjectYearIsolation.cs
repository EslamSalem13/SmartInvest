using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMainProjectApprovalDefaultAndSubProjectYearIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubProjects_MainProjectId_SubProjectName",
                table: "SubProjects");

            migrationBuilder.AlterColumn<bool>(
                name: "IsApproved",
                table: "MainProjects",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_MainProjectId_SubProjectName",
                table: "SubProjects",
                columns: new[] { "MainProjectId", "SubProjectName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubProjects_MainProjectId_SubProjectName",
                table: "SubProjects");

            migrationBuilder.AlterColumn<bool>(
                name: "IsApproved",
                table: "MainProjects",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_MainProjectId_SubProjectName",
                table: "SubProjects",
                columns: new[] { "MainProjectId", "SubProjectName" },
                unique: true);
        }
    }
}
