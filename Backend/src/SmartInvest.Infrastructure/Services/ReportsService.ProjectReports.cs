using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.Common.Reports;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Services;

public partial class ReportsService
{
    private async Task<ReportWorkbookData> BuildProjectRegisterAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        var items = await BuildProjectsQuery(request)
            .OrderBy(project => project.MainProject.MainProjectName)
            .ThenBy(project => project.SubProjectName)
            .Select(project => new
            {
                Years = project.FinancialYears.Select(link => link.FinancialYear.Name).OrderBy(name => name).ToList(),
                MainCode = project.MainProject.MainProjectCode,
                MainName = project.MainProject.MainProjectName,
                SubCode = project.SubProjectCode,
                SubName = project.SubProjectName,
                MainProgram = project.MainProject.SubProgram.MainProgram.ProgramName,
                SubProgram = project.MainProject.SubProgram.SubProgramName,
                Nature = project.ProjectNature,
                Level = project.ProjectLevel.Name,
                Component = project.ComponentType.Name,
                AccountingUnit = project.AccountingUnit.Name,
                Governorate = project.Markaz.Governorate.GovernorateName,
                Markaz = project.Markaz.MarkazName,
                Agency = project.ExecutiveAgency == null ? null : project.ExecutiveAgency.AgencyName,
                Priority = project.Priority.Priority,
                Status = project.Status.StatusName,
                project.IsApproved,
                project.ApprovedAt,
                project.ApprovalCancellationReason,
                project.BankFunding,
                project.SelfFunding,
                project.OverrunPercentage,
                Description = project.ProjectDescription,
                Goal = project.ProjectGoal,
                project.SocialImpact,
                project.EconomicImpact,
                project.EnvironmentalImpact,
                project.GreenInvestmentLink,
                project.Latitude,
                project.Longitude
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);

        EnsureWithinRowCap(items.Count);
        var workbook = CreateWorkbook(request, "project-register");
        workbook.Columns = new List<ReportColumn>
        {
            Column("السنة المالية"), Column("كود المشروع الرئيسي"), Column("اسم المشروع الرئيسي"),
            Column("كود المشروع الفرعي"), Column("اسم المشروع الفرعي"), Column("البرنامج الرئيسي"),
            Column("البرنامج الفرعي"), Column("نوع المشروع"), Column("مستوى المشروع"),
            Column("المكون العيني"), Column("الوحدة الحسابية"), Column("المحافظة"), Column("المركز"),
            Column("الجهة التنفيذية"), Column("الأولوية"), Column("الحالة"), Column("معتمد", ReportColumnKind.Boolean),
            Column("تاريخ الاعتماد", ReportColumnKind.Date), Column("سبب الإلغاء أو التعثر"),
            Column("تمويل بنكي", ReportColumnKind.MoneyThousands), Column("تمويل ذاتي", ReportColumnKind.MoneyThousands),
            Column("إجمالي التمويل", ReportColumnKind.MoneyThousands), Column("نسبة التجاوز المسموحة", ReportColumnKind.Percentage),
            Column("الوصف"), Column("الهدف"), Column("الأثر الاجتماعي"), Column("الأثر الاقتصادي"),
            Column("الأثر البيئي"), Column("رابط الاستثمار الأخضر"), Column("خط العرض", ReportColumnKind.Decimal),
            Column("خط الطول", ReportColumnKind.Decimal)
        };
        workbook.Rows = items.Select(item => new object?[]
        {
            string.Join("، ", item.Years), item.MainCode, item.MainName, item.SubCode, item.SubName,
            item.MainProgram, item.SubProgram, item.Nature, item.Level, item.Component, item.AccountingUnit,
            item.Governorate, item.Markaz, item.Agency, item.Priority, item.Status, item.IsApproved,
            item.ApprovedAt, item.ApprovalCancellationReason, item.BankFunding, item.SelfFunding,
            item.BankFunding + item.SelfFunding, item.OverrunPercentage, item.Description, item.Goal,
            item.SocialImpact, item.EconomicImpact, item.EnvironmentalImpact, item.GreenInvestmentLink,
            item.Latitude, item.Longitude
        }).ToList();
        AddMoneySummary(workbook, "إجمالي التمويل", items.Sum(item => item.BankFunding + item.SelfFunding));
        workbook.Summary.Add(new KeyValuePair<string, object?>("المشروعات المعتمدة", items.Count(item => item.IsApproved).ToString("N0")));
        return workbook;
    }

