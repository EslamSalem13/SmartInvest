using Microsoft.EntityFrameworkCore;
using SmartInvest.Domain.Enums;

namespace SmartInvest.Infrastructure.Services;

public partial class ReportsService
{
    private async Task<ReportWorkbookData> BuildBankAvailabilityLedgerAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        var query = _context.BankAvailabilities.AsNoTracking().AsQueryable();
        if (request.FinancialYearId.HasValue)
        {
            query = query.Where(item => item.FinancialYearId == request.FinancialYearId.Value);
        }

        var items = await query
            .OrderBy(item => item.FinancialYear.StartDate)
            .ThenBy(item => item.ReceivedDate)
            .ThenBy(item => item.CreatedAt)
            .Select(item => new
            {
                item.BankAvailabilityId,
                YearId = item.FinancialYearId,
                YearName = item.FinancialYear.Name,
                item.Amount,
                item.ReceivedDate,
                item.CreatedAt,
                item.Notes,
                CreatedBy = _context.Users.Where(user => user.Id == item.CreatedByUserId).Select(user => user.FullName).FirstOrDefault(),
                DocumentNames = item.Documents.Select(document => document.File.FileName).OrderBy(name => name).ToList(),
                YearBankFunding = item.FinancialYear.SubProjectFinancialYears.Sum(link => (decimal?)link.SubProject.BankFunding) ?? 0
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);
        EnsureWithinRowCap(items.Count);

        var yearTotals = items.GroupBy(item => item.YearId).ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
        var runningByYear = new Dictionary<int, decimal>();
        var workbook = CreateWorkbook(request, "bank-availability-ledger");
        workbook.Columns = new List<ReportColumn>
        {
            Column("السنة المالية"), Column("رقم الإتاحة", ReportColumnKind.Integer),
            Column("تاريخ الاستلام", ReportColumnKind.Date), Column("تاريخ التسجيل", ReportColumnKind.DateTime),
            Column("المنشئ"), Column("قيمة الإتاحة", ReportColumnKind.MoneyThousands),
            Column("الرصيد التراكمي", ReportColumnKind.MoneyThousands), Column("إجمالي تمويل بنكي للسنة", ReportColumnKind.MoneyThousands),
            Column("إجمالي إتاحات السنة", ReportColumnKind.MoneyThousands), Column("العجز أو المتبقي", ReportColumnKind.MoneyThousands),
            Column("نسبة الإتاحة", ReportColumnKind.Percentage), Column("عدد المستندات", ReportColumnKind.Integer),
            Column("أسماء المستندات"), Column("ملاحظات")
        };

        foreach (var item in items)
        {
            runningByYear[item.YearId] = runningByYear.GetValueOrDefault(item.YearId) + item.Amount;
            var totalAvailability = yearTotals[item.YearId];
            workbook.Rows.Add(new object?[]
            {
                item.YearName, item.BankAvailabilityId, item.ReceivedDate, item.CreatedAt, item.CreatedBy,
                item.Amount, runningByYear[item.YearId], item.YearBankFunding, totalAvailability,
                item.YearBankFunding - totalAvailability,
                item.YearBankFunding <= 0 ? 0 : Math.Round(totalAvailability / item.YearBankFunding * 100, 2),
                item.DocumentNames.Count, string.Join("، ", item.DocumentNames), item.Notes
            });
        }

        AddMoneySummary(workbook, "إجمالي الإتاحات", items.Sum(item => item.Amount));
        workbook.Summary.Add(new KeyValuePair<string, object?>("عدد مستندات الإثبات", items.Sum(item => item.DocumentNames.Count).ToString("N0")));
        return workbook;
    }

    private async Task<ReportWorkbookData> BuildPlanApprovalStatusAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        var matchingProjectIds = BuildProjectsQuery(request).Select(project => project.SubProjectId);
        var query = _context.PlanProjects.AsNoTracking().Where(link => matchingProjectIds.Contains(link.SubProjectId));
        if (request.FinancialYearId.HasValue)
        {
            query = query.Where(link => link.Plan.FinancialYearId == request.FinancialYearId.Value);
        }

