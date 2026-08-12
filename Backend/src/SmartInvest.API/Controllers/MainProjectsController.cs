using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.DTOs.Common;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/mainprojects")]
[Authorize]
public class MainProjectsController : ControllerBase
{
    private readonly IMainProjectService _mainProjectService;

    public MainProjectsController(IMainProjectService mainProjectService)
    {
        _mainProjectService = mainProjectService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<MainProjectListItemDto>>> GetAll(
        [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var effectivePage = page <= 0 ? 1 : page;
        var effectivePageSize = pageSize <= 0 ? 2000 : pageSize;

        var result = await _mainProjectService.GetAllAsync(effectivePage, effectivePageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MainProjectDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _mainProjectService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<MainProjectDetailDto>> Create(CreateMainProjectDto dto, CancellationToken cancellationToken)
    {
        var result = await _mainProjectService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<MainProjectDetailDto>> Update(int id, UpdateMainProjectDto dto, CancellationToken cancellationToken)
    {
        var result = await _mainProjectService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.ManagementStaff)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _mainProjectService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
