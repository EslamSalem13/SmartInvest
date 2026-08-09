using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteHandoverProofAndFinalDeliveryStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "Deadline",
                table: "ExecutionStages",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<bool>(
                name: "IsFinalDelivery",
                table: "ExecutionStages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "SiteHandoverProof_Content",
                table: "ContractAwards",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteHandoverProof_FileExtension",
                table: "ContractAwards",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteHandoverProof_FileName",
                table: "ContractAwards",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SiteHandoverProof_FileSize",
                table: "ContractAwards",
                type: "bigint",
                nullable: true);

            // كل ترسية مكتملة موجودة بالفعل تحصل على مرحلة التسليم النهائي، حتى تتطابق
            // البيانات القديمة مع الجديدة من أول يوم. NOT EXISTS يجعله آمنًا لإعادة التشغيل.
            migrationBuilder.Sql(@"
INSERT INTO ExecutionStages
    (SubProjectId, Name, Deadline, SelfFundingSpent, BankFundingSpent,
     PhysicalProgressPercent, PenaltyPaid, IsCompleted, IsFinalDelivery, CreatedAt)
SELECT ca.SubProjectId,
       N'التسليم النهائي',
       CASE WHEN ca.SiteHandoverDate IS NULL THEN NULL
            ELSE DATEADD(DAY, ISNULL(ca.ExecutionDurationDays, 0),
                 DATEADD(MONTH, ISNULL(ca.ExecutionDurationMonths, 0), ca.SiteHandoverDate))
       END,
       0, 0, 0, 0, 0, 1, SYSUTCDATETIME()
FROM ContractAwards ca
WHERE ca.IsCompleted = 1
  AND NOT EXISTS (
      SELECT 1 FROM ExecutionStages e
      WHERE e.SubProjectId = ca.SubProjectId AND e.IsFinalDelivery = 1);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ExecutionStages WHERE IsFinalDelivery = 1;");

            migrationBuilder.DropColumn(
                name: "IsFinalDelivery",
                table: "ExecutionStages");

            migrationBuilder.DropColumn(
                name: "SiteHandoverProof_Content",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "SiteHandoverProof_FileExtension",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "SiteHandoverProof_FileName",
                table: "ContractAwards");

            migrationBuilder.DropColumn(
                name: "SiteHandoverProof_FileSize",
                table: "ContractAwards");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Deadline",
                table: "ExecutionStages",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