        var items = await query
            .OrderBy(link => link.Plan.FinancialYear!.StartDate)
            .ThenBy(link => link.Plan.PlanName)
            .ThenBy(link => link.SubProject.SubProjectName)
            .Select(link => new
            {
                Year = link.Plan.FinancialYear!.Name,
                Plan = link.Plan.PlanName,
                link.Plan.PlanStatus,
                link.Plan.SuggestionDate,
                link.Plan.ApprovalDate,
                link.Plan.StartDate,
                link.Plan.EndDate,
                link.Plan.IsClosed,
                ProjectCode = link.SubProject.SubProjectCode,
                ProjectName = link.SubProject.SubProjectName,
                link.SubProject.IsApproved,
                link.SubProject.ApprovedAt,
                link.SubProject.ApprovalCancelledAt,
                link.SubProject.ApprovalCancellationReason,
                Status = link.SubProject.Status.StatusName,
                link.SubProject.BankFunding,
                link.SubProject.SelfFunding
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);
        EnsureWithinRowCap(items.Count);

        var workbook = CreateWorkbook(request, "plan-approval-status");
        workbook.Columns = new List<ReportColumn>
        {
            Column("السنة المالية"), Column("اسم الخطة"), Column("حالة الخطة"),
            Column("تاريخ الاقتراح", ReportColumnKind.Date), Column("تاريخ اعتماد الخطة", ReportColumnKind.Date),
            Column("بداية الخطة", ReportColumnKind.Date), Column("نهاية الخطة", ReportColumnKind.Date),
            Column("الخطة مغلقة", ReportColumnKind.Boolean), Column("كود المشروع"), Column("اسم المشروع"),
            Column("المشروع معتمد", ReportColumnKind.Boolean), Column("تاريخ اعتماد المشروع", ReportColumnKind.Date),
            Column("تاريخ إلغاء الاعتماد", ReportColumnKind.Date), Column("سبب الإلغاء أو التعثر"), Column("الحالة الحالية"),
            Column("تمويل بنكي", ReportColumnKind.MoneyThousands), Column("تمويل ذاتي", ReportColumnKind.MoneyThousands),
            Column("إجمالي التمويل", ReportColumnKind.MoneyThousands)
        };
        workbook.Rows = items.Select(item => new object?[]
        {
            item.Year, item.Plan, item.PlanStatus == PlanStatus.Approved ? "معتمدة" : "مقترحة",
            item.SuggestionDate, item.ApprovalDate, item.StartDate, item.EndDate, item.IsClosed,
            item.ProjectCode, item.ProjectName, item.IsApproved, item.ApprovedAt, item.ApprovalCancelledAt,
            item.ApprovalCancellationReason, item.Status, item.BankFunding, item.SelfFunding,
            item.BankFunding + item.SelfFunding
        }).ToList();
        workbook.Summary.Add(new KeyValuePair<string, object?>("المشروعات المعتمدة", items.Count(item => item.IsApproved).ToString("N0")));
        workbook.Summary.Add(new KeyValuePair<string, object?>("المشروعات المقترحة", items.Count(item => !item.IsApproved).ToString("N0")));
        return workbook;
    }

