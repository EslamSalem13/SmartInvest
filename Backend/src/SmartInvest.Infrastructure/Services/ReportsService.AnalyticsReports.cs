using Microsoft.EntityFrameworkCore;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Services;

public partial class ReportsService
{
    private async Task<ReportWorkbookData> BuildGeographicDistributionAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        var projects = await BuildProjectsQuery(request)
            .Select(project => new
            {
                project.SubProjectId,
                Governorate = project.Markaz.Governorate.GovernorateName,
                Markaz = project.Markaz.MarkazName,
                project.IsApproved,
                Status = project.Status.StatusName,
                project.BankFunding,
                project.SelfFunding,
                HasCoordinates = project.Latitude.HasValue && project.Longitude.HasValue
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
                stage.IsCompleted,
                stage.Deadline,
                stage.PhysicalProgressPercent,
                stage.BankFundingSpent,
                stage.SelfFundingSpent
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);
        EnsureWithinRowCap(stageRows.Count);
        var today = DateTime.UtcNow.Date;
        var workbook = CreateWorkbook(request, "geographic-distribution");
        workbook.Description += " المصروف عند التصفية بسنة مالية يمثل إجمالي عمر المشروعات المرتبطة بها.";
        workbook.Columns = new List<ReportColumn>
        {
            Column("المحافظة"), Column("المركز"), Column("إجمالي المشروعات", ReportColumnKind.Integer),
            Column("المعتمدة", ReportColumnKind.Integer), Column("المقترحة", ReportColumnKind.Integer),
            Column("قيد التنفيذ", ReportColumnKind.Integer), Column("المنتهية", ReportColumnKind.Integer),
            Column("تمويل بنكي", ReportColumnKind.MoneyThousands), Column("تمويل ذاتي", ReportColumnKind.MoneyThousands),
            Column("إجمالي التمويل", ReportColumnKind.MoneyThousands), Column("إجمالي المصروف", ReportColumnKind.MoneyThousands),
            Column("متوسط الإنجاز", ReportColumnKind.Percentage), Column("مراحل متأخرة", ReportColumnKind.Integer),
            Column("تغطية الإحداثيات", ReportColumnKind.Percentage)
        };

        foreach (var group in projects.GroupBy(project => new { project.Governorate, project.Markaz }).OrderBy(group => group.Key.Governorate).ThenBy(group => group.Key.Markaz))
        {
            var projectIds = group.Select(project => project.SubProjectId).ToHashSet();
            var stages = stageRows.Where(stage => projectIds.Contains(stage.SubProjectId)).ToList();
            var spent = stages.Sum(stage => stage.BankFundingSpent + stage.SelfFundingSpent);
            var progressValues = group.Select(project => stages
                    .Where(stage => stage.SubProjectId == project.SubProjectId && !stage.IsFinalDelivery)
                    .OrderByDescending(stage => stage.CreatedAt)
                    .ThenByDescending(stage => stage.ExecutionStageId)
                    .Select(stage => stage.PhysicalProgressPercent)
                    .FirstOrDefault())
                .ToList();
            workbook.Rows.Add(new object?[]
            {
                group.Key.Governorate, group.Key.Markaz, group.Count(), group.Count(project => project.IsApproved),
                group.Count(project => !project.IsApproved), group.Count(project => project.Status == "قيد التنفيذ"),
                group.Count(project => project.Status == "منتهي"), group.Sum(project => project.BankFunding),
                group.Sum(project => project.SelfFunding), group.Sum(project => project.BankFunding + project.SelfFunding),
                spent, progressValues.Count == 0 ? 0 : Math.Round(progressValues.Average(), 2),
                stages.Count(stage => !stage.IsCompleted && stage.Deadline.HasValue && stage.Deadline.Value.Date < today),
                group.Count() == 0 ? 0 : Math.Round((decimal)group.Count(project => project.HasCoordinates) / group.Count() * 100, 2)
            });
        }

        workbook.Summary.Add(new KeyValuePair<string, object?>("عدد المراكز", workbook.Rows.Count.ToString("N0")));
        AddMoneySummary(workbook, "إجمالي التمويل", projects.Sum(project => project.BankFunding + project.SelfFunding));
        return workbook;
    }

