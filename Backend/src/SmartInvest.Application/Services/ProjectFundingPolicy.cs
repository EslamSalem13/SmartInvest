namespace SmartInvest.Application.Services;

/// <summary>
/// قاعدة عمل نقية: إن كانت قيمة العقد أقل من الإجمالي المخطط، يُوزَّع الفرق بينهما على مصدري
/// التمويل بالترتيب - يُخصم أولًا من التمويل الذاتي، وما تبقّى من الفرق (إن وُجد) يُخصم من
/// التمويل البنكي. تُستخدم لضبط "المتاح/المتبقي" الفعلي لكل مصدر بعد معرفة قيمة العقد الحقيقية،
/// بدل الاعتماد على المخطط الأصلي الذي قد يكون أعلى من الفعلي المتعاقد عليه.
/// مصدر وحيد لهذا الحساب - يُستدعى من ExecutionStageService (المتبقي في متابعة المشروعات)
/// وProcurementService (سقف الدفعة المقدمة عند الترسية وعند إكمالها) بدل تكراره في الثلاثة.
/// </summary>
public static class ProjectFundingPolicy
{
    public static (decimal AdjustedSelfFunding, decimal AdjustedBankFunding) ApplyContractSavings(
        decimal selfFunding, decimal bankFunding, decimal totalPlannedValue, decimal? contractValue)
    {
        // لا فرق للتوزيع إن لم تُحدَّد قيمة عقد صحيحة بعد، أو كانت تساوي/تتجاوز المخطط
        // (لا "وفرة" في هذه الحالة - العقد لا يقلّل من المتاح، وقد يتجاوزه ضمن نسبة التجاوز المسموحة
        // التي تُدار بقاعدة منفصلة في GetAllowedCeilingAsync وليست من شأن هذه الدالة).
        if (contractValue is not > 0m || contractValue.Value >= totalPlannedValue)
        {
            return (selfFunding, bankFunding);
        }

        var difference = totalPlannedValue - contractValue.Value;

        var selfDeduction = Math.Min(selfFunding, difference);
        var adjustedSelf = selfFunding - selfDeduction;

        var remainingDifference = difference - selfDeduction;
        var bankDeduction = Math.Min(bankFunding, remainingDifference);
        var adjustedBank = bankFunding - bankDeduction;

        return (adjustedSelf, adjustedBank);
    }
}
