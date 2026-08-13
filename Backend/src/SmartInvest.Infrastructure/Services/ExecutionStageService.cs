using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Application.Services;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Services;

public class ExecutionStageService : IExecutionStageService
{
    private readonly AppDbContext _context;
    private readonly IGenericRepository<ExecutionStage> _stageRepository;
    private readonly ISubProjectRepository _subProjectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public const string FinalDeliveryStageName = "التسليم الأولي";

    public ExecutionStageService(
        AppDbContext context,
        IGenericRepository<ExecutionStage> stageRepository,
        ISubProjectRepository subProjectRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _context = context;
        _stageRepository = stageRepository;
        _subProjectRepository = subProjectRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ExecutionStageDto>> GetBySubProjectAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken = default)
    {
        await GetCycleAsync(subProjectId, financialYearId, cancellationToken);
        await SyncFinalDeliveryStageAsync(subProjectId, cancellationToken);
        var stages = await _context.ExecutionStages.AsNoTracking()
            .Include(s => s.SubProjectFinancialYear)
            .Where(s => s.SubProjectId == subProjectId
                && s.SubProjectFinancialYear!.FinancialYearId == financialYearId)
            .OrderBy(s => s.IsFinalDelivery)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var contractualDeliveryDate = await GetContractualDeliveryDateAsync(subProjectId, cancellationToken);
        return stages.Select(s => ToDto(s, contractualDeliveryDate)).ToList();
    }

    public async Task<ExecutionStageDto> CreateAsync(int subProjectId, CreateExecutionStageDto dto, CancellationToken cancellationToken = default)
    {
        var cycle = await GetCycleAsync(subProjectId, dto.FinancialYearId, cancellationToken);
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new BusinessRuleException("اسم المرحلة مطلوب");
        }
        if (dto.Name.Trim().Length > 250)
        {
            throw new BusinessRuleException("اسم المرحلة يجب ألا يتجاوز 250 حرفًا");
        }
        if (dto.SelfFundingSpent < 0)
        {
            throw new BusinessRuleException("المصروف الذاتي لا يمكن أن يكون سالبًا");
        }
        if (dto.BankFundingSpent < 0)
        {
            throw new BusinessRuleException("المصروف البنكي لا يمكن أن يكون سالبًا");
        }
        if (dto.PhysicalProgressPercent < 0 || dto.PhysicalProgressPercent > 100)
        {
            throw new BusinessRuleException("نسبة التنفيذ العيني يجب أن تكون بين 0 و100");
        }
        if (dto.SelfFundingSpent > 0 && dto.SelfFundingProofFile == null)
        {
            throw new BusinessRuleException("إثبات الصرف الذاتي مطلوب عند تسجيل مبلغ ذاتي");
        }
        if (dto.BankFundingSpent > 0 && dto.BankFundingProofFile == null)
        {
            throw new BusinessRuleException("إثبات الصرف البنكي مطلوب عند تسجيل مبلغ بنكي");
        }
        if (dto.PhysicalProgressPercent > 0 && dto.PhysicalProgressProofFile == null)
        {
            throw new BusinessRuleException("إثبات التنفيذ العيني مطلوب عند تسجيل نسبة تنفيذ");
        }

        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        EnsureProjectOpen(subProject);

        var award = await _context.ContractAwards.AsNoTracking()
            .Where(a => a.SubProjectId == subProjectId)
            .Select(a => new { a.IsCompleted })
            .FirstOrDefaultAsync(cancellationToken);
        if (award is not { IsCompleted: true })
        {
            throw new BusinessRuleException("لا يمكن إضافة مرحلة تنفيذ قبل اكتمال مرحلة العقد والترسية");
        }

