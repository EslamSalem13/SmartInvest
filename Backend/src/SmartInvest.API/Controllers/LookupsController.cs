using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/lookups")]
[Authorize]
public class LookupsController : ControllerBase
{
    private readonly ILookupService _lookupService;

    public LookupsController(ILookupService lookupService)
    {
        _lookupService = lookupService;
    }

    [HttpGet("priorities")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetPriorities(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetPrioritiesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("priorities")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreatePriority(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreatePriorityAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("priorities/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdatePriority(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdatePriorityAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("priorities/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeletePriority(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeletePriorityAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("statuses")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetStatuses(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetStatusesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("statuses")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreateStatus(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateStatusAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("statuses/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdateStatus(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateStatusAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("statuses/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteStatus(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteStatusAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("main-programs")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetMainPrograms(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetMainProgramsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("main-programs")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreateMainProgram(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateMainProgramAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("main-programs/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdateMainProgram(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateMainProgramAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("main-programs/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteMainProgram(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteMainProgramAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("sub-programs")]
    public async Task<ActionResult<IReadOnlyList<SubProgramLookupDto>>> GetSubPrograms([FromQuery] int? mainProgramId, CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetSubProgramsAsync(mainProgramId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sub-programs")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<SubProgramLookupDto>> CreateSubProgram(CreateSubProgramDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateSubProgramAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("sub-programs/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<SubProgramLookupDto>> UpdateSubProgram(int id, UpdateSubProgramDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateSubProgramAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("sub-programs/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteSubProgram(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteSubProgramAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("governorates")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetGovernorates(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetGovernoratesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("governorates")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreateGovernorate(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateGovernorateAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("governorates/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdateGovernorate(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateGovernorateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("governorates/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteGovernorate(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteGovernorateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("markaz")]
    public async Task<ActionResult<IReadOnlyList<MarkazLookupDto>>> GetMarkaz([FromQuery] int? governorateId, CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetMarkazAsync(governorateId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("markaz")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<MarkazLookupDto>> CreateMarkaz(CreateMarkazDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateMarkazAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("markaz/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<MarkazLookupDto>> UpdateMarkaz(int id, UpdateMarkazDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateMarkazAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("markaz/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteMarkaz(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteMarkazAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("villages")]
    public async Task<ActionResult<IReadOnlyList<VillageLookupDto>>> GetVillages([FromQuery] int? markazId, CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetVillagesAsync(markazId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("villages")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<VillageLookupDto>> CreateVillage(CreateVillageDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateVillageAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("villages/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<VillageLookupDto>> UpdateVillage(int id, UpdateVillageDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateVillageAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("villages/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteVillage(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteVillageAsync(id, cancellationToken);
        return NoContent();
    }
}
