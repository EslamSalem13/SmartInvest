using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartInvest.Application.Common.Ai;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.Common.Reports;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Services;

public partial class ReportsService : IReportsService
{
    private const int MaxPromptLength = 500;

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SortColumns =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["project-register"] = SortMap(("projectName", "اسم المشروع الفرعي"), ("totalFunding", "إجمالي التمويل")),
            ["funding-vs-spending"] = SortMap(("projectName", "اسم المشروع"), ("totalFunding", "إجمالي التمويل"), ("spent", "إجمالي المصروف"), ("progress", "آخر تقدم عيني")),
            ["bank-availability-ledger"] = SortMap(("receivedDate", "تاريخ الاستلام"), ("value", "قيمة الإتاحة")),
            ["plan-approval-status"] = SortMap(("projectName", "اسم المشروع"), ("totalFunding", "إجمالي التمويل")),
            ["procurement-pipeline"] = SortMap(("projectName", "اسم المشروع"), ("progress", "المراحل المكتملة")),
            ["contracts-contractors"] = SortMap(("projectName", "اسم المشروع"), ("contractValue", "قيمة العقد"), ("deadline", "تاريخ النهاية المتوقع")),
            ["execution-delays"] = SortMap(("projectName", "اسم المشروع"), ("spent", "إجمالي المصروف"), ("progress", "نسبة الإنجاز"), ("deadline", "الموعد النهائي")),
            ["geographic-distribution"] = SortMap(("totalFunding", "إجمالي التمويل"), ("spent", "إجمالي المصروف"), ("progress", "متوسط الإنجاز")),
            ["program-agency-performance"] = SortMap(("totalFunding", "إجمالي التمويل"), ("spent", "إجمالي المصروف"), ("progress", "متوسط الإنجاز")),
            ["measurements-outcomes"] = SortMap(("projectName", "اسم المشروع"), ("value", "القيمة"), ("totalFunding", "إجمالي التمويل"))
        };

    private readonly AppDbContext _context;
    private readonly IAiGatewayClient _aiGatewayClient;
    private readonly ILogger<ReportsService> _logger;

    public ReportsService(AppDbContext context, IAiGatewayClient aiGatewayClient, ILogger<ReportsService> logger)
    {
        _context = context;
        _aiGatewayClient = aiGatewayClient;
        _logger = logger;
    }

    public IReadOnlyList<ReportCatalogItemDto> GetCatalog()
    {
        return ReportCatalog.GetAll()
            .Select(item => new ReportCatalogItemDto
            {
                Key = item.Key,
                Title = item.Title,
                Description = item.Description,
                IncludedFields = item.IncludedFields.ToList()
            })
            .ToList();
    }

    public async Task<FileDownloadDto> GenerateExcelAsync(string reportKey, int? financialYearId, CancellationToken cancellationToken = default)
    {
        var request = new ReportRequestOptions
        {
            ReportKey = NormalizeAndValidateReportKey(reportKey),
            FinancialYearId = financialYearId
        };

        await ResolveFinancialYearAsync(request, cancellationToken);
        return await GenerateInternalAsync(request, cancellationToken);
    }

    public async Task<FileDownloadDto> GenerateAiExcelAsync(string prompt, int? financialYearId, CancellationToken cancellationToken = default)
    {
        var normalizedPrompt = (prompt ?? string.Empty).Trim();
        if (normalizedPrompt.Length < 3)
        {
            throw new BusinessRuleException("اكتب وصفًا أوضح للتقرير المطلوب");
        }

        if (normalizedPrompt.Length > MaxPromptLength)
        {
            throw new BusinessRuleException($"وصف التقرير لا يجوز أن يتجاوز {MaxPromptLength} حرفًا");
        }

        var plan = await ParseAiPlanAsync(normalizedPrompt, cancellationToken);
        var request = ValidateAiPlan(plan);
        if (financialYearId.HasValue)
        {
            request.FinancialYearId = financialYearId;
            request.FinancialYearName = null;
        }

        await ResolveFinancialYearAsync(request, cancellationToken);
        return await GenerateInternalAsync(request, cancellationToken);
    }

    private async Task<FileDownloadDto> GenerateInternalAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        ReportWorkbookData workbook = request.ReportKey switch
        {
            "project-register" => await BuildProjectRegisterAsync(request, cancellationToken),
            "funding-vs-spending" => await BuildFundingVsSpendingAsync(request, cancellationToken),
            "bank-availability-ledger" => await BuildBankAvailabilityLedgerAsync(request, cancellationToken),
            "plan-approval-status" => await BuildPlanApprovalStatusAsync(request, cancellationToken),
            "procurement-pipeline" => await BuildProcurementPipelineAsync(request, cancellationToken),
            "contracts-contractors" => await BuildContractsAndContractorsAsync(request, cancellationToken),
            "execution-delays" => await BuildExecutionDelaysAsync(request, cancellationToken),
            "geographic-distribution" => await BuildGeographicDistributionAsync(request, cancellationToken),
            "program-agency-performance" => await BuildProgramAgencyPerformanceAsync(request, cancellationToken),
            "measurements-outcomes" => await BuildMeasurementsOutcomesAsync(request, cancellationToken),
            _ => throw new BusinessRuleException("نوع التقرير المطلوب غير مدعوم")
        };

        ApplyRequestedSort(workbook, request);
        return new FileDownloadDto
        {
            FileName = $"smartinvest-{request.ReportKey}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx",
            FileExtension = ".xlsx",
            Content = ReportExcelBuilder.Build(workbook)
        };
    }

    private IQueryable<SubProject> BuildProjectsQuery(ReportRequestOptions request)
    {
        var query = _context.SubProjects.AsNoTracking().AsQueryable();
        if (request.FinancialYearId.HasValue)
        {
            var yearId = request.FinancialYearId.Value;
            query = query.Where(project => project.FinancialYears.Any(link => link.FinancialYearId == yearId));
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectNature))
        {
            query = query.Where(project => project.ProjectNature == request.ProjectNature);
        }

        if (!string.IsNullOrWhiteSpace(request.StatusName))
        {
            query = query.Where(project => project.Status.StatusName == request.StatusName);
        }

        if (!string.IsNullOrWhiteSpace(request.MarkazName))
        {
            query = query.Where(project => project.Markaz.MarkazName == request.MarkazName);
        }

        if (request.ApprovedOnly.HasValue)
        {
            var approved = request.ApprovedOnly.Value;
            query = query.Where(project => project.IsApproved == approved);
        }

        return query;
    }

    private async Task<AiReportPlan> ParseAiPlanAsync(string prompt, CancellationToken cancellationToken)
    {
        var keys = string.Join(", ", ReportCatalog.GetAll().Select(item => item.Key));
        var catalog = string.Join(Environment.NewLine, ReportCatalog.GetAll()
            .Select(item => $"- {item.Key}: {item.Title} — {item.Description}"));
        var systemPrompt = $$"""
            أنت مخطط تقارير لنظام إدارة مشروعات حكومية. حوّل طلب المستخدم إلى JSON فقط، ولا تكتب SQL مطلقًا.
            reportKey يجب أن يكون واحدًا فقط من: {{keys}}.
            استخدم هذا الكتالوج لاختيار أقرب تقرير لطلب المستخدم:
            {{catalog}}
            filters اختيارية ومفاتيحها الوحيدة: financialYearName, projectNature, statusName, markazName,
            approvedOnly, overdueOnly. projectNature إن وُجدت تكون «توريدات» أو «مقاولات» فقط.
            سجل الإتاحات البنكية يدعم financialYearName فقط. overdueOnly مسموح فقط مع
            contracts-contractors أو execution-delays. لا تُضف أي عامل تصفية غير مناسب للتقرير المختار.
            sortBy اختياري ويكون واحدًا من: projectName, totalFunding, spent, progress, deadline,
            receivedDate, contractValue, value. sortDirection اختياري ويكون asc أو desc فقط.
            الشكل الحرفي: {"reportKey":"...","filters":{"financialYearName":null,"projectNature":null,
            "statusName":null,"markazName":null,"approvedOnly":null,"overdueOnly":null},
            "sortBy":null,"sortDirection":"asc"}
            لا تضف Markdown أو تعليقًا أو مفاتيح أخرى. لا تخترع قيمًا لم يطلبها المستخدم.
            """;

        var response = await _aiGatewayClient.CompleteAsync(systemPrompt, prompt, 700, cancellationToken);
        try
        {
            var json = AiResponseParsing.StripMarkdownFences(response);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };
            return JsonSerializer.Deserialize<AiReportPlan>(json, options)
                   ?? throw new BusinessRuleException("لم تتمكن خدمة الذكاء الاصطناعي من تحديد التقرير المطلوب");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI report plan failed strict JSON validation");
            throw new BusinessRuleException("تعذّر فهم اقتراح خدمة الذكاء الاصطناعي. حاول وصف التقرير بصورة أبسط");
        }
    }

    private ReportRequestOptions ValidateAiPlan(AiReportPlan plan)
    {
        var reportKey = NormalizeAndValidateReportKey(plan.ReportKey);
        var filters = plan.Filters ?? new AiReportFilters();
        ValidateOptionalText(filters.FinancialYearName, "السنة المالية", 30);
        ValidateOptionalText(filters.StatusName, "حالة المشروع", 100);
        ValidateOptionalText(filters.MarkazName, "اسم المركز", 150);

        if (!string.IsNullOrWhiteSpace(filters.ProjectNature)
            && filters.ProjectNature is not "توريدات" and not "مقاولات")
        {
            throw new BusinessRuleException("نوع المشروع الذي اقترحه الذكاء الاصطناعي غير صالح");
        }

        var sortDirection = string.IsNullOrWhiteSpace(plan.SortDirection) ? "asc" : plan.SortDirection.Trim().ToLowerInvariant();
        if (sortDirection is not "asc" and not "desc")
        {
            throw new BusinessRuleException("اتجاه ترتيب التقرير الذي اقترحه الذكاء الاصطناعي غير صالح");
        }

        string? sortBy = null;
        if (!string.IsNullOrWhiteSpace(plan.SortBy))
        {
            sortBy = plan.SortBy.Trim();
            if (!SortColumns[reportKey].ContainsKey(sortBy))
            {
                throw new BusinessRuleException("طريقة الترتيب المطلوبة غير مدعومة لهذا التقرير");
            }
        }

        var request = new ReportRequestOptions
        {
            ReportKey = reportKey,
            FinancialYearName = NormalizeOptional(filters.FinancialYearName),
            ProjectNature = NormalizeOptional(filters.ProjectNature),
            StatusName = NormalizeOptional(filters.StatusName),
            MarkazName = NormalizeOptional(filters.MarkazName),
            ApprovedOnly = filters.ApprovedOnly,
            OverdueOnly = filters.OverdueOnly,
            SortBy = sortBy,
            SortDirection = sortDirection
        };
        ValidateFilterCompatibility(request);
        return request;
    }

    private static void ValidateFilterCompatibility(ReportRequestOptions request)
    {
        var projectFiltersRequested = request.ProjectNature != null
                                      || request.StatusName != null
                                      || request.MarkazName != null
                                      || request.ApprovedOnly.HasValue;
        if (request.ReportKey == "bank-availability-ledger" && projectFiltersRequested)
        {
            throw new BusinessRuleException("سجل الإتاحات البنكية يدعم التصفية بالسنة المالية فقط؛ احذف عوامل تصفية المشروعات أو اختر تقريرًا آخر");
        }

        if (request.OverdueOnly == true
            && request.ReportKey is not "contracts-contractors" and not "execution-delays")
        {
            throw new BusinessRuleException("تصفية العناصر المتأخرة متاحة فقط في تقريري العقود أو التنفيذ والتأخيرات");
        }
    }

    private async Task ResolveFinancialYearAsync(ReportRequestOptions request, CancellationToken cancellationToken)
    {
        if (request.FinancialYearId.HasValue)
        {
            var year = await _context.FinancialYears.AsNoTracking()
                .Where(item => item.FinancialYearId == request.FinancialYearId.Value)
                .Select(item => new { item.FinancialYearId, item.Name })
                .FirstOrDefaultAsync(cancellationToken);
            if (year == null)
            {
                throw new NotFoundException("السنة المالية المحددة غير موجودة");
            }

            request.FinancialYearName = year.Name;
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.FinancialYearName))
        {
            var normalizedName = request.FinancialYearName.Trim();
            var year = await _context.FinancialYears.AsNoTracking()
                .Where(item => item.Name == normalizedName)
                .Select(item => new { item.FinancialYearId, item.Name })
                .FirstOrDefaultAsync(cancellationToken);
            if (year == null)
            {
                throw new BusinessRuleException($"السنة المالية «{normalizedName}» غير موجودة");
            }

            request.FinancialYearId = year.FinancialYearId;
            request.FinancialYearName = year.Name;
        }
    }

    private static string NormalizeAndValidateReportKey(string reportKey)
    {
        var normalized = (reportKey ?? string.Empty).Trim().ToLowerInvariant();
        if (ReportCatalog.Find(normalized) == null)
        {
            throw new BusinessRuleException("نوع التقرير المطلوب غير موجود ضمن قائمة التقارير المتاحة");
        }

        return normalized;
    }

    private static void ValidateOptionalText(string? value, string label, int maxLength)
    {
        if (value != null && value.Trim().Length > maxLength)
        {
            throw new BusinessRuleException($"قيمة {label} المقترحة أطول من الحد المسموح");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyDictionary<string, string> SortMap(params (string Key, string Header)[] items)
    {
        return items.ToDictionary(item => item.Key, item => item.Header, StringComparer.OrdinalIgnoreCase);
    }

    private static ReportColumn Column(string header, ReportColumnKind kind = ReportColumnKind.Text)
    {
        return new ReportColumn { Header = header, Kind = kind };
    }

    private static void EnsureWithinRowCap(int count)
    {
        if (count > ReportExcelBuilder.MaxRows)
        {
            throw new BusinessRuleException($"التقرير يحتوي على أكثر من {ReportExcelBuilder.MaxRows:N0} صف. برجاء اختيار سنة مالية أو تضييق عوامل التصفية.");
        }
    }

    private static string GetFilterDescription(ReportRequestOptions request)
    {
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.FinancialYearName))
        {
            filters.Add($"السنة المالية: {request.FinancialYearName}");
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectNature))
        {
            filters.Add($"نوع المشروع: {request.ProjectNature}");
        }

        if (!string.IsNullOrWhiteSpace(request.StatusName))
        {
            filters.Add($"الحالة: {request.StatusName}");
        }

        if (!string.IsNullOrWhiteSpace(request.MarkazName))
        {
            filters.Add($"المركز: {request.MarkazName}");
        }

        if (request.ApprovedOnly.HasValue)
        {
            filters.Add(request.ApprovedOnly.Value ? "المشروعات المعتمدة فقط" : "المشروعات المقترحة فقط");
        }

        if (request.OverdueOnly == true)
        {
            filters.Add("العناصر المتأخرة فقط");
        }

        return filters.Count == 0 ? "كل البيانات المتاحة" : string.Join(" — ", filters);
    }

    private static void AddMoneySummary(ReportWorkbookData workbook, string label, decimal value)
    {
        workbook.Summary.Add(new KeyValuePair<string, object?>(label, new ReportSummaryValue
        {
            Value = value,
            Kind = ReportColumnKind.MoneyThousands
        }));
    }

    private static void ApplyRequestedSort(ReportWorkbookData workbook, ReportRequestOptions request)
    {
        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            return;
        }

        var header = SortColumns[request.ReportKey][request.SortBy];
        var index = workbook.Columns.FindIndex(column => column.Header == header);
        if (index < 0)
        {
            throw new BusinessRuleException("تعذّر تطبيق ترتيب التقرير المطلوب");
        }

        var descending = request.SortDirection == "desc";
        workbook.Rows = descending
            ? workbook.Rows.OrderByDescending(row => row[index], ReportValueComparer.Instance).ToList()
            : workbook.Rows.OrderBy(row => row[index], ReportValueComparer.Instance).ToList();
    }

    private sealed class ReportRequestOptions
    {
        public string ReportKey { get; set; } = string.Empty;
        public int? FinancialYearId { get; set; }
        public string? FinancialYearName { get; set; }
        public string? ProjectNature { get; set; }
        public string? StatusName { get; set; }
        public string? MarkazName { get; set; }
        public bool? ApprovedOnly { get; set; }
        public bool? OverdueOnly { get; set; }
        public string? SortBy { get; set; }
        public string SortDirection { get; set; } = "asc";
    }

    private sealed class AiReportPlan
    {
        public string ReportKey { get; set; } = string.Empty;
        public AiReportFilters? Filters { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
    }

    private sealed class AiReportFilters
    {
        public string? FinancialYearName { get; set; }
        public string? ProjectNature { get; set; }
        public string? StatusName { get; set; }
        public string? MarkazName { get; set; }
        public bool? ApprovedOnly { get; set; }
        public bool? OverdueOnly { get; set; }
    }

    private sealed class ReportValueComparer : IComparer<object?>
    {
        public static ReportValueComparer Instance { get; } = new();

        public int Compare(object? left, object? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            if (left is IComparable comparable && left.GetType() == right.GetType())
            {
                return comparable.CompareTo(right);
            }

            return string.Compare(Convert.ToString(left), Convert.ToString(right), StringComparison.CurrentCulture);
        }
    }
}
