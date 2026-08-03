using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/subprojects/{subProjectId:int}/measurement-values")]
[Authorize]
public class SubProjectMeasurementValuesController : ControllerBase
{
    private readonly IMeasurementService _measurementService;

    public SubProjectMeasurementValuesController(IMeasurementService measurementService)
    {
        _measurementService = measurementService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubProjectMeasurementValueDto>>> GetAll(int subProjectId, CancellationToken cancellationToken)
    {
        var result = await _measurementService.GetValuesForSubProjectAsync(subProjectId, cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> SetAll(int subProjectId, SetSubProjectMeasurementValuesDto dto, CancellationToken cancellationToken)
    {
        await _measurementService.SetValuesForSubProjectAsync(subProjectId, dto, cancellationToken);
        return NoContent();
    }
}
