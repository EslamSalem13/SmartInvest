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
    public async Task<ActionResult<IReadOnlyList<ProcurementSubProjectListItemDto>>> GetSubProjects(
        [FromQuery] int? financialYearId, [FromQuery] int? excludeMemoId, CancellationToken cancellationToken)
    {
        var result = await _procurementService.GetSubProjectsAsync(financialYearId, excludeMemoId, cancellationToken);
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
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
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
    [Authorize(Roles = Roles.FinancialManagers)]
    public async Task<IActionResult> Complete(int subProjectId, string stage, CancellationToken cancellationToken)
    {
        await _procurementService.SetCompletionAsync(subProjectId, ParseStage(stage), true, cancellationToken);
        return NoContent();
    }

    /// <summary>إعادة فتح مرحلة مكتملة — مدير التخطيط فقط.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/{stage}/reopen")]
    [Authorize(Roles = Roles.FinancialManagers)]
    public async Task<IActionResult> Reopen(int subProjectId, string stage, CancellationToken cancellationToken)
    {
        await _procurementService.SetCompletionAsync(subProjectId, ParseStage(stage), false, cancellationToken);
        return NoContent();
    }

    /// <summary>تأكيد/إلغاء تأكيد صرف الدفعة المقدمة — خاص بمرحلة العقد والترسية.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/contract-award/advance-payment")]
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
    public async Task<IActionResult> SetAdvancePaymentDone(int subProjectId, SetAdvancePaymentDoneDto dto, CancellationToken cancellationToken)
    {
        await _procurementService.SetAdvancePaymentDoneAsync(subProjectId, dto.Done, cancellationToken);
        return NoContent();
    }

    /// <summary>حفظ بيانات الترسية: المقاول، الدفعة المقدمة، مدة التنفيذ، الشرط الجزائي.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/contract-award/details")]
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
    public async Task<IActionResult> SetContractAwardDetails(int subProjectId, SetContractAwardDetailsDto dto, CancellationToken cancellationToken)
    {
        await _procurementService.SetContractAwardDetailsAsync(subProjectId, dto, cancellationToken);
        return NoContent();
    }

    /// <summary>تسجيل تسليم أرضية المشروع للمقاول — multipart/form-data: handoverDate + ملف proof.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/contract-award/site-handover")]
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
    public async Task<IActionResult> SetSiteHandover(
        int subProjectId,
        [FromForm] DateTime handoverDate,
        CancellationToken cancellationToken)
    {
        var file = Request.Form.Files.FirstOrDefault(f => f.Name == "proof" && f.Length > 0)
            ?? throw new BusinessRuleException("إثبات تسليم الأرضية مطلوب");

        var proof = await FileRequestHelpers.ToUploadDtoAsync(file, cancellationToken);
        await _procurementService.SetSiteHandoverAsync(subProjectId, handoverDate, proof, cancellationToken);
        return NoContent();
    }

    /// <summary>تنزيل إثبات تسليم الأرضية.</summary>
    [HttpGet("api/subprojects/{subProjectId:int}/procurement/contract-award/site-handover/proof")]
    public async Task<IActionResult> DownloadSiteHandoverProof(int subProjectId, CancellationToken cancellationToken)
    {
        var file = await _procurementService.DownloadSiteHandoverProofAsync(subProjectId, cancellationToken);
        return File(file.Content, FileRequestHelpers.GetContentType(file.FileExtension), file.FileName);
    }

    /// <summary>تحديث إثبات صرف الدفعة المقدمة على الإصدار الحالي مباشرة — بلا حاجة لإعادة رفع أمر الإسناد والعقد
    /// (بعكس رفع إصدار جديد عبر /versions الذي يُلزم كل الملفات المطلوبة بكل رفعة).</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/contract-award/advance-payment-proof")]
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
    public async Task<IActionResult> SetAdvancePaymentProof(
        int subProjectId,
        IFormFile? proof,
        CancellationToken cancellationToken)
    {
        if (proof == null || proof.Length == 0)
        {
            throw new BusinessRuleException("إثبات صرف الدفعة المقدمة مطلوب");
        }

        var file = await FileRequestHelpers.ToUploadDtoAsync(proof, cancellationToken);
        await _procurementService.SetAdvancePaymentProofAsync(subProjectId, file, cancellationToken);
        return NoContent();
    }

    /// <summary>مدير التخطيط أو السوبر أدمن يحددان المدة القصوى لمرحلة عادية. الإعلان ثابت والترسية بلا مدة عامة.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/{stage}/duration")]
    [Authorize(Roles = Roles.ManagementStaff)]
    public async Task<IActionResult> SetDuration(int subProjectId, string stage, SetStageDurationDto dto, CancellationToken cancellationToken)
    {
        await _procurementService.SetStageDurationAsync(subProjectId, ParseStage(stage), dto.DurationDays, cancellationToken);
        return NoContent();
    }

    /// <summary>تاريخ نشر الإعلان — خاص بمرحلة الإعلان فقط، منه تُحسب مدة الـ15 يومًا الإلزامية.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/announcement/date")]
    [Authorize(Roles = Roles.FinancialOperationsStaff)]
    public async Task<IActionResult> SetAnnouncementDate(int subProjectId, SetAnnouncementDateDto dto, CancellationToken cancellationToken)
    {
        await _procurementService.SetAnnouncementDateAsync(subProjectId, dto.AnnouncementDate, cancellationToken);
        return NoContent();
    }

    /// <summary>"هذه المرحلة غير لازمة للطرح" — مدير الإدارة المالية فقط، يتطلب سببًا.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/{stage}/skip")]
    [Authorize(Roles = Roles.FinancialManagers)]
    public async Task<IActionResult> Skip(int subProjectId, string stage, SkipStageDto dto, CancellationToken cancellationToken)
    {
        await _procurementService.SkipStageAsync(subProjectId, ParseStage(stage), dto.Reason, cancellationToken);
        return NoContent();
    }

    /// <summary>فشل مرحلة — يتطلب سببًا، ويُبطل اكتمالها وما بعدها دون حذف أي إصدار.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/procurement/{stage}/fail")]
    [Authorize(Roles = Roles.FinancialManagers)]
    public async Task<IActionResult> Fail(int subProjectId, string stage, FailStageDto dto, CancellationToken cancellationToken)
    {
        await _procurementService.FailStageAsync(subProjectId, ParseStage(stage), dto.Reason, cancellationToken);
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
