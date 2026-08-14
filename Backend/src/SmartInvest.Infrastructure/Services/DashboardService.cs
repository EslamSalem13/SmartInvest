using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Services;

/// <summary>
/// إحصائيات لوحة التحكم لمدير التخطيط. يستخدم AppDbContext مباشرة (نفس نمط ProcurementService)
/// لأن كل مؤشر يحتاج تجميعًا عبر عدة جداول لمشروعات نفس السنة المالية. يقرأ فقط، بلا كتابة.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;

    public DashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync(int? financialYearId, CancellationToken cancellationToken = default)
    {
        var year = financialYearId.HasValue
            ? await _context.FinancialYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.FinancialYearId == financialYearId.Value, cancellationToken)
                ?? throw new NotFoundException($"السنة المالية رقم {financialYearId} غير موجودة")
            : await _context.FinancialYears.AsNoTracking()
                .OrderByDescending(y => y.StartDate)
                .FirstOrDefaultAsync(cancellationToken);

        if (year == null)
        {
            throw new NotFoundException("لا توجد سنوات مالية مسجلة بعد");
        }

        var subProjects = await _context.SubProjects.AsNoTracking()
            .Where(s => s.FinancialYears.Any(fy => fy.FinancialYearId == year.FinancialYearId))
            .Select(s => new SubProjectProjection
            {
                SubProjectId = s.SubProjectId,
                SubProjectName = s.SubProjectName,
                SubProjectCode = s.SubProjectCode,
                MainProjectName = s.MainProject.MainProjectName,
                ProgramName = s.MainProject.SubProgram.MainProgram.ProgramName,
                MarkazName = s.Markaz.MarkazName,
                PriorityName = s.Priority.Priority,
                StatusName = s.Status.StatusName,
                IsApproved = s.IsApproved,
                ExecutionCompletedAt = s.ExecutionCompletedAt,
                BankFunding = s.BankFunding,
                SelfFunding = s.SelfFunding,
            })
            .ToListAsync(cancellationToken);

        var subProjectIds = subProjects.Select(s => s.SubProjectId).ToList();

        var stages = await _context.ExecutionStages.AsNoTracking()
            .Where(x => subProjectIds.Contains(x.SubProjectId)
                && x.SubProjectFinancialYear != null
                && x.SubProjectFinancialYear.FinancialYearId == year.FinancialYearId)
            .Select(x => new ExecutionStageProjection
            {
                ExecutionStageId = x.ExecutionStageId,
                SubProjectId = x.SubProjectId,
                Name = x.Name,
                Deadline = x.Deadline,
                IsCompleted = x.IsCompleted,
                IsFinalDelivery = x.IsFinalDelivery,
                CreatedAt = x.CreatedAt,
                PhysicalProgressPercent = x.PhysicalProgressPercent,
                SelfFundingSpent = x.SelfFundingSpent,
                BankFundingSpent = x.BankFundingSpent,
            })
            .ToListAsync(cancellationToken);

        var stagesBySubProject = stages
            .GroupBy(x => x.SubProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var availabilities = await _context.BankAvailabilities.AsNoTracking()
            .Where(a => a.FinancialYearId == year.FinancialYearId)
            .Select(a => new { a.BankAvailabilityId, a.Amount, a.ReceivedDate, a.CreatedAt })
            .ToListAsync(cancellationToken);

        decimal GetPhysicalProgress(int subProjectId)
        {
            if (!stagesBySubProject.TryGetValue(subProjectId, out var list))
            {
                return 0;
            }

            return list
                .Where(x => !x.IsFinalDelivery)
                .Sum(x => x.PhysicalProgressPercent);
        }

        bool IsCompletedInSelectedYear(SubProjectProjection subProject)
        {
            if (!subProject.IsApproved
                || subProject.StatusName != "منتهي"
                || subProject.ExecutionCompletedAt == null
                || !stagesBySubProject.TryGetValue(subProject.SubProjectId, out var projectStages))
            {
                return false;
            }

            var actualStages = projectStages.Where(x => !x.IsFinalDelivery).ToList();
            return actualStages.Count > 0
                && projectStages.Any(x => x.IsFinalDelivery)
                && projectStages.All(x => x.IsCompleted)
                && actualStages.Sum(x => x.PhysicalProgressPercent) == 100m;
        }

        DashboardProjectBriefDto ToProjectBrief(SubProjectProjection s)
        {
            return new DashboardProjectBriefDto
            {
                SubProjectId = s.SubProjectId,
                SubProjectName = s.SubProjectName,
                SubProjectCode = s.SubProjectCode,
                MainProjectName = s.MainProjectName,
                TotalCost = s.BankFunding + s.SelfFunding,
                IsApproved = s.IsApproved,
            };
        }

        var totalSubProjects = subProjects.Count;
        var approvedList = subProjects.Where(s => s.IsApproved).ToList();
        var stalledCount = subProjects.Count(s => s.IsApproved && s.StatusName == "متعثر");
        var approvedCount = subProjects.Count(s => s.IsApproved && s.StatusName != "متعثر");
        var proposedCount = subProjects.Count(s => !s.IsApproved);
        var completedProjectIds = subProjects
            .Where(IsCompletedInSelectedYear)
            .Select(s => s.SubProjectId)
            .ToHashSet();
        var completedCount = completedProjectIds.Count;
        var inProgressProjectIds = subProjects
            .Where(s => s.IsApproved
                && s.StatusName != "متعثر"
                && !completedProjectIds.Contains(s.SubProjectId)
                && (s.StatusName == "قيد التنفيذ"
                    || (stagesBySubProject.TryGetValue(s.SubProjectId, out var projectStages)
                        && projectStages.Any(x => !x.IsFinalDelivery))))
            .Select(s => s.SubProjectId)
            .ToHashSet();
        var inProgressCount = inProgressProjectIds.Count;
        var approvalRate = totalSubProjects == 0
            ? 0
            : Math.Round((decimal)subProjects.Count(s => s.IsApproved) / totalSubProjects * 100, 2);
        var averagePhysicalProgress = approvedList.Count == 0
            ? 0
            : Math.Round(approvedList.Average(s => Math.Min(100m, GetPhysicalProgress(s.SubProjectId))), 2);

        var projectMetrics = new DashboardProjectMetricsDto
        {
            TotalSubProjects = totalSubProjects,
            ApprovedCount = approvedCount,
            ProposedCount = proposedCount,
            StalledCount = stalledCount,
            ApprovalRate = approvalRate,
            CompletedCount = completedCount,
            InProgressCount = inProgressCount,
            AveragePhysicalProgress = averagePhysicalProgress,
        };

        var bankFunding = subProjects.Sum(s => s.BankFunding);
        var selfFunding = subProjects.Sum(s => s.SelfFunding);
        var totalFunding = bankFunding + selfFunding;
        // صافٍ من الصرف (دفعات مقدمة + صرف تنفيذ) بنفس حساب BankAvailabilityService.GetForFinancialYearAsync —
        // حتى لا يختلف رقم "إجمالي المتاح" بين لوحة التحكم وشاشة المشروعات لنفس السنة المالية (BankSpendCalculator المشترك).
        var advancesSpent = await BankSpendCalculator.GetAdvancePaymentsSpentAsync(_context, year.FinancialYearId, cancellationToken);
        var executionSpent = await BankSpendCalculator.GetExecutionBankSpendAsync(_context, year.FinancialYearId, cancellationToken);
        var receipts = availabilities.Sum(a => a.Amount);
        var totalBankAvailabilities = receipts - advancesSpent - executionSpent;
        // المتبقي للبنك سؤال عن الإيداعات (receipts) لا عن الصرف — يطابق RemainingAvailable في BankAvailabilityService عمدًا.
        var remainingAvailable = bankFunding - receipts;
        var availabilityRate = bankFunding <= 0 ? 0 : Math.Round(totalBankAvailabilities / bankFunding * 100, 2);
        var bankSpent = stages.Sum(x => x.BankFundingSpent);
        var selfSpent = stages.Sum(x => x.SelfFundingSpent);
        var totalSpent = bankSpent + selfSpent;
        var spentRate = totalFunding <= 0 ? 0 : Math.Round(totalSpent / totalFunding * 100, 2);

        var financialMetrics = new DashboardFinancialMetricsDto
        {
            TotalFunding = totalFunding,
            BankFunding = bankFunding,
            SelfFunding = selfFunding,
            TotalBankAvailabilities = totalBankAvailabilities,
            RemainingAvailableToBank = remainingAvailable,
            AvailabilityRateOfBankFunding = availabilityRate,
            BankSpent = bankSpent,
            SelfSpent = selfSpent,
            TotalSpent = totalSpent,
            SpentRateOfTotalFunding = spentRate,
        };

        var statusDistribution = new List<DashboardNamedValueDto>
        {
            new() { Name = "معتمد", Value = subProjects.Count(s => s.IsApproved
                && s.StatusName != "متعثر"
                && !completedProjectIds.Contains(s.SubProjectId)
                && !inProgressProjectIds.Contains(s.SubProjectId)) },
            new() { Name = "مقترح", Value = proposedCount },
            new() { Name = "متعثر", Value = stalledCount },
            new() { Name = "جاري التنفيذ", Value = inProgressCount },
            new() { Name = "منتهي", Value = completedCount },
        };

        var priorityDistribution = subProjects
            .GroupBy(s => string.IsNullOrWhiteSpace(s.PriorityName) ? "غير محدد" : s.PriorityName)
            .Select(g => new DashboardNamedValueDto { Name = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .ToList();

        var markazDistribution = subProjects
            .GroupBy(s => string.IsNullOrWhiteSpace(s.MarkazName) ? "غير محدد" : s.MarkazName)
            .Select(g => new DashboardNamedValueDto { Name = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .ToList();

        var programFunding = subProjects
            .GroupBy(s => string.IsNullOrWhiteSpace(s.ProgramName) ? "غير محدد" : s.ProgramName)
            .Select(g => new DashboardProgramFundingDto
            {
                ProgramName = g.Key,
                ProjectCount = g.Count(),
                BankFunding = g.Sum(x => x.BankFunding),
                SelfFunding = g.Sum(x => x.SelfFunding),
                TotalFunding = g.Sum(x => x.BankFunding + x.SelfFunding),
            })
            .OrderByDescending(x => x.TotalFunding)
            .ToList();

        var progressBucketNames = new[] { "0%", "1–25%", "26–50%", "51–75%", "76–99%", "100%", "أكثر من 100%" };
        var progressBucketCounts = new int[7];
        foreach (var s in approvedList)
        {
            var p = GetPhysicalProgress(s.SubProjectId);
            var bucketIndex = p <= 0 ? 0 : p <= 25 ? 1 : p <= 50 ? 2 : p <= 75 ? 3 : p < 100 ? 4 : p == 100 ? 5 : 6;
            progressBucketCounts[bucketIndex]++;
        }
        var progressDistribution = progressBucketNames
            .Select((name, i) => new DashboardNamedValueDto { Name = name, Value = progressBucketCounts[i] })
            .ToList();

        var availabilityTimeline = new List<DashboardAvailabilityPointDto>();
        var cumulativeAmount = 0m;
        foreach (var a in availabilities.OrderBy(a => a.ReceivedDate).ThenBy(a => a.CreatedAt))
        {
            cumulativeAmount += a.Amount;
            availabilityTimeline.Add(new DashboardAvailabilityPointDto
            {
                ReceivedDate = a.ReceivedDate,
                Amount = a.Amount,
                CumulativeAmount = cumulativeAmount,
            });
        }

        var charts = new DashboardChartsDto
        {
            FundingDistribution =
            [
                new DashboardNamedValueDto { Name = "بنكي", Value = bankFunding },
                new DashboardNamedValueDto { Name = "ذاتي", Value = selfFunding },
            ],
            StatusDistribution = statusDistribution,
            PriorityDistribution = priorityDistribution,
            MarkazDistribution = markazDistribution,
            ProgramFunding = programFunding,
            ProgressDistribution = progressDistribution,
            AvailabilityTimeline = availabilityTimeline,
        };

        var today = DateTime.UtcNow.Date;
        var subProjectNameById = subProjects.ToDictionary(s => s.SubProjectId, s => s.SubProjectName);

        var details = new DashboardDetailsDto
        {
            RecentAvailabilities = availabilities
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .Select(a => new DashboardAvailabilityBriefDto { Id = a.BankAvailabilityId, Amount = a.Amount, ReceivedDate = a.ReceivedDate })
                .ToList(),
            // لا يوجد حقل تاريخ إنشاء على SubProject، فتُستخدم SubProjectId تنازليًا كمؤشر تقريبي للأحدث.
            RecentProjects = subProjects
                .OrderByDescending(s => s.SubProjectId)
                .Take(5)
                .Select(ToProjectBrief)
                .ToList(),
            TopFundedProjects = subProjects
                .OrderByDescending(s => s.BankFunding + s.SelfFunding)
                .Take(5)
                .Select(ToProjectBrief)
                .ToList(),
            OverdueStages = stages
                .Where(x => !x.IsCompleted && x.Deadline != null && x.Deadline.Value.Date < today)
                .OrderBy(x => x.Deadline)
                .Take(5)
                .Select(x => new DashboardStageBriefDto
                {
                    ExecutionStageId = x.ExecutionStageId,
                    SubProjectId = x.SubProjectId,
                    SubProjectName = subProjectNameById.GetValueOrDefault(x.SubProjectId, string.Empty),
                    StageName = x.Name,
                    Deadline = x.Deadline,
                })
                .ToList(),
            StalledProjects = subProjects
                .Where(s => s.IsApproved && s.StatusName == "متعثر")
                .Take(5)
                .Select(ToProjectBrief)
                .ToList(),
            PendingApprovalProjects = subProjects
                .Where(s => !s.IsApproved)
                .Take(5)
                .Select(ToProjectBrief)
                .ToList(),
        };

        return new DashboardOverviewDto
        {
            Year = new DashboardYearDto
            {
                FinancialYearId = year.FinancialYearId,
                FinancialYearName = year.Name,
                StartDate = year.StartDate,
                EndDate = year.EndDate,
                IsClosed = year.IsClosed,
            },
            ProjectMetrics = projectMetrics,
            FinancialMetrics = financialMetrics,
            Charts = charts,
            Details = details,
        };
    }

    /// <summary>إسقاط خفيف لصفوف المشروعات الفرعية — يتجنب تحميل الكيان الكامل والملاحات الثقيلة لكل مشروع.</summary>
    private sealed class SubProjectProjection
    {
        public int SubProjectId { get; set; }
        public string SubProjectName { get; set; } = string.Empty;
        public string? SubProjectCode { get; set; }
        public string MainProjectName { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public string MarkazName { get; set; } = string.Empty;
        public string PriorityName { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public DateTime? ExecutionCompletedAt { get; set; }
        public decimal BankFunding { get; set; }
        public decimal SelfFunding { get; set; }
    }

    /// <summary>إسقاط خفيف لمراحل التنفيذ — يتجنب تحميل بايتات ملفات الإثبات (varbinary(max)).</summary>
    private sealed class ExecutionStageProjection
    {
        public int ExecutionStageId { get; set; }
        public int SubProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? Deadline { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsFinalDelivery { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal PhysicalProgressPercent { get; set; }
        public decimal SelfFundingSpent { get; set; }
        public decimal BankFundingSpent { get; set; }
    }
}
