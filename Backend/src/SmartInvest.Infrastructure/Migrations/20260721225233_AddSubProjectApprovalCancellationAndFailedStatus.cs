using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubProjectApprovalCancellationAndFailedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubProjects_SubProjectCode",
                table: "SubProjects");

            migrationBuilder.AlterColumn<string>(
                name: "SubProjectName",
                table: "SubProjects",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalCancellationReason",
                table: "SubProjects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalCancelledAt",
                table: "SubProjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "SubProjects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "SubProjects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_SubProjectCode",
                table: "SubProjects",
                column: "SubProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_SubProjectName",
                table: "SubProjects",
                column: "SubProjectName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubProjects_SubProjectCode",
                table: "SubProjects");

            migrationBuilder.DropIndex(
                name: "IX_SubProjects_SubProjectName",
                table: "SubProjects");

            migrationBuilder.DropColumn(
                name: "ApprovalCancellationReason",
                table: "SubProjects");

            migrationBuilder.DropColumn(
                name: "ApprovalCancelledAt",
                table: "SubProjects");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "SubProjects");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "SubProjects");

            migrationBuilder.AlterColumn<string>(
                name: "SubProjectName",
                table: "SubProjects",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250);

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_SubProjectCode",
                table: "SubProjects",
                column: "SubProjectCode",
                unique: true,
                filter: "[SubProjectCode] IS NOT NULL");
        }
    }
}