        // ترتيب التنفيذ: مقاولات يسمح بصرف الدفعة فور بدء المرحلة، توريدات يشترط تسجيل تنفيذ عيني
        // في نفس المرحلة قبل أي صرف - المقاول يورّد أولًا ثم يُصرف له.
        var hasSpend = dto.SelfFundingSpent > 0 || dto.BankFundingSpent > 0;
        if (subProject.ProjectNature == "توريدات" && hasSpend && dto.PhysicalProgressPercent <= 0)
        {
            throw new BusinessRuleException("في مشروعات التوريدات، يجب تسجيل نسبة تنفيذ عيني قبل تسجيل أي صرف على نفس المرحلة");
        }

        var existingStages = await _context.ExecutionStages.AsNoTracking()
            .Where(s => s.SubProjectFinancialYearId == cycle.SubProjectFinancialYearId)
            .ToListAsync(cancellationToken);

        var advanceSpent = await GetAdvancePaymentTotalAsync(subProjectId, cancellationToken);
        var spentSoFar = existingStages.Sum(s => s.SelfFundingSpent + s.BankFundingSpent) + advanceSpent;
        var newTotalSpent = spentSoFar + dto.SelfFundingSpent + dto.BankFundingSpent;
        var allowedCeiling = await GetAllowedCeilingAsync(subProjectId, subProject, cancellationToken);
        if (newTotalSpent > allowedCeiling)
        {
            throw new BusinessRuleException(
                $"إجمالي المصروف ({newTotalSpent:N2} ج.م) يتجاوز الحد المسموح ({allowedCeiling:N2} ج.م = التكلفة الإجمالية + نسبة التجاوز)");
        }

        var stage = new ExecutionStage
        {
            SubProjectId = subProjectId,
            SubProjectFinancialYearId = cycle.SubProjectFinancialYearId,
            SubProjectFinancialYear = cycle,
            Name = dto.Name.Trim(),
            Deadline = dto.Deadline,
            SelfFundingSpent = dto.SelfFundingSpent,
            BankFundingSpent = dto.BankFundingSpent,
            PhysicalProgressPercent = dto.PhysicalProgressPercent,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
        };

        if (dto.SelfFundingProofFile != null)
        {
            stage.SelfFundingProofFile = ToStoredFile(dto.SelfFundingProofFile);
        }
        if (dto.BankFundingProofFile != null)
        {
            stage.BankFundingProofFile = ToStoredFile(dto.BankFundingProofFile);
        }
        if (dto.PhysicalProgressProofFile != null)
        {
            stage.PhysicalProgressProofFile = ToStoredFile(dto.PhysicalProgressProofFile);
        }

