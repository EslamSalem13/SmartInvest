using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
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

    public const string FinalDeliveryStageName = "التسليم النهائي";

    public ExecutionStageService(
        AppDbContext context,
        IGenericRepository<ExecutionStage> stageRepository,
        ISubProjectRepository subProjectRepository,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _stageRepository = stageRepository;
        _subProjectRepository = subProjectRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ExecutionStageDto>> GetBySubProjectAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var stages = await _context.ExecutionStages.AsNoTracking()
            .Where(s => s.SubProjectId == subProjectId)
            .OrderBy(s => s.IsFinalDelivery)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var contractualDeliveryDate = await GetContractualDeliveryDateAsync(subProjectId, cancellationToken);
        return stages.Select(s => ToDto(s, contractualDeliveryDate)).ToList();
    }

    public async Task<ExecutionStageDto> CreateAsync(int subProjectId, CreateExecutionStageDto dto, CancellationToken cancellationToken = default)
    {
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
            .Where(s => s.SubProjectId == subProjectId)
            .ToListAsync(cancellationToken);

        var spentSoFar = existingStages.Sum(s => s.SelfFundingSpent + s.BankFundingSpent);
        var newTotalSpent = spentSoFar + dto.SelfFundingSpent + dto.BankFundingSpent;
        var overrunMultiplier = 1 + (subProject.OverrunPercentage ?? 0) / 100m;
        var allowedCeiling = subProject.TotalCost * overrunMultiplier;
        if (newTotalSpent > allowedCeiling)
        {
            throw new BusinessRuleException(
                $"إجمالي المصروف ({newTotalSpent:N2} ج.م) يتجاوز الحد المسموح ({allowedCeiling:N2} ج.م = التكلفة الإجمالية + نسبة التجاوز)");
        }

        var stage = new ExecutionStage
        {
            SubProjectId = subProjectId,
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

    public async Task<ExecutionStageDto> MarkCompleteAsync(int subProjectId, int stageId, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, cancellationToken);
        stage.IsCompleted = true;
        stage.CompletedAt = DateTime.UtcNow;

        _stageRepository.Update(stage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage, await GetContractualDeliveryDateAsync(subProjectId, cancellationToken));
    }

    public async Task<ExecutionStageDto> ReopenAsync(int subProjectId, int stageId, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, cancellationToken);
        stage.IsCompleted = false;
        stage.CompletedAt = null;

        _stageRepository.Update(stage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage, await GetContractualDeliveryDateAsync(subProjectId, cancellationToken));
    }

    public async Task<ExecutionStageDto> SetPenaltyAsync(int subProjectId, int stageId, SetExecutionStagePenaltyDto dto, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, cancellationToken);
        stage.PenaltyAmount = dto.PenaltyAmount;
        stage.PenaltyPaid = dto.PenaltyPaid;

        _stageRepository.Update(stage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage, await GetContractualDeliveryDateAsync(subProjectId, cancellationToken));
    }

    public async Task<FileDownloadDto> DownloadFileAsync(int subProjectId, int stageId, string fileKey, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, cancellationToken);

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
                .Where(s => subProjectIds.Contains(s.SubProjectId))
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

        return approved.Select(s =>
        {
            stagesByProject.TryGetValue(s.SubProjectId, out var stages);
            stages ??= [];

            var financialPercent = s.TotalCost <= 0
                ? 0
                : Math.Round(stages.Sum(x => x.SelfFundingSpent + x.BankFundingSpent) / s.TotalCost * 100, 2);

            var latestPhysical = stages
                .Where(x => !x.IsFinalDelivery)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.ExecutionStageId)
                .FirstOrDefault()?.PhysicalProgressPercent ?? 0;

            var nextDeadline = stages
                .Where(x => !x.IsCompleted && x.Deadline != null)
                .OrderBy(x => x.Deadline)
                .FirstOrDefault()?.Deadline;

            contractorNameByProject.TryGetValue(s.SubProjectId, out var contractorName);

            return new FollowUpListItemDto
            {
                SubProjectId = s.SubProjectId,
                SubProjectName = s.SubProjectName,
                SubProjectCode = s.SubProjectCode,
                MainProjectName = s.MainProject.MainProjectName,
                ContractorName = contractorName,
                IsStalled = s.Status.StatusName == "متعثر",
                FinancialProgressPercent = financialPercent,
                PhysicalProgressPercent = latestPhysical,
                NextDeadline = nextDeadline,
                StageCount = stages.Count,
            };
        }).ToList();
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

        var stage = await _context.ExecutionStages
            .FirstOrDefaultAsync(s => s.SubProjectId == subProjectId && s.IsFinalDelivery, cancellationToken);

        if (stage == null)
        {
            stage = new ExecutionStage
            {
                SubProjectId = subProjectId,
                Name = FinalDeliveryStageName,
                IsFinalDelivery = true,
                Deadline = deadline,
            };
            await _stageRepository.AddAsync(stage, cancellationToken);
        }
        else
        {
            // الاسم والموعد فقط يُداران تلقائيًا — الصرف والنسبة والغرامة تبقى كما سجّلها الموظف
            stage.Name = FinalDeliveryStageName;
            stage.Deadline = deadline;
            _stageRepository.Update(stage);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<ExecutionStage> GetOwnedStageAsync(int subProjectId, int stageId, CancellationToken cancellationToken)
    {
        var stage = await _stageRepository.GetByIdAsync(stageId, cancellationToken);
        if (stage == null || stage.SubProjectId != subProjectId)
        {
            throw new NotFoundException($"مرحلة التنفيذ رقم {stageId} غير موجودة لهذا المشروع");
        }

        return stage;
    }

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
