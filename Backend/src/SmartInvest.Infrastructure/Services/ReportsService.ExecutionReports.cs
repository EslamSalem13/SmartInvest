using Microsoft.EntityFrameworkCore;
using SmartInvest.Domain.Enums;

namespace SmartInvest.Infrastructure.Services;

public partial class ReportsService
{
    private async Task<ReportWorkbookData> BuildContractsAndContractorsAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        var matchingProjectIds = BuildProjectsQuery(request).Select(project => project.SubProjectId);
        var query = _context.ContractAwards.AsNoTracking().Where(award => matchingProjectIds.Contains(award.SubProjectId));
        var today = DateTime.UtcNow.Date;
        if (request.OverdueOnly == true)
        {
            query = query.Where(award => award.ProjectAssignment != null
                                         && award.ProjectAssignment.ExpectedEndDate.Date < today
                                         && !_context.ExecutionStages.Any(stage => stage.SubProjectId == award.SubProjectId
                                                                                   && stage.IsFinalDelivery
                                                                                   && stage.IsCompleted));
        }

        var items = await query
            .OrderBy(award => award.SubProject.SubProjectName)
            .Select(award => new
            {
                ProjectCode = award.SubProject.SubProjectCode,
                ProjectName = award.SubProject.SubProjectName,
                Nature = award.SubProject.ProjectNature,
                Agency = award.SubProject.ExecutiveAgency == null ? null : award.SubProject.ExecutiveAgency.AgencyName,
                award.IsCompleted,
                ContractorId = award.ProjectAssignment == null ? null : award.ProjectAssignment.ContractorId,
                ContractorName = award.ProjectAssignment == null || award.ProjectAssignment.Contractor == null ? null : award.ProjectAssignment.Contractor.ContractorName,
                ContractorCategory = award.ProjectAssignment == null || award.ProjectAssignment.Contractor == null ? null : award.ProjectAssignment.Contractor.Category,
                ContractorActive = award.ProjectAssignment != null && award.ProjectAssignment.Contractor != null && award.ProjectAssignment.Contractor.IsActive,
                WillWorkAgain = award.ProjectAssignment == null || award.ProjectAssignment.Contractor == null ? null : award.ProjectAssignment.Contractor.WillWorkAgain,
                ContractType = award.ProjectAssignment == null ? null : award.ProjectAssignment.ContractType.ContractName,
                ContractNumber = award.ProjectAssignment == null ? null : award.ProjectAssignment.ContractNumber,
                ContractValue = award.ProjectAssignment == null ? null : award.ProjectAssignment.ContractValue,
                AssignmentDate = award.ProjectAssignment == null ? (DateTime?)null : award.ProjectAssignment.AssignmentDate,
                ExpectedStartDate = award.ProjectAssignment == null ? (DateTime?)null : award.ProjectAssignment.ExpectedStartDate,
                ExpectedEndDate = award.ProjectAssignment == null ? (DateTime?)null : award.ProjectAssignment.ExpectedEndDate,
                AssignmentLocked = award.ProjectAssignment != null && award.ProjectAssignment.IsLocked,
                award.AdvancePaymentDone,
                award.AdvancePaymentPercentage,
                award.AdvancePaymentBankAmount,
                award.AdvancePaymentSelfAmount,
                award.ExecutionDurationMonths,
                award.ExecutionDurationDays,
                award.SiteHandoverMode,
                award.SiteHandoverDate,
                HasSiteHandoverProof = award.SiteHandoverProofFile != null,
                award.PenaltyAmount,
                NotesCount = award.ProjectAssignment == null || award.ProjectAssignment.ContractorId == null
                    ? 0
                    : _context.ContractorNotes.Count(note => note.ContractorId == award.ProjectAssignment.ContractorId)
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);
        EnsureWithinRowCap(items.Count);

        var workbook = CreateWorkbook(request, "contracts-contractors");
        workbook.Columns = new List<ReportColumn>
        {
            Column("كود المشروع"), Column("اسم المشروع"), Column("نوع المشروع"), Column("الجهة التنفيذية"),
            Column("الترسية مكتملة", ReportColumnKind.Boolean), Column("المقاول"), Column("فئة المقاول"),
            Column("المقاول نشط", ReportColumnKind.Boolean), Column("نتعامل معه مجددًا"), Column("نوع العقد"),
            Column("رقم العقد"), Column("قيمة العقد", ReportColumnKind.MoneyThousands),
            Column("تاريخ الإسناد", ReportColumnKind.Date), Column("تاريخ البداية المتوقع", ReportColumnKind.Date),
            Column("تاريخ النهاية المتوقع", ReportColumnKind.Date), Column("الإسناد مقفول", ReportColumnKind.Boolean),
            Column("صُرفت دفعة مقدمة", ReportColumnKind.Boolean), Column("نسبة الدفعة المقدمة", ReportColumnKind.Percentage),
            Column("دفعة مقدمة بنكية", ReportColumnKind.MoneyThousands), Column("دفعة مقدمة ذاتية", ReportColumnKind.MoneyThousands),
            Column("مدة التنفيذ بالشهور", ReportColumnKind.Integer), Column("مدة التنفيذ بالأيام", ReportColumnKind.Integer),
            Column("وضع تسليم الأرضية"), Column("تاريخ تسليم الأرضية", ReportColumnKind.Date),
            Column("إثبات تسليم الأرضية", ReportColumnKind.Boolean), Column("تاريخ التسليم التعاقدي", ReportColumnKind.Date),
            Column("الشرط الجزائي", ReportColumnKind.MoneyThousands), Column("عدد ملاحظات المقاول", ReportColumnKind.Integer)
        };

        foreach (var item in items)
        {
            DateTime? contractualDeliveryDate = null;
            if (item.SiteHandoverDate.HasValue)
            {
                contractualDeliveryDate = item.SiteHandoverDate.Value
                    .AddMonths(item.ExecutionDurationMonths ?? 0)
                    .AddDays(item.ExecutionDurationDays ?? 0);
            }

            var handoverMode = item.SiteHandoverMode switch
            {
                SiteHandoverMode.AtAward => "وقت الترسية",
                SiteHandoverMode.Pending => "لاحقًا",
                _ => "غير محدد"
            };
            var willWorkAgain = item.WillWorkAgain.HasValue ? (item.WillWorkAgain.Value ? "نعم" : "لا") : "لم يُقيّم";
            workbook.Rows.Add(new object?[]
            {
                item.ProjectCode, item.ProjectName, item.Nature, item.Agency, item.IsCompleted,
                item.ContractorName, item.ContractorCategory, item.ContractorActive, willWorkAgain,
                item.ContractType, item.ContractNumber, item.ContractValue, item.AssignmentDate,
                item.ExpectedStartDate, item.ExpectedEndDate, item.AssignmentLocked, item.AdvancePaymentDone,
                item.AdvancePaymentPercentage, item.AdvancePaymentBankAmount, item.AdvancePaymentSelfAmount,
                item.ExecutionDurationMonths, item.ExecutionDurationDays, handoverMode, item.SiteHandoverDate,
                item.HasSiteHandoverProof, contractualDeliveryDate, item.PenaltyAmount, item.NotesCount
            });
        }

        AddMoneySummary(workbook, "إجمالي قيم العقود", items.Sum(item => item.ContractValue ?? 0));
        workbook.Summary.Add(new KeyValuePair<string, object?>("عقود وترسيات مكتملة", items.Count(item => item.IsCompleted).ToString("N0")));
        return workbook;
    }

