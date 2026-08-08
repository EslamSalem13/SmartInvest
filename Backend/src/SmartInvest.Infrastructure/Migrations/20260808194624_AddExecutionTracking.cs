using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OverrunPercentage",
                table: "SubProjects",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WillWorkAgain",
                table: "Contractor",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractorNotes",
                columns: table => new
                {
                    ContractorNoteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractorId = table.Column<int>(type: "int", nullable: false),
                    SubProjectId = table.Column<int>(type: "int", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsAiGenerated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorNotes", x => x.ContractorNoteId);
                    table.ForeignKey(
                        name: "FK_ContractorNotes_Contractor_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractor",
                        principalColumn: "ContractorId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractorNotes_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionStages",
                columns: table => new
                {
                    ExecutionStageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SelfFundingSpent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BankFundingSpent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SelfFundingProof_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SelfFundingProof_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SelfFundingProof_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    SelfFundingProof_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    BankFundingProof_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BankFundingProof_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BankFundingProof_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    BankFundingProof_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PhysicalProgressPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PhysicalProgressProof_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PhysicalProgressProof_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PhysicalProgressProof_FileSize = table.Column<long>(type: "bigint", nullable: true),
                    PhysicalProgressProof_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PenaltyPaid = table.Column<bool>(type: "bit", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionStages", x => x.ExecutionStageId);
                    table.ForeignKey(
                        name: "FK_ExecutionStages_SubProjects_SubProjectId",
                        column: x => x.SubProjectId,
                        principalTable: "SubProjects",
                        principalColumn: "SubProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractorNotes_ContractorId",
                table: "ContractorNotes",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorNotes_SubProjectId",
                table: "ContractorNotes",
                column: "SubProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionStages_SubProjectId",
                table: "ExecutionStages",
                column: "SubProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractorNotes");

            migrationBuilder.DropTable(
                name: "ExecutionStages");

            migrationBuilder.DropColumn(
                name: "OverrunPercentage",
                table: "SubProjects");

            migrationBuilder.DropColumn(
                name: "WillWorkAgain",
                table: "Contractor");
        }
    }
}