    private async Task<ReportWorkbookData> BuildFundingVsSpendingAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        var projects = await BuildProjectsQuery(request)
            .OrderBy(project => project.SubProjectName)
            .Select(project => new
            {
                project.SubProjectId,
                Code = project.SubProjectCode,
                Name = project.SubProjectName,
                Program = project.MainProject.SubProgram.MainProgram.ProgramName,
                Agency = project.ExecutiveAgency == null ? null : project.ExecutiveAgency.AgencyName,
                Status = project.Status.StatusName,
                project.BankFunding,
                project.SelfFunding,
                project.OverrunPercentage
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);
        EnsureWithinRowCap(projects.Count);

        var ids = projects.Select(project => project.SubProjectId).ToList();
        var stageRows = await _context.ExecutionStages.AsNoTracking()
            .Where(stage => ids.Contains(stage.SubProjectId))
            .Select(stage => new
            {
                stage.SubProjectId,
                stage.ExecutionStageId,
                stage.CreatedAt,
                stage.IsFinalDelivery,
                stage.BankFundingSpent,
                stage.SelfFundingSpent,
                stage.PhysicalProgressPercent,
                stage.PenaltyAmount,
                stage.PenaltyPaid
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);
        EnsureWithinRowCap(stageRows.Count);
        var advancePayments = await _context.ContractAwards.AsNoTracking()
            .Where(award => ids.Contains(award.SubProjectId))
            .Select(award => new
            {
                award.SubProjectId,
                Bank = award.AdvancePaymentBankAmount ?? 0,
                Self = award.AdvancePaymentSelfAmount ?? 0
            })
            .ToDictionaryAsync(item => item.SubProjectId, cancellationToken);

        var workbook = CreateWorkbook(request, "funding-vs-spending");
        workbook.Description += " تنبيه: عند اختيار سنة مالية، المصروف هو إجمالي عمر المشروع للمشروعات المرتبطة بهذه السنة لأن مرحلة التنفيذ غير مرتبطة بسنة مالية.";
        workbook.Columns = new List<ReportColumn>
        {
            Column("كود المشروع"), Column("اسم المشروع"), Column("البرنامج"), Column("الجهة التنفيذية"), Column("الحالة"),
            Column("تمويل بنكي", ReportColumnKind.MoneyThousands), Column("تمويل ذاتي", ReportColumnKind.MoneyThousands),
            Column("إجمالي التمويل", ReportColumnKind.MoneyThousands), Column("مصروف بنكي", ReportColumnKind.MoneyThousands),
            Column("مصروف ذاتي", ReportColumnKind.MoneyThousands), Column("إجمالي المصروف", ReportColumnKind.MoneyThousands),
            Column("الدفعة المقدمة", ReportColumnKind.MoneyThousands), Column("المتبقي", ReportColumnKind.MoneyThousands),
            Column("نسبة الصرف", ReportColumnKind.Percentage), Column("آخر تقدم عيني", ReportColumnKind.Percentage),
            Column("نسبة التجاوز المسموحة", ReportColumnKind.Percentage), Column("جزاءات غير مدفوعة", ReportColumnKind.MoneyThousands),
            Column("جزاءات مدفوعة", ReportColumnKind.MoneyThousands)
        };

        foreach (var project in projects)
        {
            var stages = stageRows.Where(stage => stage.SubProjectId == project.SubProjectId).ToList();
            var bankSpent = stages.Sum(stage => stage.BankFundingSpent);
            var selfSpent = stages.Sum(stage => stage.SelfFundingSpent);
            var totalFunding = project.BankFunding + project.SelfFunding;
            var totalSpent = bankSpent + selfSpent;
            var latestProgress = stages
                .Where(stage => !stage.IsFinalDelivery)
                .OrderByDescending(stage => stage.CreatedAt)
                .ThenByDescending(stage => stage.ExecutionStageId)
                .Select(stage => stage.PhysicalProgressPercent)
                .FirstOrDefault();
            var advance = advancePayments.GetValueOrDefault(project.SubProjectId);
            var advanceTotal = advance == null ? 0 : advance.Bank + advance.Self;
            workbook.Rows.Add(new object?[]
            {
                project.Code, project.Name, project.Program, project.Agency, project.Status,
                project.BankFunding, project.SelfFunding, totalFunding, bankSpent, selfSpent, totalSpent,
                advanceTotal, totalFunding - totalSpent, totalFunding <= 0 ? 0 : Math.Round(totalSpent / totalFunding * 100, 2),
                latestProgress, project.OverrunPercentage,
                stages.Where(stage => !stage.PenaltyPaid).Sum(stage => stage.PenaltyAmount ?? 0),
                stages.Where(stage => stage.PenaltyPaid).Sum(stage => stage.PenaltyAmount ?? 0)
            });
        }

        AddMoneySummary(workbook, "إجمالي تمويل المشروعات", projects.Sum(project => project.BankFunding + project.SelfFunding));
        workbook.Summary.Add(new KeyValuePair<string, object?>("ملاحظة المصروف", "المصروف المعروض يشمل عمر المشروع بالكامل"));
        return workbook;
    }

    private ReportWorkbookData CreateWorkbook(ReportRequestOptions request, string reportKey)
    {
        var definition = ReportCatalog.Find(reportKey) ?? throw new BusinessRuleException("تعريف التقرير غير موجود");
        return new ReportWorkbookData
        {
            Title = definition.Title,
            Description = definition.Description,
            FilterDescription = GetFilterDescription(request)
        };
    }
}
