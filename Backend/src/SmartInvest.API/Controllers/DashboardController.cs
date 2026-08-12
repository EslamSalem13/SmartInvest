using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

/// <summary>إحصائيات لوحة التحكم لكل الأدوار الوظيفية — قراءة فقط.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.AllStaff)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> GetOverview([FromQuery] int? financialYearId, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetOverviewAsync(financialYearId, cancellationToken);
        return Ok(result);
    }
}