    private async Task<ReportWorkbookData> BuildProcurementPipelineAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        var items = await BuildProjectsQuery(request)
            .OrderBy(project => project.SubProjectName)
            .Select(project => new
            {
                project.SubProjectId,
                Code = project.SubProjectCode,
                Name = project.SubProjectName,
                Nature = project.ProjectNature,
                Agency = project.ExecutiveAgency == null ? null : project.ExecutiveAgency.AgencyName,
                MemoCount = _context.PresentationMemoSubProjects.Count(link => link.SubProjectId == project.SubProjectId),
                CompletedMemoCount = _context.PresentationMemoSubProjects.Count(link => link.SubProjectId == project.SubProjectId && link.PresentationMemo.IsCompleted),
                TenderDone = _context.TenderDocuments.Any(document => document.SubProjectId == project.SubProjectId && document.IsCompleted),
                TenderVersion = _context.TenderDocuments.Where(document => document.SubProjectId == project.SubProjectId).Select(document => document.CurrentVersionNumber).FirstOrDefault(),
                AnnouncementDone = _context.Announcements.Any(document => document.SubProjectId == project.SubProjectId && document.IsCompleted),
                AnnouncementVersion = _context.Announcements.Where(document => document.SubProjectId == project.SubProjectId).Select(document => document.CurrentVersionNumber).FirstOrDefault(),
                OpeningDone = _context.OpeningEnvelopes.Any(document => document.SubProjectId == project.SubProjectId && document.IsCompleted),
                OpeningVersion = _context.OpeningEnvelopes.Where(document => document.SubProjectId == project.SubProjectId).Select(document => document.CurrentVersionNumber).FirstOrDefault(),
                TechnicalDone = _context.TechnicalEvaluations.Any(document => document.SubProjectId == project.SubProjectId && document.IsCompleted),
                TechnicalVersion = _context.TechnicalEvaluations.Where(document => document.SubProjectId == project.SubProjectId).Select(document => document.CurrentVersionNumber).FirstOrDefault(),
                FinancialDone = _context.FinancialEvaluations.Any(document => document.SubProjectId == project.SubProjectId && document.IsCompleted),
                FinancialVersion = _context.FinancialEvaluations.Where(document => document.SubProjectId == project.SubProjectId).Select(document => document.CurrentVersionNumber).FirstOrDefault(),
                AwardDone = _context.ContractAwards.Any(document => document.SubProjectId == project.SubProjectId && document.IsCompleted),
                AwardVersion = _context.ContractAwards.Where(document => document.SubProjectId == project.SubProjectId).Select(document => document.CurrentVersionNumber).FirstOrDefault()
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);
        EnsureWithinRowCap(items.Count);

        var workbook = CreateWorkbook(request, "procurement-pipeline");
        workbook.Columns = new List<ReportColumn>
        {
            Column("كود المشروع"), Column("اسم المشروع"), Column("نوع المشروع"), Column("الجهة التنفيذية"),
            Column("عدد مذكرات العرض", ReportColumnKind.Integer), Column("مذكرات عرض مكتملة", ReportColumnKind.Integer),
            Column("كراسة الشروط مكتملة", ReportColumnKind.Boolean), Column("إصدار كراسة الشروط", ReportColumnKind.Integer),
            Column("الإعلان مكتمل", ReportColumnKind.Boolean), Column("إصدار الإعلان", ReportColumnKind.Integer),
            Column("فتح المظاريف مكتمل", ReportColumnKind.Boolean), Column("إصدار فتح المظاريف", ReportColumnKind.Integer),
            Column("التقييم الفني مكتمل", ReportColumnKind.Boolean), Column("إصدار التقييم الفني", ReportColumnKind.Integer),
            Column("التقييم المالي مكتمل", ReportColumnKind.Boolean), Column("إصدار التقييم المالي", ReportColumnKind.Integer),
            Column("العقد والترسية مكتمل", ReportColumnKind.Boolean), Column("إصدار العقد والترسية", ReportColumnKind.Integer),
            Column("المراحل المكتملة", ReportColumnKind.Integer), Column("المرحلة الحالية")
        };
        foreach (var item in items)
        {
            var completed = new[] { item.TenderDone, item.AnnouncementDone, item.OpeningDone, item.TechnicalDone, item.FinancialDone, item.AwardDone }.Count(done => done);
            var currentStage = completed switch
            {
                0 => "كراسة الشروط",
                1 => "الإعلان",
                2 => "فتح المظاريف",
                3 => "التقييم الفني",
                4 => "التقييم المالي",
                5 => "العقد والترسية",
                _ => "مكتمل"
            };
            workbook.Rows.Add(new object?[]
            {
                item.Code, item.Name, item.Nature, item.Agency, item.MemoCount, item.CompletedMemoCount,
                item.TenderDone, item.TenderVersion, item.AnnouncementDone, item.AnnouncementVersion,
                item.OpeningDone, item.OpeningVersion, item.TechnicalDone, item.TechnicalVersion,
                item.FinancialDone, item.FinancialVersion, item.AwardDone, item.AwardVersion, completed, currentStage
            });
        }

        workbook.Summary.Add(new KeyValuePair<string, object?>("دورات طرح مكتملة", items.Count(item => item.AwardDone).ToString("N0")));
        workbook.Summary.Add(new KeyValuePair<string, object?>("دورات طرح لم تكتمل", items.Count(item => !item.AwardDone).ToString("N0")));
        return workbook;
    }
}