        await _stageRepository.AddAsync(stage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage, await GetContractualDeliveryDateAsync(subProjectId, cancellationToken));
    }

    public async Task<ExecutionStageDto> UpdateAsync(int subProjectId, int stageId, UpdateExecutionStageDto dto, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, dto.FinancialYearId, cancellationToken);
        if (stage.IsCompleted)
        {
            throw new BusinessRuleException("يجب إعادة فتح المرحلة المكتملة قبل تعديلها");
        }

        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");
        EnsureProjectOpen(subProject);
        ValidateStageValues(dto, stage);

        var otherSpent = await _context.ExecutionStages.AsNoTracking()
            .Where(x => x.SubProjectFinancialYearId == stage.SubProjectFinancialYearId && x.ExecutionStageId != stageId)
            .SumAsync(x => x.SelfFundingSpent + x.BankFundingSpent, cancellationToken);
        var allowedCeiling = await GetAllowedCeilingAsync(subProjectId, subProject, cancellationToken);
        var newTotal = otherSpent + await GetAdvancePaymentTotalAsync(subProjectId, cancellationToken)
            + dto.SelfFundingSpent + dto.BankFundingSpent;
        if (newTotal > allowedCeiling)
        {
            throw new BusinessRuleException($"إجمالي المنصرف ({newTotal:N2} ج.م) يتجاوز الحد المسموح ({allowedCeiling:N2} ج.م)");
        }

        if (!stage.IsFinalDelivery)
        {
            stage.Name = dto.Name.Trim();
            stage.Deadline = dto.Deadline;
        }
        stage.SelfFundingSpent = dto.SelfFundingSpent;
        stage.BankFundingSpent = dto.BankFundingSpent;
        stage.PhysicalProgressPercent = dto.PhysicalProgressPercent;
        stage.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        if (dto.SelfFundingProofFile != null) stage.SelfFundingProofFile = ToStoredFile(dto.SelfFundingProofFile);
        if (dto.BankFundingProofFile != null) stage.BankFundingProofFile = ToStoredFile(dto.BankFundingProofFile);
        if (dto.PhysicalProgressProofFile != null) stage.PhysicalProgressProofFile = ToStoredFile(dto.PhysicalProgressProofFile);

        _stageRepository.Update(stage);
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            EntityName = nameof(ExecutionStage), EntityId = stageId, FieldName = "ExecutionStage",
            OldValue = "Existing", NewValue = "Updated", ChangedAt = DateTime.UtcNow,
            ChangedByUserId = RequireCurrentUserId(),
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(stage, await GetContractualDeliveryDateAsync(subProjectId, cancellationToken));
    }

    public async Task<ExecutionStageDto> MarkCompleteAsync(int subProjectId, int stageId, int financialYearId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectOpenAsync(subProjectId, cancellationToken);
        var stage = await GetOwnedStageAsync(subProjectId, stageId, financialYearId, cancellationToken);
        stage.IsCompleted = true;
        stage.CompletedAt = DateTime.UtcNow;

        _stageRepository.Update(stage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage, await GetContractualDeliveryDateAsync(subProjectId, cancellationToken));
    }

    public async Task<ExecutionStageDto> ReopenAsync(int subProjectId, int stageId, int financialYearId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectOpenAsync(subProjectId, cancellationToken);
        var stage = await GetOwnedStageAsync(subProjectId, stageId, financialYearId, cancellationToken);
        stage.IsCompleted = false;
        stage.CompletedAt = null;

        _stageRepository.Update(stage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage, await GetContractualDeliveryDateAsync(subProjectId, cancellationToken));
    }

    public async Task<ExecutionStageDto> SetPenaltyAsync(int subProjectId, int stageId, int financialYearId, SetExecutionStagePenaltyDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureProjectOpenAsync(subProjectId, cancellationToken);
        var stage = await GetOwnedStageAsync(subProjectId, stageId, financialYearId, cancellationToken);
        stage.PenaltyAmount = dto.PenaltyAmount;
        stage.PenaltyPaid = dto.PenaltyPaid;

        _stageRepository.Update(stage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage, await GetContractualDeliveryDateAsync(subProjectId, cancellationToken));
    }

    public async Task<FileDownloadDto> DownloadFileAsync(int subProjectId, int stageId, int financialYearId, string fileKey, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, financialYearId, cancellationToken);

        var file = fileKey switch
        {
            "self" => stage.SelfFundingProofFile,
            "bank" => stage.BankFundingProofFile,
            "progress" => stage.PhysicalProgressProofFile,
            _ => throw new BusinessRuleException($"نوع الملف '{fileKey}' غير معروف"),
        };

        if (file == null)
        {
            throw new NotFoundException("الملف المطلوب غير موجود");
        }

        return new FileDownloadDto { FileName = file.FileName, FileExtension = file.FileExtension, Content = file.Content };
    }

    public async Task<IReadOnlyList<FollowUpListItemDto>> GetFollowUpListAsync(
        int? financialYearId, int? mainProgramId, int? subProgramId, int? markazId, int? priorityId,
        string? searchTerm, CancellationToken cancellationToken = default)
    {
        var (subProjects, _) = await _subProjectRepository.SearchAsync(
            mainProjectId: null, mainProgramId, subProgramId, markazId, priorityId,
            statusId: null, financialYearId, searchTerm, page: 1, pageSize: 2000, cancellationToken);

        // متابعة المشروعات للمشروعات المُسندة فعلًا لمقاول — أي التي اكتملت ترسيتها.
        // إكمال الترسية يستلزم بالفعل إكمال المراحل الخمس السابقة، فهذا يكافئ 6/6.
        var approvedIds = subProjects.Where(s => s.IsApproved).Select(s => s.SubProjectId).ToList();
        if (approvedIds.Count == 0)
        {
            return [];
        }

        var awardedIds = (await _context.ContractAwards.AsNoTracking()
                .Where(a => a.IsCompleted && approvedIds.Contains(a.SubProjectId))
                .Select(a => a.SubProjectId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var approved = subProjects.Where(s => s.IsApproved && awardedIds.Contains(s.SubProjectId)).ToList();
        if (approved.Count == 0)
        {
            return [];
        }

        var subProjectIds = approved.Select(s => s.SubProjectId).ToList();

        var stagesByProject = (await _context.ExecutionStages.AsNoTracking()
                .Where(s => subProjectIds.Contains(s.SubProjectId)
                    && financialYearId != null
                    && s.SubProjectFinancialYear != null
                    && s.SubProjectFinancialYear.FinancialYearId == financialYearId)
                .Select(s => new FollowUpStageProjection
                {
                    SubProjectId = s.SubProjectId,
                    ExecutionStageId = s.ExecutionStageId,
                    SelfFundingSpent = s.SelfFundingSpent,
                    BankFundingSpent = s.BankFundingSpent,
                    PhysicalProgressPercent = s.PhysicalProgressPercent,
                    Deadline = s.Deadline,
                    IsCompleted = s.IsCompleted,
                    CreatedAt = s.CreatedAt,
                    IsFinalDelivery = s.IsFinalDelivery,
                })
                .ToListAsync(cancellationToken))
            .GroupBy(s => s.SubProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var contractorNameByProject = (await _context.Set<ProjectAssignment>().AsNoTracking()
                .Where(a => subProjectIds.Contains(a.SubProjectId) && a.ContractorId != null)
                .OrderByDescending(a => a.AssignmentDate)
                .Select(a => new { a.SubProjectId, ContractorName = a.Contractor!.ContractorName })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.SubProjectId)
            .ToDictionary(g => g.Key, g => g.First().ContractorName);

        var awardByProject = (await _context.ContractAwards.AsNoTracking()
                .Where(a => subProjectIds.Contains(a.SubProjectId))
                .Select(a => new AwardProjection
                {
                    SubProjectId = a.SubProjectId,
                    IsCompleted = a.IsCompleted,
                    ContractValue = a.ProjectAssignment != null ? a.ProjectAssignment.ContractValue : null,
                    AdvancePaymentDone = a.AdvancePaymentDone,
                    AdvancePaymentSelfAmount = a.AdvancePaymentSelfAmount ?? 0,
                    AdvancePaymentBankAmount = a.AdvancePaymentBankAmount ?? 0,
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.SubProjectId);

        return approved.Select(s =>
        {
            stagesByProject.TryGetValue(s.SubProjectId, out var stages);
            stages ??= [];

            var financialPercent = s.TotalCost <= 0
                ? 0
                : Math.Round(stages.Sum(x => x.SelfFundingSpent + x.BankFundingSpent) / s.TotalCost * 100, 2);

            var physicalTotal = stages
                .Where(x => !x.IsFinalDelivery)
                .Sum(x => x.PhysicalProgressPercent);

            var nextDeadline = stages
                .Where(x => !x.IsCompleted && x.Deadline != null)
                .OrderBy(x => x.Deadline)
                .FirstOrDefault()?.Deadline;

            contractorNameByProject.TryGetValue(s.SubProjectId, out var contractorName);
            awardByProject.TryGetValue(s.SubProjectId, out var award);
            var eligibility = ProjectCompletionPolicy.Evaluate(new ProjectCompletionFacts(
                s.ExecutionCompletedAt != null || s.Status.StatusName == "منتهي",
                award?.IsCompleted == true,
                award?.ContractValue,
                s.OverrunPercentage ?? 0,
                stages.Sum(x => x.SelfFundingSpent),
                stages.Sum(x => x.BankFundingSpent),
                award?.AdvancePaymentDone == true,
                award?.AdvancePaymentSelfAmount ?? 0,
                award?.AdvancePaymentBankAmount ?? 0,
                stages.Select(x => new ExecutionStageCompletionFact(
                    x.IsFinalDelivery, x.IsCompleted, x.PhysicalProgressPercent)).ToList()));

            return new FollowUpListItemDto
            {
                SubProjectId = s.SubProjectId,
                SubProjectName = s.SubProjectName,
                SubProjectCode = s.SubProjectCode,
                MainProjectName = s.MainProject.MainProjectName,
                ContractorName = contractorName,
                IsStalled = s.Status.StatusName == "متعثر",
                FinancialProgressPercent = financialPercent,
                PhysicalProgressPercent = physicalTotal,
                NextDeadline = nextDeadline,
                StageCount = stages.Count,
                CompletionEligibility = eligibility,
            };
        }).ToList();
    }

    public async Task<ProjectCompletionEligibilityDto> GetCompletionEligibilityAsync(
        int subProjectId, int financialYearId, CancellationToken cancellationToken = default)
    {
        var cycle = await GetCycleAsync(subProjectId, financialYearId, cancellationToken);
        return await BuildCompletionEligibilityAsync(subProjectId, cycle.SubProjectFinancialYearId, cancellationToken);
    }

    public async Task<ProjectCompletionEligibilityDto> CompleteExecutionAsync(
        int subProjectId, int financialYearId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var cycle = await GetCycleAsync(subProjectId, financialYearId, cancellationToken);
        var subProject = await _context.SubProjects
            .Include(x => x.Status)
            .FirstOrDefaultAsync(x => x.SubProjectId == subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        if (subProject.ExecutionCompletedAt != null || subProject.Status.StatusName == "منتهي")
        {
            await transaction.CommitAsync(cancellationToken);
            return await BuildCompletionEligibilityAsync(subProjectId, cycle.SubProjectFinancialYearId, cancellationToken);
        }

        var eligibility = await BuildCompletionEligibilityAsync(subProjectId, cycle.SubProjectFinancialYearId, cancellationToken);
        if (!eligibility.CanCompleteProject)
            throw new BusinessRuleException(string.Join(" ", eligibility.Blockers));

        var completedStatus = await _context.Set<ProjectStatus>()
            .FirstOrDefaultAsync(x => x.StatusName == "منتهي", cancellationToken)
            ?? throw new BusinessRuleException("حالة المشروع القياسية «منتهي» غير موجودة");
        var completedAt = DateTime.UtcNow;
        var oldStatus = subProject.Status.StatusName;
        subProject.StatusId = completedStatus.StatusId;
        subProject.Status = completedStatus;
        subProject.ExecutionCompletedAt = completedAt;

        _context.AuditLogs.AddRange(
            new AuditLog
            {
                EntityName = nameof(SubProject), EntityId = subProjectId,
                FieldName = nameof(SubProject.ExecutionCompletedAt), OldValue = null,
                NewValue = completedAt.ToString("O"), ChangedByUserId = RequireCurrentUserId(), ChangedAt = completedAt,
            },
            new AuditLog
            {
                EntityName = nameof(SubProject), EntityId = subProjectId,
                FieldName = nameof(SubProject.StatusId), OldValue = oldStatus,
                NewValue = completedStatus.StatusName, ChangedByUserId = RequireCurrentUserId(), ChangedAt = completedAt,
            });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        eligibility.IsProjectCompleted = true;
        eligibility.CanCompleteProject = false;
        return eligibility;
    }

    public async Task SyncFinalDeliveryStageAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var award = await _context.ContractAwards.AsNoTracking()
            .Where(a => a.SubProjectId == subProjectId)
            .Select(a => new { a.IsCompleted, a.SiteHandoverDate, a.ExecutionDurationMonths, a.ExecutionDurationDays })
            .FirstOrDefaultAsync(cancellationToken);

        if (award is not { IsCompleted: true })
        {
            return;
        }

        var deadline = ComputeContractualDeliveryDate(
            award.SiteHandoverDate, award.ExecutionDurationMonths, award.ExecutionDurationDays);

        var cycles = await _context.Set<SubProjectFinancialYear>()
            .Where(x => x.SubProjectId == subProjectId)
            .ToListAsync(cancellationToken);

        foreach (var cycle in cycles)
        {
            var stage = await _context.ExecutionStages.FirstOrDefaultAsync(
                s => s.SubProjectFinancialYearId == cycle.SubProjectFinancialYearId && s.IsFinalDelivery,
                cancellationToken);

            if (stage == null)
            {
                await _stageRepository.AddAsync(new ExecutionStage
                {
                    SubProjectId = subProjectId,
                    SubProjectFinancialYearId = cycle.SubProjectFinancialYearId,
                    Name = FinalDeliveryStageName,
                    IsFinalDelivery = true,
                    Deadline = deadline,
                }, cancellationToken);
            }
            else
            {
                stage.Name = FinalDeliveryStageName;
                stage.Deadline = deadline;
                _stageRepository.Update(stage);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<ExecutionStage> GetOwnedStageAsync(int subProjectId, int stageId, int financialYearId, CancellationToken cancellationToken)
    {
        var stage = await _context.ExecutionStages
            .Include(x => x.SubProjectFinancialYear)
            .FirstOrDefaultAsync(x => x.ExecutionStageId == stageId, cancellationToken);
        if (stage == null || stage.SubProjectId != subProjectId
            || stage.SubProjectFinancialYear?.FinancialYearId != financialYearId)
        {
            throw new NotFoundException($"مرحلة التنفيذ رقم {stageId} غير موجودة لهذا المشروع");
        }

        return stage;
    }

    private async Task<SubProjectFinancialYear> GetCycleAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken)
    {
        var cycle = await _context.Set<SubProjectFinancialYear>()
            .FirstOrDefaultAsync(x => x.SubProjectId == subProjectId && x.FinancialYearId == financialYearId, cancellationToken);
        return cycle ?? throw new BusinessRuleException("المشروع غير مرتبط بالسنة المالية المختارة");
    }

    private static void EnsureProjectOpen(SubProject subProject)
    {
        if (subProject.ExecutionCompletedAt != null || subProject.Status?.StatusName == "منتهي")
            throw new BusinessRuleException("لا يمكن إضافة أو تعديل مراحل بعد إكمال المشروع");
    }

    private async Task EnsureProjectOpenAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var project = await _context.SubProjects.AsNoTracking()
            .Include(x => x.Status)
            .FirstOrDefaultAsync(x => x.SubProjectId == subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");
        EnsureProjectOpen(project);
    }

    private static void ValidateStageValues(UpdateExecutionStageDto dto, ExecutionStage existing)
    {
        if (!existing.IsFinalDelivery && string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessRuleException("اسم المرحلة مطلوب");
        if (!existing.IsFinalDelivery && dto.Deadline == null)
            throw new BusinessRuleException("الموعد النهائي مطلوب");
        if (dto.Name.Trim().Length > 250)
            throw new BusinessRuleException("اسم المرحلة يجب ألا يتجاوز 250 حرفًا");
        if (dto.SelfFundingSpent < 0 || dto.BankFundingSpent < 0)
            throw new BusinessRuleException("قيم المنصرف لا يمكن أن تكون سالبة");
        if (dto.PhysicalProgressPercent < 0 || dto.PhysicalProgressPercent > 100)
            throw new BusinessRuleException("نسبة التنفيذ العيني يجب أن تكون بين 0 و100");
        if (dto.SelfFundingSpent > 0 && dto.SelfFundingProofFile == null && existing.SelfFundingProofFile == null)
            throw new BusinessRuleException("إثبات الصرف الذاتي مطلوب عند تسجيل مبلغ ذاتي");
        if (dto.BankFundingSpent > 0 && dto.BankFundingProofFile == null && existing.BankFundingProofFile == null)
            throw new BusinessRuleException("إثبات الصرف البنكي مطلوب عند تسجيل مبلغ بنكي");
        if (dto.PhysicalProgressPercent > 0 && dto.PhysicalProgressProofFile == null && existing.PhysicalProgressProofFile == null)
            throw new BusinessRuleException("إثبات التنفيذ العيني مطلوب عند تسجيل نسبة تنفيذ");
    }

    private async Task<decimal> GetAllowedCeilingAsync(int subProjectId, SubProject subProject, CancellationToken cancellationToken)
    {
        var contractValue = await _context.ContractAwards.AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId && x.IsCompleted && x.ProjectAssignmentId != null)
            .Select(x => x.ProjectAssignment!.ContractValue)
            .FirstOrDefaultAsync(cancellationToken);
        if (contractValue is null or <= 0)
            throw new BusinessRuleException("لا توجد قيمة عقد صحيحة مسجلة للمشروع");
        return contractValue.Value * (1 + (subProject.OverrunPercentage ?? 0) / 100m);
    }

    private async Task<decimal> GetAdvancePaymentTotalAsync(int subProjectId, CancellationToken cancellationToken) =>
        await _context.ContractAwards.AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId && x.AdvancePaymentDone)
            .Select(x => (x.AdvancePaymentSelfAmount ?? 0) + (x.AdvancePaymentBankAmount ?? 0))
            .FirstOrDefaultAsync(cancellationToken);

    private string RequireCurrentUserId() =>
        _currentUser.UserId ?? throw new BusinessRuleException("تعذر تحديد المستخدم الحالي لتسجيل العملية");

    /// <summary>تاريخ التسليم التعاقدي المحسوب — null لو الترسية غير مكتملة أو الأرضية لم تُسلَّم.</summary>
    private async Task<DateTime?> GetContractualDeliveryDateAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var award = await _context.ContractAwards.AsNoTracking()
            .Where(a => a.SubProjectId == subProjectId && a.IsCompleted)
            .Select(a => new { a.SiteHandoverDate, a.ExecutionDurationMonths, a.ExecutionDurationDays })
            .FirstOrDefaultAsync(cancellationToken);

        return award == null
            ? null
            : ComputeContractualDeliveryDate(award.SiteHandoverDate, award.ExecutionDurationMonths, award.ExecutionDurationDays);
    }

    /// <summary>إسقاط خفيف لصفوف قائمة المتابعة — يتجنب تحميل بايتات ملفات الإثبات (varbinary(max)) لكل مرحلة.</summary>
    private sealed class FollowUpStageProjection
    {
        public int SubProjectId { get; set; }
        public int ExecutionStageId { get; set; }
        public decimal SelfFundingSpent { get; set; }
        public decimal BankFundingSpent { get; set; }
        public decimal PhysicalProgressPercent { get; set; }
        public DateTime? Deadline { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsFinalDelivery { get; set; }
    }

    private sealed class AwardProjection
    {
        public int SubProjectId { get; set; }
        public bool IsCompleted { get; set; }
        public decimal? ContractValue { get; set; }
        public bool AdvancePaymentDone { get; set; }
        public decimal AdvancePaymentSelfAmount { get; set; }
        public decimal AdvancePaymentBankAmount { get; set; }
    }

    private async Task<ProjectCompletionEligibilityDto> BuildCompletionEligibilityAsync(
        int subProjectId, int subProjectFinancialYearId, CancellationToken cancellationToken)
    {
        var project = await _context.SubProjects.AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId)
            .Select(x => new
            {
                x.ExecutionCompletedAt,
                x.OverrunPercentage,
                StatusName = x.Status.StatusName,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        var award = await _context.ContractAwards.AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId)
            .Select(x => new
            {
                x.IsCompleted,
                ContractValue = x.ProjectAssignment != null ? x.ProjectAssignment.ContractValue : null,
                x.AdvancePaymentDone,
                AdvanceSelf = x.AdvancePaymentSelfAmount ?? 0,
                AdvanceBank = x.AdvancePaymentBankAmount ?? 0,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var stages = await _context.ExecutionStages.AsNoTracking()
            .Where(x => x.SubProjectFinancialYearId == subProjectFinancialYearId)
            .Select(x => new
            {
                x.IsFinalDelivery,
                x.IsCompleted,
                x.PhysicalProgressPercent,
                x.SelfFundingSpent,
                x.BankFundingSpent,
            })
            .ToListAsync(cancellationToken);

        return ProjectCompletionPolicy.Evaluate(new ProjectCompletionFacts(
            project.ExecutionCompletedAt != null || project.StatusName == "منتهي",
            award?.IsCompleted == true,
            award?.ContractValue,
            project.OverrunPercentage ?? 0,
            stages.Sum(x => x.SelfFundingSpent),
            stages.Sum(x => x.BankFundingSpent),
            award?.AdvancePaymentDone == true,
            award?.AdvanceSelf ?? 0,
            award?.AdvanceBank ?? 0,
            stages.Select(x => new ExecutionStageCompletionFact(
                x.IsFinalDelivery, x.IsCompleted, x.PhysicalProgressPercent)).ToList()));
    }

    private static StoredFile ToStoredFile(FileUploadDto dto) => new()
    {
        FileName = dto.FileName,
        FileExtension = dto.FileExtension,
        FileSize = dto.FileSize,
        Content = dto.Content,
    };

    private static DateTime? ComputeContractualDeliveryDate(DateTime? handoverDate, int? months, int? days) =>
        handoverDate?.AddMonths(months ?? 0).AddDays(days ?? 0);

    private static ExecutionStageDto ToDto(ExecutionStage s, DateTime? contractualDeliveryDate) => new()
    {
        Id = s.ExecutionStageId,
        SubProjectId = s.SubProjectId,
        FinancialYearId = s.SubProjectFinancialYear?.FinancialYearId,
        Name = s.Name,
        Deadline = s.Deadline,
        IsFinalDelivery = s.IsFinalDelivery,
        // مرحلة التسليم النهائي هي المرجع نفسه، فلا تُقارن بذاتها
        ExceedsContractualDeadline = !s.IsFinalDelivery
            && s.Deadline != null
            && contractualDeliveryDate != null
            && s.Deadline > contractualDeliveryDate,
        SelfFundingSpent = s.SelfFundingSpent,
        BankFundingSpent = s.BankFundingSpent,
        HasSelfFundingProof = s.SelfFundingProofFile != null,
        HasBankFundingProof = s.BankFundingProofFile != null,
        SelfFundingProofFileName = s.SelfFundingProofFile?.FileName,
        BankFundingProofFileName = s.BankFundingProofFile?.FileName,
        PhysicalProgressPercent = s.PhysicalProgressPercent,
        HasPhysicalProgressProof = s.PhysicalProgressProofFile != null,
        PhysicalProgressProofFileName = s.PhysicalProgressProofFile?.FileName,
        Notes = s.Notes,
        PenaltyAmount = s.PenaltyAmount,
        PenaltyPaid = s.PenaltyPaid,
        IsCompleted = s.IsCompleted,
        CreatedAt = s.CreatedAt,
        CompletedAt = s.CompletedAt,
    };
}
