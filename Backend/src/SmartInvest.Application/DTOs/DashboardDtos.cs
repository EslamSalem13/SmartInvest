namespace SmartInvest.Application.DTOs;

public class DashboardOverviewDto
{
    public DashboardYearDto Year { get; set; } = new();
    public DashboardProjectMetricsDto ProjectMetrics { get; set; } = new();
    public DashboardFinancialMetricsDto FinancialMetrics { get; set; } = new();
    public DashboardChartsDto Charts { get; set; } = new();
    public DashboardDetailsDto Details { get; set; } = new();
}

public class DashboardYearDto
{
    public int FinancialYearId { get; set; }
    public string FinancialYearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}

public class DashboardProjectMetricsDto
{
    public int TotalSubProjects { get; set; }
    public int ApprovedCount { get; set; }
    public int ProposedCount { get; set; }
    public int StalledCount { get; set; }
    public decimal ApprovalRate { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }

    /// <summary>
    /// متوسط نسبة التنفيذ العيني للمشروعات المعتمدة — بنفس منطق حساب PhysicalProgressPercent
    /// في صفحة متابعة المشروعات (قيمة آخر مرحلة تنفيذ غير نهائية)، وليس معادلة جديدة.
    /// </summary>
    public decimal AveragePhysicalProgress { get; set; }
}

public class DashboardFinancialMetricsDto
{
    public decimal TotalFunding { get; set; }
    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }
    public decimal TotalBankAvailabilities { get; set; }
    public decimal RemainingAvailableToBank { get; set; }
    public decimal AvailabilityRateOfBankFunding { get; set; }

    /// <summary>منصرف بنكي عبر مراحل تنفيذ مشروعات هذه السنة المالية فقط.</summary>
    public decimal BankSpent { get; set; }

    /// <summary>منصرف ذاتي عبر مراحل تنفيذ مشروعات هذه السنة المالية فقط.</summary>
    public decimal SelfSpent { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal SpentRateOfTotalFunding { get; set; }

    /// <summary>
    /// الوفرة: مجموع (إجمالي المخطط − قيمة العقد) لكل مشروع فرعي اكتمل تنفيذه ماليًا وعينيًا
    /// ضمن هذه السنة المالية تحديدًا (نفس مجموعة CompletedCount أعلاه) — موجبة تعني فائضًا،
    /// سالبة تعني عجزًا. لا تُحتسَب المشروعات غير المكتملة إطلاقًا.
    /// </summary>
    public decimal Savings { get; set; }
}

public class DashboardNamedValueDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class DashboardProgramFundingDto
{
    public string ProgramName { get; set; } = string.Empty;
    public int ProjectCount { get; set; }
    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }
    public decimal TotalFunding { get; set; }
}

public class DashboardAvailabilityPointDto
{
    public DateTime ReceivedDate { get; set; }
    public decimal Amount { get; set; }
    public decimal CumulativeAmount { get; set; }
}

public class DashboardChartsDto
{
    public List<DashboardNamedValueDto> FundingDistribution { get; set; } = new();
    public List<DashboardNamedValueDto> StatusDistribution { get; set; } = new();
    public List<DashboardNamedValueDto> PriorityDistribution { get; set; } = new();
    public List<DashboardNamedValueDto> MarkazDistribution { get; set; } = new();
    public List<DashboardProgramFundingDto> ProgramFunding { get; set; } = new();
    public List<DashboardNamedValueDto> ProgressDistribution { get; set; } = new();
    public List<DashboardAvailabilityPointDto> AvailabilityTimeline { get; set; } = new();
}

public class DashboardAvailabilityBriefDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime ReceivedDate { get; set; }
}

public class DashboardProjectBriefDto
{
    public int SubProjectId { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string? SubProjectCode { get; set; }
    public string MainProjectName { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public bool IsApproved { get; set; }
}

public class DashboardStageBriefDto
{
    public int ExecutionStageId { get; set; }
    public int SubProjectId { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
}

public class DashboardDetailsDto
{
    public List<DashboardAvailabilityBriefDto> RecentAvailabilities { get; set; } = new();
    public List<DashboardProjectBriefDto> RecentProjects { get; set; } = new();
    public List<DashboardProjectBriefDto> TopFundedProjects { get; set; } = new();
    public List<DashboardStageBriefDto> OverdueStages { get; set; } = new();
    public List<DashboardProjectBriefDto> StalledProjects { get; set; } = new();
    public List<DashboardProjectBriefDto> PendingApprovalProjects { get; set; } = new();
}
