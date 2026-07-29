using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMainProjectApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server refuses to ALTER COLUMN a column a filtered index depends on,
            // even though the filter/definition itself is unchanged — drop and recreate it.
            migrationBuilder.DropIndex(
                name: "IX_MainProjects_MainProjectCode",
                table: "MainProjects");

            migrationBuilder.AlterColumn<string>(
                name: "MainProjectCode",
                table: "MainProjects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "MainProjects",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_MainProjects_MainProjectCode",
                table: "MainProjects",
                column: "MainProjectCode",
                unique: true,
                filter: "[MainProjectCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MainProjects_MainProjectCode",
                table: "MainProjects");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "MainProjects");

            migrationBuilder.AlterColumn<string>(
                name: "MainProjectCode",
                table: "MainProjects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MainProjects_MainProjectCode",
                table: "MainProjects",
                column: "MainProjectCode",
                unique: true,
                filter: "[MainProjectCode] IS NOT NULL");
        }
    }
}
