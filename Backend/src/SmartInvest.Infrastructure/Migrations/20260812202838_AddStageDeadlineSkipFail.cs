using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStageDeadlineSkipFail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "TenderDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "TenderDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "TenderDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "TenderDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "TenderDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SkippedAt",
                table: "TenderDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "TechnicalEvaluations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "TechnicalEvaluations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "TechnicalEvaluations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "TechnicalEvaluations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "TechnicalEvaluations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SkippedAt",
                table: "TechnicalEvaluations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "OpeningEnvelopes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "OpeningEnvelopes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "OpeningEnvelopes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "OpeningEnvelopes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "OpeningEnvelopes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SkippedAt",
                table: "OpeningEnvelopes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "FinancialEvaluations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "FinancialEvaluations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "FinancialEvaluations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "FinancialEvaluations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "FinancialEvaluations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SkippedAt",
                table: "FinancialEvaluations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "ContractAwards",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "ContractAwards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "ContractAwards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "ContractAwards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "ContractAwards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SkippedAt",
                table: "ContractAwards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AnnouncementDate",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "Announcements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "Announcements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Announcements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "Announcements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "Announcements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SkippedAt",
                table: "Announcements",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "TenderDocuments");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "TenderDocuments");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "TenderDocuments");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "TenderDocuments");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "TenderDocuments");

            migrationBuilder.DropColumn(
                name: "SkippedAt",
                table: "TenderDocuments");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "TechnicalEvaluations");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "TechnicalEvaluations");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "TechnicalEvaluations");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "TechnicalEvaluations");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "TechnicalEvaluations");

            migrationBuilder.DropColumn(
                name: "SkippedAt",
                table: "TechnicalEvaluations");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "OpeningEnvelopes");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "OpeningEnvelopes");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "OpeningEnvelopes");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "OpeningEnvelopes");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "OpeningEnvelopes");

            migrationBuilder.DropColumn(
                name: "SkippedAt",
                table: "OpeningEnvelopes");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "FinancialEvaluations");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "FinancialEvaluations");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "FinancialEvaluations");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "FinancialEvaluations");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "FinancialEvaluations");

            migrationBuilder.DropColumn(
                name: "SkippedAt",
                table: "FinancialEvaluations");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "SkippedAt",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "AnnouncementDate",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "Announcements");

            migrationBuilder.DropColumn(
                name: "SkippedAt",
                table: "Announcements");
        }
    }
}