    private async Task<ReportWorkbookData> BuildProgramAgencyPerformanceAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        var projects = await BuildProjectsQuery(request)
            .Select(project => new
            {
                project.SubProjectId,
                MainProgram = project.MainProject.SubProgram.MainProgram.ProgramName,
                SubProgram = project.MainProject.SubProgram.SubProgramName,
                Agency = project.ExecutiveAgency == null ? "غير محددة" : project.ExecutiveAgency.AgencyName,
                project.ProjectNature,
                project.IsApproved,
                Status = project.Status.StatusName,
                project.BankFunding,
                project.SelfFunding
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
                stage.IsCompleted,
                stage.Deadline,
                stage.PhysicalProgressPercent,
                stage.BankFundingSpent,
                stage.SelfFundingSpent
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);
        EnsureWithinRowCap(stageRows.Count);
        var awardedIds = await _context.ContractAwards.AsNoTracking()
            .Where(award => ids.Contains(award.SubProjectId) && award.IsCompleted)
            .Select(award => award.SubProjectId)
            .ToListAsync(cancellationToken);
        var awardedSet = awardedIds.ToHashSet();
        var today = DateTime.UtcNow.Date;
        var workbook = CreateWorkbook(request, "program-agency-performance");
        workbook.Description += " المصروف عند التصفية بسنة مالية يمثل إجمالي عمر المشروعات المرتبطة بها.";
        workbook.Columns = new List<ReportColumn>
        {
            Column("البرنامج الرئيسي"), Column("البرنامج الفرعي"), Column("الجهة التنفيذية"),
            Column("إجمالي المشروعات", ReportColumnKind.Integer), Column("توريدات", ReportColumnKind.Integer),
            Column("مقاولات", ReportColumnKind.Integer), Column("المعتمدة", ReportColumnKind.Integer),
            Column("المقترحة", ReportColumnKind.Integer), Column("قيد التنفيذ", ReportColumnKind.Integer),
            Column("المنتهية", ReportColumnKind.Integer), Column("إجمالي التمويل", ReportColumnKind.MoneyThousands),
            Column("إجمالي المصروف", ReportColumnKind.MoneyThousands), Column("نسبة الصرف", ReportColumnKind.Percentage),
            Column("متوسط الإنجاز", ReportColumnKind.Percentage), Column("مراحل متأخرة", ReportColumnKind.Integer),
            Column("عقود مكتملة", ReportColumnKind.Integer)
        };

        foreach (var group in projects.GroupBy(project => new { project.MainProgram, project.SubProgram, project.Agency })
                     .OrderBy(group => group.Key.MainProgram).ThenBy(group => group.Key.SubProgram).ThenBy(group => group.Key.Agency))
        {
            var projectIds = group.Select(project => project.SubProjectId).ToHashSet();
            var stages = stageRows.Where(stage => projectIds.Contains(stage.SubProjectId)).ToList();
            var funding = group.Sum(project => project.BankFunding + project.SelfFunding);
            var spent = stages.Sum(stage => stage.BankFundingSpent + stage.SelfFundingSpent);
            var progress = group.Select(project => stages
                    .Where(stage => stage.SubProjectId == project.SubProjectId && !stage.IsFinalDelivery)
                    .OrderByDescending(stage => stage.CreatedAt)
                    .ThenByDescending(stage => stage.ExecutionStageId)
                    .Select(stage => stage.PhysicalProgressPercent)
                    .FirstOrDefault())
                .ToList();
            workbook.Rows.Add(new object?[]
            {
                group.Key.MainProgram, group.Key.SubProgram, group.Key.Agency, group.Count(),
                group.Count(project => project.ProjectNature == "توريدات"), group.Count(project => project.ProjectNature == "مقاولات"),
                group.Count(project => project.IsApproved), group.Count(project => !project.IsApproved),
                group.Count(project => project.Status == "قيد التنفيذ"), group.Count(project => project.Status == "منتهي"),
                funding, spent, funding <= 0 ? 0 : Math.Round(spent / funding * 100, 2),
                progress.Count == 0 ? 0 : Math.Round(progress.Average(), 2),
                stages.Count(stage => !stage.IsCompleted && stage.Deadline.HasValue && stage.Deadline.Value.Date < today),
                group.Count(project => awardedSet.Contains(project.SubProjectId))
            });
        }

        workbook.Summary.Add(new KeyValuePair<string, object?>("مجموعات البرنامج والجهة", workbook.Rows.Count.ToString("N0")));
        AddMoneySummary(workbook, "إجمالي التمويل", projects.Sum(project => project.BankFunding + project.SelfFunding));
        return workbook;
    }

