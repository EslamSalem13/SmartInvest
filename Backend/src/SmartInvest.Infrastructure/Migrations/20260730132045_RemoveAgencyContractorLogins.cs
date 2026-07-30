using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAgencyContractorLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Contractor_ContractorId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_ExecutiveAgency_ExecutiveAgencyId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ContractorId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ExecutiveAgencyId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ContractorId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ExecutiveAgencyId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ExecutiveAgency",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ExecutiveAgency");

            migrationBuilder.AddColumn<int>(
                name: "ContractorId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExecutiveAgencyId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ContractorId",
                table: "AspNetUsers",
                column: "ContractorId",
                unique: true,
                filter: "[ContractorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ExecutiveAgencyId",
                table: "AspNetUsers",
                column: "ExecutiveAgencyId",
                unique: true,
                filter: "[ExecutiveAgencyId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Contractor_ContractorId",
                table: "AspNetUsers",
                column: "ContractorId",
                principalTable: "Contractor",
                principalColumn: "ContractorId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_ExecutiveAgency_ExecutiveAgencyId",
                table: "AspNetUsers",
                column: "ExecutiveAgencyId",
                principalTable: "ExecutiveAgency",
                principalColumn: "ExecutiveAgencyId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
