using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.API.Common;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

/// <summary>سجل الإتاحات البنكية لكل سنة مالية — سجل تاريخي، بلا تعديل أو حذف.</summary>
[ApiController]
[Authorize]
[Route("api/financial-years/{financialYearId:int}/bank-availabilities")]
public class BankAvailabilitiesController : ControllerBase
{
    private readonly IBankAvailabilityService _bankAvailabilityService;

    public BankAvailabilitiesController(IBankAvailabilityService bankAvailabilityService)
    {
        _bankAvailabilityService = bankAvailabilityService;
    }

    [HttpGet]
    public async Task<ActionResult<BankAvailabilityListDto>> GetForFinancialYear(int financialYearId, CancellationToken cancellationToken)
    {
        var result = await _bankAvailabilityService.GetForFinancialYearAsync(financialYearId, cancellationToken);
        return Ok(result);
    }

    /// <summary>multipart/form-data: amount, receivedDate, notes (اختياري) + مستند إثبات واحد أو أكثر.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<BankAvailabilityDto>> Create(
        int financialYearId,
        [FromForm] decimal amount,
        [FromForm] DateTime receivedDate,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        var dto = new CreateBankAvailabilityDto
        {
            Amount = amount,
            ReceivedDate = receivedDate,
            Notes = notes,
        };

        foreach (var file in Request.Form.Files)
        {
            if (file.Length > 0)
            {
                dto.Documents.Add(await FileRequestHelpers.ToUploadDtoAsync(file, cancellationToken));
            }
        }

        var result = await _bankAvailabilityService.CreateAsync(financialYearId, dto, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{availabilityId:int}/documents/{documentId:int}")]
    public async Task<IActionResult> DownloadDocument(int financialYearId, int availabilityId, int documentId, CancellationToken cancellationToken)
    {
        var file = await _bankAvailabilityService.DownloadDocumentAsync(financialYearId, availabilityId, documentId, cancellationToken);
        return File(file.Content, FileRequestHelpers.GetContentType(file.FileExtension), file.FileName);
    }
}