    private async Task<ReportWorkbookData> BuildMeasurementsOutcomesAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        var matchingProjectIds = BuildProjectsQuery(request).Select(project => project.SubProjectId);
        var items = await _context.Set<SubProjectMeasurementValue>().AsNoTracking()
            .Where(item => matchingProjectIds.Contains(item.SubProjectId))
            .OrderBy(item => item.SubProject.SubProjectName)
            .ThenBy(item => item.Measurement.Name)
            .Select(item => new
            {
                Years = item.SubProject.FinancialYears.Select(link => link.FinancialYear.Name).OrderBy(name => name).ToList(),
                ProjectCode = item.SubProject.SubProjectCode,
                ProjectName = item.SubProject.SubProjectName,
                MainProgram = item.SubProject.MainProject.SubProgram.MainProgram.ProgramName,
                SubProgram = item.SubProject.MainProject.SubProgram.SubProgramName,
                Governorate = item.SubProject.Markaz.Governorate.GovernorateName,
                Markaz = item.SubProject.Markaz.MarkazName,
                Measurement = item.Measurement.Name,
                Unit = item.Unit.Name,
                item.Value,
                item.SubProject.ProjectNature,
                item.SubProject.BankFunding,
                item.SubProject.SelfFunding
            })
            .Take(ReportExcelBuilder.MaxRows + 1)
            .ToListAsync(cancellationToken);
        EnsureWithinRowCap(items.Count);

        var workbook = CreateWorkbook(request, "measurements-outcomes");
        workbook.Columns = new List<ReportColumn>
        {
            Column("السنة المالية"), Column("كود المشروع"), Column("اسم المشروع"), Column("البرنامج الرئيسي"),
            Column("البرنامج الفرعي"), Column("المحافظة"), Column("المركز"), Column("اسم القياس"),
            Column("الوحدة"), Column("القيمة", ReportColumnKind.Decimal), Column("نوع المشروع"),
            Column("تمويل بنكي", ReportColumnKind.MoneyThousands), Column("تمويل ذاتي", ReportColumnKind.MoneyThousands),
            Column("إجمالي التمويل", ReportColumnKind.MoneyThousands)
        };
        workbook.Rows = items.Select(item => new object?[]
        {
            string.Join("، ", item.Years), item.ProjectCode, item.ProjectName, item.MainProgram, item.SubProgram,
            item.Governorate, item.Markaz, item.Measurement, item.Unit, item.Value, item.ProjectNature,
            item.BankFunding, item.SelfFunding, item.BankFunding + item.SelfFunding
        }).ToList();
        workbook.Summary.Add(new KeyValuePair<string, object?>("عدد القياسات المسجلة", items.Count.ToString("N0")));
        workbook.Summary.Add(new KeyValuePair<string, object?>("المشروعات ذات القياسات", items.Select(item => item.ProjectName).Distinct().Count().ToString("N0")));
        return workbook;
    }
}