    private async Task<ReportWorkbookData> BuildExecutionDelaysAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        var matchingProjectIds = BuildProjectsQuery(request).Select(project => project.SubProjectId);
        var query = _context.ExecutionStages.AsNoTracking().Where(stage => matchingProjectIds.Contains(stage.SubProjectId));
        var today = DateTime.UtcNow.Date;
        if (request.OverdueOnly == true)
        {
            query = query.Where(stage => stage.Deadline.HasValue
                                         && stage.Deadline.Value.Date < today
                                         && (!stage.IsCompleted || stage.CompletedAt > stage.Deadline));
        }

        var items = await query
            .OrderBy(stage => stage.Deadline)
            .ThenBy(stage => stage.SubProject.SubProjectName)
            .Select(stage => new
            {
                ProjectCode = stage.SubProject.SubProjectCode,
                ProjectName = stage.SubProject.SubProjectName,
                Agency = stage.SubProject.ExecutiveAgency == null ? null : stage.SubProject.ExecutiveAgency.AgencyName,
                Status = stage.SubProject.Status.StatusName,
                StageName = stage.Name,
                stage.IsFinalDelivery,
                stage.Deadline,
                stage.IsCompleted,
                stage.CompletedAt,
                stage.PhysicalProgressPercent,
                stage.BankFundingSpent,
                stage.SelfFundingSpent,
                stage.PenaltyAmount,
                stage.PenaltyPaid,
                stage.Notes,
                HasBankProof = stage.BankFundingProofFile != null,
                HasSelfProof = stage.SelfFundingProofFile != null,
                HasProgressProof = stage.PhysicalProgressProofFile != null
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);
        EnsureWithinRowCap(items.Count);

        var workbook = CreateWorkbook(request, "execution-delays");
        workbook.Description += " تنبيه: عند اختيار سنة مالية، المصروف هو إجمالي عمر المشروع للمشروعات المرتبطة بهذه السنة لأن مرحلة التنفيذ غير مرتبطة بسنة مالية.";
        workbook.Columns = new List<ReportColumn>
        {
            Column("كود المشروع"), Column("اسم المشروع"), Column("الجهة التنفيذية"), Column("حالة المشروع"),
            Column("مرحلة التنفيذ"), Column("مرحلة تسليم نهائي", ReportColumnKind.Boolean),
            Column("الموعد النهائي", ReportColumnKind.Date), Column("مكتملة", ReportColumnKind.Boolean),
            Column("تاريخ الإكمال", ReportColumnKind.Date), Column("أيام التأخير", ReportColumnKind.Integer),
            Column("نسبة الإنجاز", ReportColumnKind.Percentage), Column("مصروف بنكي", ReportColumnKind.MoneyThousands),
            Column("مصروف ذاتي", ReportColumnKind.MoneyThousands), Column("إجمالي المصروف", ReportColumnKind.MoneyThousands),
            Column("قيمة الجزاء", ReportColumnKind.MoneyThousands), Column("الجزاء مدفوع", ReportColumnKind.Boolean),
            Column("إثبات بنكي", ReportColumnKind.Boolean), Column("إثبات ذاتي", ReportColumnKind.Boolean),
            Column("إثبات تقدم", ReportColumnKind.Boolean), Column("ملاحظات")
        };

        foreach (var item in items)
        {
            var delayDays = 0;
            if (item.Deadline.HasValue)
            {
                var comparisonDate = item.IsCompleted && item.CompletedAt.HasValue ? item.CompletedAt.Value.Date : today;
                delayDays = Math.Max(0, (comparisonDate - item.Deadline.Value.Date).Days);
            }

            workbook.Rows.Add(new object?[]
            {
                item.ProjectCode, item.ProjectName, item.Agency, item.Status, item.StageName, item.IsFinalDelivery,
                item.Deadline, item.IsCompleted, item.CompletedAt, delayDays, item.PhysicalProgressPercent,
                item.BankFundingSpent, item.SelfFundingSpent, item.BankFundingSpent + item.SelfFundingSpent,
                item.PenaltyAmount, item.PenaltyPaid, item.HasBankProof, item.HasSelfProof, item.HasProgressProof, item.Notes
            });
        }

        workbook.Summary.Add(new KeyValuePair<string, object?>("مراحل متأخرة", workbook.Rows.Count(row => Convert.ToInt32(row[9]) > 0).ToString("N0")));
        AddMoneySummary(workbook, "إجمالي المصروف", items.Sum(item => item.BankFundingSpent + item.SelfFundingSpent));
        workbook.Summary.Add(new KeyValuePair<string, object?>("ملاحظة المصروف", "المصروف المعروض يشمل عمر المشروع بالكامل"));
        return workbook;
    }
}
