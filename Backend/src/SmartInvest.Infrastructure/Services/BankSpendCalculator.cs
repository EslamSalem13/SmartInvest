using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Domain.Entities;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Services;

/// <summary>
/// حساب الصرف الفعلي من الرصيد البنكي المستلم لسنة مالية معينة — يشترك فيه BankAvailabilityService
/// (سجل الإتاحات، "إجمالي المتاح") وDashboardService (لوحة التحكم، نفس التسمية) حتى لا يتذبذب
/// نفس الرقم المعروض بنفس التسمية بين الشاشتين. أي تعديل مستقبلي على قواعد الخصم يجب أن يمر من هنا فقط.
///
/// <see cref="GetTotalAvailableAsync"/> هو نفس الرقم — وهو أيضًا **الحارس الوحيد** الذي يجب أن يمر منه
/// أي صرف بنكي جديد (مرحلة تنفيذ أو دفعة مقدمة) قبل حفظه: التحقق من عدم تجاوز سقف المشروع المخطط
/// (TotalCost × نسبة التجاوز) لا يكفي وحده، لأنه رقم خاص بالمشروع بينما "المتاح" رصيد فعلي مشترك بين
/// كل مشروعات السنة المالية — مشروع بسقفه الخاص سليم قد يُفرِغ رصيدًا بنكيًا لم يعد كافيًا لمشروع آخر.
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
    /// <param name="excludeContractAwardId">استبعاد ترسية بعينها من المجموع — تُستخدم عند التحقق من
    /// دفعة مقدمة قيد التعديل حتى لا تُحسب قيمتها القديمة المخزَّنة ضد قيمتها الجديدة المقترحة.</param>
    public static async Task<decimal> GetAdvancePaymentsSpentAsync(
        AppDbContext context, int financialYearId, CancellationToken cancellationToken, int? excludeContractAwardId = null)
    {
        var query = context.ContractAwards.AsNoTracking()
            .Where(a => a.AdvancePaymentDone
                && a.SubProject.FinancialYears.Any(fy => fy.FinancialYearId == financialYearId));
        if (excludeContractAwardId is int excludeAwardId)
        {
            query = query.Where(a => a.Id != excludeAwardId);
        }

        var candidates = await query
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
    /// <param name="excludeExecutionStageId">استبعاد مرحلة تنفيذ بعينها — تُستخدم عند تعديل مرحلة
    /// قائمة حتى لا تُحسب قيمتها القديمة المخزَّنة ضد قيمتها الجديدة المقترحة.</param>
    public static async Task<decimal> GetExecutionBankSpendAsync(
        AppDbContext context, int financialYearId, CancellationToken cancellationToken, int? excludeExecutionStageId = null)
    {
        var query = context.ExecutionStages.AsNoTracking()
            .Where(e => e.SubProjectFinancialYear != null && e.SubProjectFinancialYear.FinancialYearId == financialYearId);
        if (excludeExecutionStageId is int excludeStageId)
        {
            query = query.Where(e => e.ExecutionStageId != excludeStageId);
        }

        return await query.SumAsync(e => e.BankFundingSpent, cancellationToken);
    }

    /// <summary>إجمالي الإتاحات البنكية المستلمة لسنة مالية (بلا خصم أي صرف) — نفس الاستعلام المستخدم
    /// في BankAvailabilityService، مُستخرَج هنا ليُستهلك من GetTotalAvailableAsync بلا تكرار.</summary>
    public static async Task<decimal> GetTotalReceivedAsync(AppDbContext context, int financialYearId, CancellationToken cancellationToken)
    {
        return await context.BankAvailabilities.AsNoTracking()
            .Where(a => a.FinancialYearId == financialYearId)
            .SumAsync(a => (decimal?)a.Amount, cancellationToken) ?? 0m;
    }

    /// <summary>"المتاح" الفعلي لسنة مالية = الإتاحات المستلمة − كل صرف بنكي فعلي (دفعات مقدمة
    /// ومراحل تنفيذ) لهذه السنة. هذا هو الرقم الذي يجب أن يتحقق منه أي صرف بنكي جديد قبل الحفظ.</summary>
    public static async Task<decimal> GetTotalAvailableAsync(
        AppDbContext context,
        int financialYearId,
        CancellationToken cancellationToken,
        int? excludeExecutionStageId = null,
        int? excludeContractAwardId = null)
    {
        var received = await GetTotalReceivedAsync(context, financialYearId, cancellationToken);
        var advancesSpent = await GetAdvancePaymentsSpentAsync(context, financialYearId, cancellationToken, excludeContractAwardId);
        var executionSpent = await GetExecutionBankSpendAsync(context, financialYearId, cancellationToken, excludeExecutionStageId);
        return received - advancesSpent - executionSpent;
    }

    /// <summary>يحدد السنة المالية التي يُنسب إليها صرف الدفعة المقدمة لترسية معينة — نفس قاعدة
    /// GetAdvancePaymentsSpentAsync بالضبط (تاريخ الترسية = ContractDate إن وُجد وإلا AssignmentDate،
    /// وإلا أقدم سنة مالية مرتبطة بالمشروع كحل احتياطي) حتى لا تتعارض قاعدتا التحقق والاحتساب.</summary>
    public static async Task<int> ResolveAdvancePaymentFinancialYearIdAsync(
        AppDbContext context, int subProjectId, int? projectAssignmentId, CancellationToken cancellationToken)
    {
        var years = await context.Set<SubProjectFinancialYear>().AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId)
            .Select(x => new { x.FinancialYearId, x.FinancialYear.StartDate, x.FinancialYear.EndDate })
            .ToListAsync(cancellationToken);

        if (years.Count == 0)
        {
            throw new BusinessRuleException("المشروع غير مرتبط بأي سنة مالية");
        }

        DateTime? awardDate = null;
        if (projectAssignmentId is int assignmentId)
        {
            awardDate = await context.Set<ProjectAssignment>().AsNoTracking()
                .Where(a => a.AssignmentId == assignmentId)
                .Select(a => a.ContractDate ?? (DateTime?)a.AssignmentDate)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var ordered = years.OrderBy(y => y.StartDate).ThenBy(y => y.FinancialYearId).ToList();
        var owningYear = awardDate.HasValue
            ? ordered.FirstOrDefault(y => awardDate.Value >= y.StartDate && awardDate.Value <= y.EndDate)
            : null;

        return (owningYear ?? ordered.First()).FinancialYearId;
    }
}
