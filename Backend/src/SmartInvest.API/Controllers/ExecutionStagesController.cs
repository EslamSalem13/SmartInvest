using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.API.Common;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

/// <summary>مراحل التنفيذ بعد الترسية (متابعة المشروعات).</summary>
[ApiController]
[Authorize]
public class ExecutionStagesController : ControllerBase
{
    private readonly IExecutionStageService _executionStageService;

    public ExecutionStagesController(IExecutionStageService executionStageService)
    {
        _executionStageService = executionStageService;
    }

    /// <summary>جدول متابعة المشروعات — مشروعات معتمدة فقط، بنفس فلاتر صفحة المشروعات.</summary>
    [HttpGet("api/follow-up")]
    public async Task<ActionResult<IReadOnlyList<FollowUpListItemDto>>> GetFollowUpList(
        [FromQuery] int? financialYearId,
        [FromQuery] int? mainProgramId,
        [FromQuery] int? subProgramId,
        [FromQuery] int? markazId,
        [FromQuery] int? priorityId,
        [FromQuery] string? searchTerm,
        CancellationToken cancellationToken)
    {
        var result = await _executionStageService.GetFollowUpListAsync(
            financialYearId, mainProgramId, subProgramId, markazId, priorityId, searchTerm, cancellationToken);
        return Ok(result);
    }

    [HttpGet("api/subprojects/{subProjectId:int}/execution-stages")]
    public async Task<ActionResult<IReadOnlyList<ExecutionStageDto>>> GetBySubProject(int subProjectId, [FromQuery] int financialYearId, CancellationToken cancellationToken)
    {
        var result = await _executionStageService.GetBySubProjectAsync(subProjectId, financialYearId, cancellationToken);
        return Ok(result);
    }

    /// <summary>multipart/form-data: name, startDate, deadline, selfFundingSpent, bankFundingSpent, physicalProgressPercent, notes + حتى 3 ملفات (selfFundingProof / bankFundingProof / physicalProgressProof).</summary>
    [HttpPost("api/subprojects/{subProjectId:int}/execution-stages")]
    [Authorize(Roles = Roles.FollowUpStaff)]
    public async Task<ActionResult<ExecutionStageDto>> Create(
        int subProjectId,
        [FromForm] int financialYearId,
        [FromForm] string name,
        [FromForm] DateTime? startDate,
        [FromForm] DateTime deadline,
        [FromForm] decimal selfFundingSpent,
        [FromForm] decimal bankFundingSpent,
        [FromForm] decimal physicalProgressPercent,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        var dto = new CreateExecutionStageDto
        {
            FinancialYearId = financialYearId,
            Name = name,
            StartDate = startDate,
            Deadline = deadline,
            SelfFundingSpent = selfFundingSpent,
            BankFundingSpent = bankFundingSpent,
            PhysicalProgressPercent = physicalProgressPercent,
            Notes = notes,
        };

        var selfFile = Request.Form.Files["selfFundingProof"];
        if (selfFile is { Length: > 0 })
        {
            dto.SelfFundingProofFile = await FileRequestHelpers.ToUploadDtoAsync(selfFile, cancellationToken);
        }

        var bankFile = Request.Form.Files["bankFundingProof"];
        if (bankFile is { Length: > 0 })
        {
            dto.BankFundingProofFile = await FileRequestHelpers.ToUploadDtoAsync(bankFile, cancellationToken);
        }

        var progressFile = Request.Form.Files["physicalProgressProof"];
        if (progressFile is { Length: > 0 })
        {
            dto.PhysicalProgressProofFile = await FileRequestHelpers.ToUploadDtoAsync(progressFile, cancellationToken);
        }

        var result = await _executionStageService.CreateAsync(subProjectId, dto, cancellationToken);
        return Ok(result);
    }

    [HttpGet("api/subprojects/{subProjectId:int}/completion-eligibility")]
    public async Task<ActionResult<ProjectCompletionEligibilityDto>> GetCompletionEligibility(
        int subProjectId, [FromQuery] int financialYearId, CancellationToken cancellationToken)
    {
        return Ok(await _executionStageService.GetCompletionEligibilityAsync(subProjectId, financialYearId, cancellationToken));
    }

    /// <summary>خط زمني حياة المشروع الكامل (كل السنوات) — لمخطط "تطور التنفيذ" بلوحة التحكم. بلا financialYearId عمدًا.</summary>
    [HttpGet("api/subprojects/{subProjectId:int}/execution-timeline")]
    public async Task<ActionResult<ExecutionTimelineDto>> GetExecutionTimeline(int subProjectId, CancellationToken cancellationToken)
    {
        return Ok(await _executionStageService.GetExecutionTimelineAsync(subProjectId, cancellationToken));
    }

