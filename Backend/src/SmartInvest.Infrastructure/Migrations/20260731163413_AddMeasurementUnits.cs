using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeasurementUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Measurements");

            migrationBuilder.AddColumn<int>(
                name: "UnitId",
                table: "SubProjectMeasurementValue",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MeasurementUnit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MeasurementId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeasurementUnit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeasurementUnit_Measurements_MeasurementId",
                        column: x => x.MeasurementId,
                        principalTable: "Measurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeasurementUnit_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubProjectMeasurementValue_UnitId",
                table: "SubProjectMeasurementValue",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementUnit_MeasurementId",
                table: "MeasurementUnit",
                column: "MeasurementId");

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementUnit_UnitId",
                table: "MeasurementUnit",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubProjectMeasurementValue_Units_UnitId",
                table: "SubProjectMeasurementValue",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubProjectMeasurementValue_Units_UnitId",
                table: "SubProjectMeasurementValue");

            migrationBuilder.DropTable(
                name: "MeasurementUnit");

            migrationBuilder.DropIndex(
                name: "IX_SubProjectMeasurementValue_UnitId",
                table: "SubProjectMeasurementValue");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "SubProjectMeasurementValue");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Measurements",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
