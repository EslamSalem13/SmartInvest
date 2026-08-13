using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationSetAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DurationSetAt",
                table: "TenderDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DurationSetAt",
                table: "TechnicalEvaluations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DurationSetAt",
                table: "OpeningEnvelopes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DurationSetAt",
                table: "FinancialEvaluations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DurationSetAt",
                table: "ContractAwards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DurationSetAt",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            // لا نضع وقت تشغيل الـmigration كبداية وهمية للسجلات القديمة. تبدأ المدة فقط من حدث تفعيل حقيقي لاحق.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationSetAt",
                table: "TenderDocuments");

            migrationBuilder.DropColumn(
                name: "DurationSetAt",
                table: "TechnicalEvaluations");

            migrationBuilder.DropColumn(
                name: "DurationSetAt",
                table: "OpeningEnvelopes");

            migrationBuilder.DropColumn(
                name: "DurationSetAt",
                table: "FinancialEvaluations");

            migrationBuilder.DropColumn(
                name: "DurationSetAt",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "DurationSetAt",
                table: "Announcements");
        }
    }
}
