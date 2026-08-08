using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.API.Common;
using SmartInvest.Application.Common;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Enums;

namespace SmartInvest.API.Controllers;

/// <summary>
/// مراحل الطرح للمشروع الفرعي (الإدارة المالية).
/// المراحل: tender-document / announcement / opening-envelopes / technical-evaluation / financial-evaluation / contract-award.
/// </summary>
[ApiController]
[Authorize]
public class ProcurementController : ControllerBase
{
    private readonly IProcurementService _procurementService;

    public ProcurementController(IProcurementService procurementService)
    {
        _procurementService = procurementService;
    }

    /// <summary>قائمة المشروعات الفرعية مع ملخص تقدم التعاقدات.</summary>
    [HttpGet("api/procurement/subprojects")]
    public async Task<ActionResult<IReadOnlyList<ProcurementSubProjectListItemDto>>> GetSubProjects([FromQuery] int? financialYearId, CancellationToken cancellationToken)
    {
        var result = await _procurementService.GetSubProjectsAsync(financialYearId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("api/subprojects/{subProjectId:int}/procurement")]
    public async Task<ActionResult<ProcurementOverviewDto>> GetOverview(int subProjectId, CancellationToken cancellationToken)
    {
        var result = await _procurementService.GetOverviewAsync(subProjectId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("api/subprojects/{subProjectId:int}/procurement/{stage}")]
    public async Task<ActionResult<ProcurementStageDetailDto>> GetStage(int subProjectId, string stage, CancellationToken cancellationToken)
    {
        var result = await _procurementService.GetStageAsync(subProjectId, ParseStage(stage), cancellationToken);
        return Ok(result);
    }

    /// <summary>رفع إصدار جديد — multipart/form-data: حقل لكل ملف باسم مفتاح الخانة + حقل notes اختياري.</summary>
    [HttpPost("api/subprojects/{subProjectId:int}/procurement/{stage}/versions")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<ProcurementVersionDto>> UploadVersion(
        int subProjectId,
        string stage,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        var dto = new UploadProcurementVersionDto { Notes = notes };

        foreach (var file in Request.Form.Files)
        {
            if (file.Length > 0)
            {
                dto.Files[file.Name] = await FileRequestHelpers.ToUploadDtoAsync(file, cancellationToken);
            }
        }

        var result = await _procurementService.UploadVersionAsync(subProjectId, ParseStage(stage), dto, cancellationToken);
        return Ok(result);
    }

    [HttpGet("api/subprojects/{subProjectId:int}/procurement/{stage}/versions/{versionNumber:int}/files/{fileKey}")]
    public async Task<IActionResult> DownloadFile(
        int subProjectId,
        string stage,
        int versionNumber,
        string fileKey,
        CancellationToken cancellationToken)
    {
        var file = await _procurementService.DownloadFileAsync(subProjectId, ParseStage(stage), versionNumber, fileKey, cancellationToken);
        return File(file.Content, FileRequestHelpers.GetContentType(file.FileExtension), file.FileName);
    }

    /// <summary>إكمال المرحلة رسميًا.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/{stage}/complete")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> Complete(int subProjectId, string stage, CancellationToken cancellationToken)
    {
        await _procurementService.SetCompletionAsync(subProjectId, ParseStage(stage), true, cancellationToken);
        return NoContent();
    }

    /// <summary>إعادة فتح مرحلة مكتملة — مدير التخطيط فقط.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/{stage}/reopen")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> Reopen(int subProjectId, string stage, CancellationToken cancellationToken)
    {
        await _procurementService.SetCompletionAsync(subProjectId, ParseStage(stage), false, cancellationToken);
        return NoContent();
    }

    /// <summary>تأكيد/إلغاء تأكيد صرف الدفعة المقدمة — خاص بمرحلة العقد والترسية.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/contract-award/advance-payment")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> SetAdvancePaymentDone(int subProjectId, SetAdvancePaymentDoneDto dto, CancellationToken cancellationToken)
    {
        await _procurementService.SetAdvancePaymentDoneAsync(subProjectId, dto.Done, cancellationToken);
        return NoContent();
    }

    /// <summary>حفظ بيانات الترسية: المقاول، الدفعة المقدمة، مدة التنفيذ، الشرط الجزائي.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/contract-award/details")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> SetContractAwardDetails(int subProjectId, SetContractAwardDetailsDto dto, CancellationToken cancellationToken)
    {
        await _procurementService.SetContractAwardDetailsAsync(subProjectId, dto, cancellationToken);
        return NoContent();
    }

    /// <summary>تسجيل تسليم أرضية المشروع للمقاول — تبدأ عندها مدة التنفيذ.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/contract-award/site-handover")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> SetSiteHandover(int subProjectId, SetSiteHandoverDto dto, CancellationToken cancellationToken)
    {
        await _procurementService.SetSiteHandoverAsync(subProjectId, dto.HandoverDate, cancellationToken);
        return NoContent();
    }

    private static ProcurementStage ParseStage(string stage)
    {
        if (!ProcurementStageKeys.TryFromKey(stage, out var parsed))
        {
            throw new NotFoundException($"مرحلة التعاقدات '{stage}' غير معروفة");
        }

        return parsed;
    }
}
