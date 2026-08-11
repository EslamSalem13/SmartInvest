using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankAvailabilities",
                columns: table => new
                {
                    BankAvailabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FinancialYearId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAvailabilities", x => x.BankAvailabilityId);
                    table.ForeignKey(
                        name: "FK_BankAvailabilities_FinancialYears_FinancialYearId",
                        column: x => x.FinancialYearId,
                        principalTable: "FinancialYears",
                        principalColumn: "FinancialYearId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankAvailabilityDocuments",
                columns: table => new
                {
                    BankAvailabilityDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankAvailabilityId = table.Column<int>(type: "int", nullable: false),
                    File_FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    File_FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    File_FileSize = table.Column<long>(type: "bigint", nullable: false),
                    File_Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAvailabilityDocuments", x => x.BankAvailabilityDocumentId);
                    table.ForeignKey(
                        name: "FK_BankAvailabilityDocuments_BankAvailabilities_BankAvailabilityId",
                        column: x => x.BankAvailabilityId,
                        principalTable: "BankAvailabilities",
                        principalColumn: "BankAvailabilityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankAvailabilities_FinancialYearId",
                table: "BankAvailabilities",
                column: "FinancialYearId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAvailabilityDocuments_BankAvailabilityId",
                table: "BankAvailabilityDocuments",
                column: "BankAvailabilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankAvailabilityDocuments");

            migrationBuilder.DropTable(
                name: "BankAvailabilities");
        }
    }
}
