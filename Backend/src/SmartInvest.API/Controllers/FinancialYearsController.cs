using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/financial-years")]
[Authorize]
public class FinancialYearsController : ControllerBase
{
    private readonly IFinancialYearService _financialYearService;

    public FinancialYearsController(IFinancialYearService financialYearService)
    {
        _financialYearService = financialYearService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FinancialYearDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _financialYearService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FinancialYearDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _financialYearService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.FinancialYearsManage)]
    public async Task<ActionResult<FinancialYearDto>> Create(CreateFinancialYearDto dto, CancellationToken cancellationToken)
    {
        var result = await _financialYearService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.FinancialYearsManage)]
    public async Task<ActionResult<FinancialYearDto>> Update(int id, UpdateFinancialYearDto dto, CancellationToken cancellationToken)
    {
        var result = await _financialYearService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.FinancialYearsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _financialYearService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
