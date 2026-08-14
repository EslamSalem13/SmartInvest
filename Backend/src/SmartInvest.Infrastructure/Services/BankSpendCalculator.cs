using Microsoft.EntityFrameworkCore;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Services;

/// <summary>
/// حساب الصرف الفعلي من الرصيد البنكي المستلم لسنة مالية معينة — يشترك فيه BankAvailabilityService
/// (سجل الإتاحات، "إجمالي المتاح") وDashboardService (لوحة التحكم، نفس التسمية) حتى لا يتذبذب
/// نفس الرقم المعروض بنفس التسمية بين الشاشتين. أي تعديل مستقبلي على قواعد الخصم يجب أن يمر من هنا فقط.
/// </summary>
internal static class BankSpendCalculator
{
    /// <summary>مجموع الدفعات المقدمة البنكية المصروفة فعليًا (AdvancePaymentDone) والمنسوبة لهذه السنة
    /// المالية تحديدًا. لكل ترسية دفعة مقدمة واحدة (AdvancePaymentBankAmount) بلا نسبة سنة مالية، بينما
    /// المشروع الفرعي قد يكون مرتبطًا بأكثر من سنة (مشروع ممتد). قرار صاحب المنتج: تُنسب الدفعة لسنة
    /// الترسية نفسها فقط (تاريخ الترسية = ContractDate إن وُجد وإلا AssignmentDate)، وتُحدَّد سنتها بأنها
    /// السنة المرتبطة بالمشروع التي يقع تاريخ الترسية داخل مداها [StartDate, EndDate]. إن لم يكن للترسية
    /// إسناد (ProjectAssignment) أصلًا، أو وقع التاريخ خارج مدى كل السنوات المرتبطة، تُنسب الدفعة لأقدم
    /// سنة مرتبطة (بحسب StartDate) كحل احتياطي — لضمان احتسابها مرة واحدة بالضبط، لا صفر مرات ولا مرتين.</summary>
    public static async Task<decimal> GetAdvancePaymentsSpentAsync(AppDbContext context, int financialYearId, CancellationToken cancellationToken)
    {
        var candidates = await context.ContractAwards.AsNoTracking()
            .Where(a => a.AdvancePaymentDone
                && a.SubProject.FinancialYears.Any(fy => fy.FinancialYearId == financialYearId))
            .Select(a => new
            {
                a.AdvancePaymentBankAmount,
                HasAssignment = a.ProjectAssignment != null,
                ContractDate = a.ProjectAssignment != null ? a.ProjectAssignment.ContractDate : null,
                AssignmentDate = a.ProjectAssignment != null ? (DateTime?)a.ProjectAssignment.AssignmentDate : null,
                Years = a.SubProject.FinancialYears
                    .Select(fy => new { fy.FinancialYearId, fy.FinancialYear.StartDate, fy.FinancialYear.EndDate })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        decimal total = 0m;
        foreach (var candidate in candidates)
        {
            if (candidate.Years.Count == 0)
            {
                continue;
            }

            var orderedYears = candidate.Years
                .OrderBy(y => y.StartDate)
                .ThenBy(y => y.FinancialYearId)
                .ToList();

            var awardDate = candidate.HasAssignment ? candidate.ContractDate ?? candidate.AssignmentDate : null;
            var owningYear = awardDate.HasValue
                ? orderedYears.FirstOrDefault(y => awardDate.Value >= y.StartDate && awardDate.Value <= y.EndDate)
                : null;
            owningYear ??= orderedYears.First();

            if (owningYear.FinancialYearId == financialYearId)
            {
                total += candidate.AdvancePaymentBankAmount ?? 0m;
            }
        }

        return total;
    }

    /// <summary>مجموع الصرف الفعلي من التمويل البنكي عبر مراحل التنفيذ لكل المشروعات الفرعية
    /// المرتبطة بهذه السنة المالية تحديدًا (وليس أي سنة أخرى للمشروع نفسه).</summary>
    public static async Task<decimal> GetExecutionBankSpendAsync(AppDbContext context, int financialYearId, CancellationToken cancellationToken)
    {
        return await context.ExecutionStages.AsNoTracking()
            .Where(e => e.SubProjectFinancialYear != null && e.SubProjectFinancialYear.FinancialYearId == financialYearId)
            .SumAsync(e => e.BankFundingSpent, cancellationToken);
    }
}
