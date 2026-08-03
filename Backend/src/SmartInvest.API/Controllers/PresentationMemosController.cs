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
[HasPermission(Permissions.MemosView)]
public class PresentationMemosController : ControllerBase
{
    private readonly IPresentationMemoService _memoService;

    public PresentationMemosController(IPresentationMemoService memoService)
    {
        _memoService = memoService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PresentationMemoDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _memoService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PresentationMemoDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _memoService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(Permissions.MemosManage)]
    public async Task<ActionResult<PresentationMemoDto>> Create(CreatePresentationMemoDto dto, CancellationToken cancellationToken)
    {
        var result = await _memoService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.MemosManage)]
    public async Task<ActionResult<PresentationMemoDto>> Update(int id, UpdatePresentationMemoDto dto, CancellationToken cancellationToken)
    {
        var result = await _memoService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.MemosManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _memoService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>رفع إصدار جديد — multipart/form-data: حقل file + حقل notes اختياري.</summary>
    [HttpPost("{id:int}/versions")]
    [HasPermission(Permissions.MemosManage)]
    public async Task<ActionResult<ProcurementVersionDto>> UploadVersion(
        int id,
        IFormFile? file,
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
        };

        var result = await _memoService.UploadVersionAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}/versions/{versionNumber:int}/file")]
    public async Task<IActionResult> DownloadFile(int id, int versionNumber, CancellationToken cancellationToken)
    {
        var file = await _memoService.DownloadFileAsync(id, versionNumber, cancellationToken);
        return File(file.Content, FileRequestHelpers.GetContentType(file.FileExtension), file.FileName);
    }

    [HttpPut("{id:int}/complete")]
    [HasPermission(Permissions.MemosManage)]
    public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken)
    {
        await _memoService.SetCompletionAsync(id, true, cancellationToken);
        return NoContent();
    }

    /// <summary>إعادة فتح مذكرة مكتملة — مدير التخطيط فقط.</summary>
    [HttpPut("{id:int}/reopen")]
    [HasPermission(Permissions.MemosManage)]
    public async Task<IActionResult> Reopen(int id, CancellationToken cancellationToken)
    {
        await _memoService.SetCompletionAsync(id, false, cancellationToken);
        return NoContent();
    }
}
