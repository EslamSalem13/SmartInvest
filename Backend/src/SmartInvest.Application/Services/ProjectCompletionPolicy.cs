using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Services;

/// <summary>Pure business rule used by both list projections and the completion command.</summary>
public static class ProjectCompletionPolicy
{
    public static ProjectCompletionEligibilityDto Evaluate(ProjectCompletionFacts facts)
    {
        var actualStages = facts.Stages.Where(x => !x.IsFinalDelivery).ToList();
        var physicalTotal = actualStages.Sum(x => x.PhysicalProgressPercent);
        var selfSpent = facts.StageSelfFundingSpent
            + (facts.AdvancePaymentDone ? facts.AdvancePaymentSelfAmount : 0m);
        var bankSpent = facts.StageBankFundingSpent
            + (facts.AdvancePaymentDone ? facts.AdvancePaymentBankAmount : 0m);
        var totalSpent = selfSpent + bankSpent;
        var overrun = Math.Max(0m, facts.OverrunPercentage);
        var validContractValue = facts.ContractValue is > 0m;
        var minimum = validContractValue ? facts.ContractValue : null;
        // السقف مبني على الإجمالي المخطط (TotalCost)، لا قيمة العقد — نفس الغلاف المشترك المستخدم
        // في التحقق عند الترسية (ProcurementService) وعند الصرف أثناء التنفيذ (GetAllowedCeilingAsync).
        // الحد الأدنى يبقى قيمة العقد نفسها (مؤشر اكتمال الصرف الفعلي)، فهو غرض مختلف عن سقف التجاوز.
        var maximum = validContractValue ? (decimal?)(facts.TotalCost * (1m + overrun / 100m)) : null;
        var hasExecutionStages = actualStages.Count > 0;
        var hasFinalDeliveryStage = facts.Stages.Any(x => x.IsFinalDelivery);
        var allStagesCompleted = facts.Stages.Count > 0 && facts.Stages.All(x => x.IsCompleted);
        var blockers = new List<string>();

        if (!facts.IsContractAwardCompleted)
            blockers.Add("مرحلة العقد والترسية غير مكتملة.");
        if (!hasExecutionStages)
            blockers.Add("لا توجد مراحل تنفيذ فعلية.");
        if (facts.IsContractAwardCompleted && !hasFinalDeliveryStage)
            blockers.Add("مرحلة التسليم الأولي التلقائية غير موجودة.");
        if (!allStagesCompleted)
            blockers.Add("توجد مراحل تنفيذ غير مكتملة.");
        if (physicalTotal != 100m)
            blockers.Add($"مجموع التنفيذ العيني {physicalTotal:0.##}% ويجب أن يساوي 100%.");
        if (!validContractValue)
            blockers.Add("لا توجد قيمة عقد صحيحة مسجلة.");
        else if (totalSpent < minimum!.Value)
            blockers.Add("إجمالي المنصرف أقل من قيمة العقد.");
        else if (totalSpent > maximum!.Value)
            blockers.Add("إجمالي المنصرف يتجاوز قيمة العقد مع نسبة التجاوز المسموحة.");

        return new ProjectCompletionEligibilityDto
        {
            IsProjectCompleted = facts.IsProjectCompleted,
            CanCompleteProject = !facts.IsProjectCompleted && blockers.Count == 0,
            ContractValue = facts.ContractValue,
            SelfFundingSpent = selfSpent,
            BankFundingSpent = bankSpent,
            TotalSpent = totalSpent,
            OverrunPercentage = overrun,
            MinimumRequired = minimum,
            MaximumAllowed = maximum,
            PhysicalProgressTotal = physicalTotal,
            AllStagesCompleted = allStagesCompleted,
            HasExecutionStages = hasExecutionStages,
            Blockers = blockers,
        };
    }
}
