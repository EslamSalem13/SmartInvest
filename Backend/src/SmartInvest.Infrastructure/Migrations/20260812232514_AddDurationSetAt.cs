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

            // تصحيح رجعي: أي صف له مدة قصوى محددة مسبقًا كان يُحسب موعده النهائي من CreatedAt —
            // نعيد ضبطه لبداية اليوم الحالي بدل تاريخ إنشاء المستند القديم.
            migrationBuilder.Sql("UPDATE TenderDocuments SET DurationSetAt = GETUTCDATE() WHERE DurationDays IS NOT NULL;");
            migrationBuilder.Sql("UPDATE TechnicalEvaluations SET DurationSetAt = GETUTCDATE() WHERE DurationDays IS NOT NULL;");
            migrationBuilder.Sql("UPDATE OpeningEnvelopes SET DurationSetAt = GETUTCDATE() WHERE DurationDays IS NOT NULL;");
            migrationBuilder.Sql("UPDATE FinancialEvaluations SET DurationSetAt = GETUTCDATE() WHERE DurationDays IS NOT NULL;");
            migrationBuilder.Sql("UPDATE ContractAwards SET DurationSetAt = GETUTCDATE() WHERE DurationDays IS NOT NULL;");
            migrationBuilder.Sql("UPDATE Announcements SET DurationSetAt = GETUTCDATE() WHERE DurationDays IS NOT NULL;");
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
