using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.SuperAdmin)]
public class ReportsController : ControllerBase
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IReportsService _reportsService;

    public ReportsController(IReportsService reportsService)
    {
        _reportsService = reportsService;
    }

    [HttpGet("catalog")]
    public ActionResult<IReadOnlyList<ReportCatalogItemDto>> GetCatalog()
    {
        return Ok(_reportsService.GetCatalog());
    }

    [HttpGet("{reportKey}/excel")]
    public async Task<IActionResult> DownloadReport(
        string reportKey,
        [FromQuery] int? financialYearId,
        CancellationToken cancellationToken)
    {
        var file = await _reportsService.GenerateExcelAsync(reportKey, financialYearId, cancellationToken);
        return File(file.Content, ExcelContentType, file.FileName);
    }

    [HttpPost("ai/excel")]
    public async Task<IActionResult> DownloadAiReport(GenerateAiReportDto dto, CancellationToken cancellationToken)
    {
        var file = await _reportsService.GenerateAiExcelAsync(dto.Prompt, dto.FinancialYearId, cancellationToken);
        return File(file.Content, ExcelContentType, file.FileName);
    }
}
