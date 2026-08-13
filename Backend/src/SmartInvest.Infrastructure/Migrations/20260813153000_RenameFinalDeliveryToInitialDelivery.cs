using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartInvest.Infrastructure.Data;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260813153000_RenameFinalDeliveryToInitialDelivery")]
public sealed class RenameFinalDeliveryToInitialDelivery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE ExecutionStages SET Name = N'التسليم الأولي' WHERE IsFinalDelivery = 1;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE ExecutionStages SET Name = N'التسليم النهائي' WHERE IsFinalDelivery = 1;");
    }
}
