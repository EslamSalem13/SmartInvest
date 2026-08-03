using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Measurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Measurements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeasurementSubProgram",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MeasurementId = table.Column<int>(type: "int", nullable: false),
                    SubProgramId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeasurementSubProgram", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeasurementSubProgram_Measurements_MeasurementId",
                        column: x => x.MeasurementId,
                        principalTable: "Measurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeasurementSubProgram_SubPrograms_SubProgramId",
                        column: x => x.SubProgramId,
                        principalTable: "SubPrograms",
                        principalColumn: "SubProgramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubProjectMeasurementValue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubProjectId = table.Column<int>(type: "int", nullable: false),
                    MeasurementId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubProjectMeasurementValue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubProjectMeasurementValue_Measurements_MeasurementId",
                        column: x => x.MeasurementId,
                        principalTable: "Measurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubProjectMeasurementValue_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementSubProgram_MeasurementId",
                table: "MeasurementSubProgram",
                column: "MeasurementId");

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementSubProgram_SubProgramId",
                table: "MeasurementSubProgram",
                column: "SubProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjectMeasurementValue_MeasurementId",
                table: "SubProjectMeasurementValue",
                column: "MeasurementId");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjectMeasurementValue_SubProjectId",
                table: "SubProjectMeasurementValue",
                column: "SubProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeasurementSubProgram");

            migrationBuilder.DropTable(
                name: "SubProjectMeasurementValue");

            migrationBuilder.DropTable(
                name: "Measurements");
        }
    }
}
