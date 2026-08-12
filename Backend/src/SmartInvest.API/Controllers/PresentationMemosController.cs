using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.API.Common;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

/// <summary>مذكرات العرض — مذكرة واحدة قد تغطي عدة مشروعات فرعية (M:N).</summary>
[ApiController]
[Route("api/presentation-memos")]
[Authorize]
public class PresentationMemosController : ControllerBase
{
    private readonly IPresentationMemoService _memoService;

    public PresentationMemosController(IPresentationMemoService memoService)
    {
        _memoService = memoService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PresentationMemoDto>>> GetAll([FromQuery] int? financialYearId, CancellationToken cancellationToken)
    {
        var result = await _memoService.GetAllAsync(financialYearId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PresentationMemoDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _memoService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
    public async Task<ActionResult<PresentationMemoDto>> Create(CreatePresentationMemoDto dto, CancellationToken cancellationToken)
    {
        var result = await _memoService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
    public async Task<ActionResult<PresentationMemoDto>> Update(int id, UpdatePresentationMemoDto dto, CancellationToken cancellationToken)
    {
        var result = await _memoService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.FinancialManagers)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _memoService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>رفع إصدار جديد — multipart/form-data: حقل file، وnotes وlegalAffairsCommitteeDecision اختياريان.</summary>
    [HttpPost("{id:int}/versions")]
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
    public async Task<ActionResult<ProcurementVersionDto>> UploadVersion(
        int id,
        IFormFile? file,
        IFormFile? legalAffairsCommitteeDecision,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            throw new BusinessRuleException("ملف مذكرة العرض مطلوب");
        }

        var dto = new UploadMemoVersionDto
        {
            Notes = notes,
            File = await FileRequestHelpers.ToUploadDtoAsync(file, cancellationToken),
            LegalAffairsCommitteeDecision = legalAffairsCommitteeDecision is { Length: > 0 }
                ? await FileRequestHelpers.ToUploadDtoAsync(legalAffairsCommitteeDecision, cancellationToken)
                : null,
        };

        var result = await _memoService.UploadVersionAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>إرفاق قرار لجنة الشؤون القانونية بالإصدار الحالي — لا يُنشئ إصدارًا جديدًا.</summary>
    [HttpPost("{id:int}/legal-decision")]
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
    public async Task<IActionResult> UploadLegalDecision(
        int id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            throw new BusinessRuleException("ملف قرار لجنة الشؤون القانونية مطلوب");
        }

        await _memoService.UploadLegalDecisionAsync(
            id,
            await FileRequestHelpers.ToUploadDtoAsync(file, cancellationToken),
            cancellationToken);

        return NoContent();
    }

    /// <summary><paramref name="fileKey"/> اختياري: "legal-affairs-decision" لتحميل قرار اللجنة بدل ملف المذكرة.</summary>
    [HttpGet("{id:int}/versions/{versionNumber:int}/file")]
    public async Task<IActionResult> DownloadFile(int id, int versionNumber, [FromQuery] string? fileKey, CancellationToken cancellationToken)
    {
        var file = await _memoService.DownloadFileAsync(id, versionNumber, fileKey, cancellationToken);
        return File(file.Content, FileRequestHelpers.GetContentType(file.FileExtension), file.FileName);
    }

    [HttpPut("{id:int}/complete")]
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
    public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken)
    {
        await _memoService.SetCompletionAsync(id, true, cancellationToken);
        return NoContent();
    }

    /// <summary>إعادة فتح مذكرة مكتملة — مدير التخطيط فقط.</summary>
    [HttpPut("{id:int}/reopen")]
    [Authorize(Roles = Roles.FinancialManagers)]
    public async Task<IActionResult> Reopen(int id, CancellationToken cancellationToken)
    {
        await _memoService.SetCompletionAsync(id, false, cancellationToken);
        return NoContent();
    }
}
