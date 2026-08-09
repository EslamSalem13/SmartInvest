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
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return stages.Select(ToDto).ToList();
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
            .FirstOrDefaultAsync(a => a.SubProjectId == subProjectId, cancellationToken);
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

        return ToDto(stage);
    }

    public async Task<ExecutionStageDto> MarkCompleteAsync(int subProjectId, int stageId, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, cancellationToken);
        stage.IsCompleted = true;
        stage.CompletedAt = DateTime.UtcNow;

        _stageRepository.Update(stage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage);
    }

    public async Task<ExecutionStageDto> SetPenaltyAsync(int subProjectId, int stageId, SetExecutionStagePenaltyDto dto, CancellationToken cancellationToken = default)
    {
        var stage = await GetOwnedStageAsync(subProjectId, stageId, cancellationToken);
        stage.PenaltyAmount = dto.PenaltyAmount;
        stage.PenaltyPaid = dto.PenaltyPaid;

        _stageRepository.Update(stage);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(stage);
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

        var approved = subProjects.Where(s => s.IsApproved).ToList();
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

            var latestPhysical = stages.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.ExecutionStageId).FirstOrDefault()?.PhysicalProgressPercent ?? 0;

            var nextDeadline = stages.Where(x => !x.IsCompleted).OrderBy(x => x.Deadline).FirstOrDefault()?.Deadline;

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

    private async Task<ExecutionStage> GetOwnedStageAsync(int subProjectId, int stageId, CancellationToken cancellationToken)
    {
        var stage = await _stageRepository.GetByIdAsync(stageId, cancellationToken);
        if (stage == null || stage.SubProjectId != subProjectId)
        {
            throw new NotFoundException($"مرحلة التنفيذ رقم {stageId} غير موجودة لهذا المشروع");
        }

        return stage;
    }

    /// <summary>إسقاط خفيف لصفوف قائمة المتابعة — يتجنب تحميل بايتات ملفات الإثبات (varbinary(max)) لكل مرحلة.</summary>
    private sealed class FollowUpStageProjection
    {
        public int SubProjectId { get; set; }
        public int ExecutionStageId { get; set; }
        public decimal SelfFundingSpent { get; set; }
        public decimal BankFundingSpent { get; set; }
        public decimal PhysicalProgressPercent { get; set; }
        public DateTime Deadline { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private static StoredFile ToStoredFile(FileUploadDto dto) => new()
    {
        FileName = dto.FileName,
        FileExtension = dto.FileExtension,
        FileSize = dto.FileSize,
        Content = dto.Content,
    };

    private static ExecutionStageDto ToDto(ExecutionStage s) => new()
    {
        Id = s.ExecutionStageId,
        SubProjectId = s.SubProjectId,
        Name = s.Name,
        Deadline = s.Deadline,
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
