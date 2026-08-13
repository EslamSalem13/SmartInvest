using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartInvest.Infrastructure.Data;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations;

/// <summary>
/// يجعل مدد مراحل الطرح القديمة آمنة: يثبت القيم الافتراضية دون اختراع وقت بداية.
/// إذا كان DurationSetAt قد أضيف بواسطة الترحيل القديم دون تحديث السجل نفسه، يُزال لأنه تاريخ غير حقيقي.
/// لا يبدأ حساب التأخير للسجل القديم إلا إذا كان له وقت تفعيل موثوق أو عند تفعيله لاحقًا من التطبيق.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260813120000_NormalizeProcurementStageDurations")]
public sealed class NormalizeProcurementStageDurations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[]
                 {
                     "TenderDocuments",
                     "OpeningEnvelopes",
                     "TechnicalEvaluations",
                     "FinancialEvaluations",
                 })
        {
            migrationBuilder.Sql($$"""
                UPDATE {{table}}
                SET DurationSetAt = NULL
                WHERE DurationSetAt IS NOT NULL
                  AND ABS(DATEDIFF(SECOND, CreatedAt, DurationSetAt)) > 5
                  AND (UpdatedAt IS NULL OR ABS(DATEDIFF(SECOND, UpdatedAt, DurationSetAt)) > 5);

                UPDATE {{table}}
                SET DurationDays = 7
                WHERE DurationDays IS NULL;
                """);
        }

        // الإعلان مصدر حقيقته الوحيد AnnouncementDate + 15 يومًا؛ لا يقبل مدة أو بداية يدوية.
        migrationBuilder.Sql("UPDATE Announcements SET DurationDays = 15, DurationSetAt = NULL;");

        // العقد والترسية مستثناة كليًا من المدة العامة، وتستخدم مدة تنفيذ العقد فقط.
        migrationBuilder.Sql("UPDATE ContractAwards SET DurationDays = NULL, DurationSetAt = NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data-only normalization. لا يمكن استرجاع nulls القديمة دون اختراع بيانات.
    }
}