    [HttpPut("api/subprojects/{subProjectId:int}/complete-execution")]
    [Authorize(Roles = Roles.FollowUpStaff)]
    public async Task<ActionResult<ProjectCompletionEligibilityDto>> CompleteExecution(
        int subProjectId, [FromQuery] int financialYearId, CancellationToken cancellationToken)
    {
        return Ok(await _executionStageService.CompleteExecutionAsync(subProjectId, financialYearId, cancellationToken));
    }

    [HttpPut("api/subprojects/{subProjectId:int}/execution-stages/{stageId:int}")]
    [Authorize(Roles = Roles.FollowUpStaff)]
    public async Task<ActionResult<ExecutionStageDto>> Update(
        int subProjectId,
        int stageId,
        [FromForm] int financialYearId,
        [FromForm] string name,
        [FromForm] DateTime? startDate,
        [FromForm] DateTime? deadline,
        [FromForm] decimal selfFundingSpent,
        [FromForm] decimal bankFundingSpent,
        [FromForm] decimal physicalProgressPercent,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        var dto = new UpdateExecutionStageDto
        {
            FinancialYearId = financialYearId,
            Name = name,
            StartDate = startDate,
            Deadline = deadline,
            SelfFundingSpent = selfFundingSpent,
            BankFundingSpent = bankFundingSpent,
            PhysicalProgressPercent = physicalProgressPercent,
            Notes = notes,
        };
        var selfFile = Request.Form.Files["selfFundingProof"];
        if (selfFile is { Length: > 0 }) dto.SelfFundingProofFile = await FileRequestHelpers.ToUploadDtoAsync(selfFile, cancellationToken);
        var bankFile = Request.Form.Files["bankFundingProof"];
        if (bankFile is { Length: > 0 }) dto.BankFundingProofFile = await FileRequestHelpers.ToUploadDtoAsync(bankFile, cancellationToken);
        var progressFile = Request.Form.Files["physicalProgressProof"];
        if (progressFile is { Length: > 0 }) dto.PhysicalProgressProofFile = await FileRequestHelpers.ToUploadDtoAsync(progressFile, cancellationToken);

        return Ok(await _executionStageService.UpdateAsync(subProjectId, stageId, dto, cancellationToken));
    }

    [HttpPut("api/subprojects/{subProjectId:int}/execution-stages/{stageId:int}/complete")]
    [Authorize(Roles = Roles.FollowUpStaff)]
    public async Task<ActionResult<ExecutionStageDto>> MarkComplete(int subProjectId, int stageId, [FromQuery] int financialYearId, CancellationToken cancellationToken)
    {
        var result = await _executionStageService.MarkCompleteAsync(subProjectId, stageId, financialYearId, cancellationToken);
        return Ok(result);
    }

    /// <summary>عكس إنهاء المرحلة عن طريق الخطأ — للمديرين المخولين فقط.</summary>
    [HttpPut("api/subprojects/{subProjectId:int}/execution-stages/{stageId:int}/reopen")]
    [Authorize(Roles = Roles.FollowUpManagers)]
    public async Task<ActionResult<ExecutionStageDto>> Reopen(int subProjectId, int stageId, [FromQuery] int financialYearId, CancellationToken cancellationToken)
    {
        var result = await _executionStageService.ReopenAsync(subProjectId, stageId, financialYearId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("api/subprojects/{subProjectId:int}/execution-stages/{stageId:int}/penalty")]
    [Authorize(Roles = Roles.FollowUpManagers)]
    public async Task<ActionResult<ExecutionStageDto>> SetPenalty(int subProjectId, int stageId, [FromQuery] int financialYearId, SetExecutionStagePenaltyDto dto, CancellationToken cancellationToken)
    {
        var result = await _executionStageService.SetPenaltyAsync(subProjectId, stageId, financialYearId, dto, cancellationToken);
        return Ok(result);
    }

    [HttpGet("api/subprojects/{subProjectId:int}/execution-stages/{stageId:int}/files/{fileKey}")]
    public async Task<IActionResult> DownloadFile(int subProjectId, int stageId, string fileKey, [FromQuery] int financialYearId, CancellationToken cancellationToken)
    {
        var file = await _executionStageService.DownloadFileAsync(subProjectId, stageId, financialYearId, fileKey, cancellationToken);
        return File(file.Content, FileRequestHelpers.GetContentType(file.FileExtension), file.FileName);
    }
}
