using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/subprojects/{subProjectId:int}/assignments")]
[Authorize]
public class ProjectAssignmentsController : ControllerBase
{
    private readonly IProjectAssignmentService _assignmentService;

    public ProjectAssignmentsController(IProjectAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpGet]
    [HasPermission(Permissions.ProjectsEdit)]
    public async Task<ActionResult<IReadOnlyList<ProjectAssignmentDto>>> GetAll(int subProjectId, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.GetBySubProjectAsync(subProjectId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.ProjectsEdit)]
    public async Task<ActionResult<ProjectAssignmentDto>> Create(int subProjectId, CreateProjectAssignmentDto dto, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.CreateAsync(subProjectId, dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.ProjectsEdit)]
    public async Task<ActionResult<ProjectAssignmentDto>> Update(int subProjectId, int id, UpdateProjectAssignmentDto dto, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.UpdateGeneralAsync(subProjectId, id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.ProjectsDelete)]
    public async Task<IActionResult> Delete(int subProjectId, int id, CancellationToken cancellationToken)
    {
        await _assignmentService.DeleteAsync(subProjectId, id, cancellationToken);
        return NoContent();
    }
}
