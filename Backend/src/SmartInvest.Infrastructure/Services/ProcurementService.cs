using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Enums;
using SmartInvest.Infrastructure.Data;
using System.Linq.Expressions;

namespace SmartInvest.Infrastructure.Services;

/// <summary>
/// تنفيذ دورة التعاقدات. يستخدم AppDbContext مباشرة (نفس نمط IdentityService)
/// لأن كل مرحلة تحتاج استعلامات مخصوصة على جدولين (المستند + إصداراته).
/// </summary>
public class ProcurementService : IProcurementService
{
    private readonly AppDbContext _context;
    private readonly Dictionary<ProcurementStage, IStageOps> _stages;

    public ProcurementService(AppDbContext context)
    {
        _context = context;
        _stages = BuildStages(context);
    }

    // ------------------------------------------------------------------
    // الواجهة العامة
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<ProcurementSubProjectListItemDto>> GetSubProjectsAsync(int? financialYearId = null, CancellationToken cancellationToken = default)
    {
        return await _context.SubProjects.AsNoTracking()
            .Where(s => financialYearId == null || s.FinancialYears.Any(y => y.FinancialYearId == financialYearId))
            .OrderBy(s => s.SubProjectId)
            .Select(s => new ProcurementSubProjectListItemDto
            {
                SubProjectId = s.SubProjectId,
                SubProjectName = s.SubProjectName,
                SubProjectCode = s.SubProjectCode,
                MainProjectName = s.MainProject.MainProjectName,
                TotalStages = 6,
                CompletedStages =
                    (_context.TenderDocuments.Any(d => d.SubProjectId == s.SubProjectId && d.IsCompleted) ? 1 : 0) +
                    (_context.Announcements.Any(d => d.SubProjectId == s.SubProjectId && d.IsCompleted) ? 1 : 0) +
                    (_context.OpeningEnvelopes.Any(d => d.SubProjectId == s.SubProjectId && d.IsCompleted) ? 1 : 0) +
                    (_context.TechnicalEvaluations.Any(d => d.SubProjectId == s.SubProjectId && d.IsCompleted) ? 1 : 0) +
                    (_context.FinancialEvaluations.Any(d => d.SubProjectId == s.SubProjectId && d.IsCompleted) ? 1 : 0) +
                    (_context.ContractAwards.Any(d => d.SubProjectId == s.SubProjectId && d.IsCompleted) ? 1 : 0),
                HasPresentationMemo = _context.PresentationMemoSubProjects.Any(m => m.SubProjectId == s.SubProjectId),
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProcurementOverviewDto> GetOverviewAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var sub = await _context.SubProjects.AsNoTracking()
            .Where(s => s.SubProjectId == subProjectId)
            .Select(s => new { s.SubProjectName, s.SubProjectCode })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        var overview = new ProcurementOverviewDto
        {
            SubProjectId = subProjectId,
            SubProjectName = sub.SubProjectName,
            SubProjectCode = sub.SubProjectCode,
            PresentationMemos = await _context.PresentationMemoSubProjects.AsNoTracking()
                .Where(x => x.SubProjectId == subProjectId)
                .Select(x => new PresentationMemoBriefDto
                {
                    Id = x.PresentationMemo.Id,
                    Title = x.PresentationMemo.Title,
                    CurrentVersionNumber = x.PresentationMemo.CurrentVersionNumber,
                    IsCompleted = x.PresentationMemo.IsCompleted,
                })
                .ToListAsync(cancellationToken),
        };

        bool? previousCompleted = true;
        foreach (var (stage, ops) in _stages.OrderBy(x => (int)x.Key))
        {
            var state = await ops.FindDocAsync(subProjectId, cancellationToken);
            var stageDto = BuildStageDto<ProcurementStageDto>(stage, ops, state);
            stageDto.IsLocked = previousCompleted != true;
            if (stage == ProcurementStage.ContractAward)
            {
                stageDto.AdvancePaymentDone = await GetAdvancePaymentDoneAsync(subProjectId, cancellationToken);
                stageDto.ContractAward = await GetContractAwardDetailsAsync(subProjectId, cancellationToken);
            }
            overview.Stages.Add(stageDto);
            previousCompleted = state?.IsCompleted ?? false;
        }

        return overview;
    }

    public async Task<ProcurementStageDetailDto> GetStageAsync(int subProjectId, ProcurementStage stage, CancellationToken cancellationToken = default)
    {
        await EnsureSubProjectExistsAsync(subProjectId, cancellationToken);

        var ops = _stages[stage];
        var state = await ops.FindDocAsync(subProjectId, cancellationToken);
        var dto = BuildStageDto<ProcurementStageDetailDto>(stage, ops, state);
        dto.IsLocked = !await IsPreviousStageCompletedAsync(stage, subProjectId, cancellationToken);

        if (stage == ProcurementStage.ContractAward)
        {
            dto.AdvancePaymentDone = await GetAdvancePaymentDoneAsync(subProjectId, cancellationToken);
            dto.ContractAward = await GetContractAwardDetailsAsync(subProjectId, cancellationToken);
        }

        if (state != null)
        {
            dto.Versions = await ops.GetVersionDtosAsync(state.Id, cancellationToken);
        }

        return dto;
    }

    public async Task<ProcurementVersionDto> UploadVersionAsync(int subProjectId, ProcurementStage stage, UploadProcurementVersionDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureSubProjectExistsAsync(subProjectId, cancellationToken);
        await EnsurePreviousStageCompletedAsync(stage, subProjectId, cancellationToken);

        var ops = _stages[stage];

        if (dto.Files.Count == 0)
        {
            throw new BusinessRuleException("يجب رفع ملف واحد على الأقل");
        }

        var knownKeys = ops.Slots.Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = dto.Files.Keys.FirstOrDefault(k => !knownKeys.Contains(k));
        if (unknown != null)
        {
            throw new BusinessRuleException($"نوع الملف '{unknown}' غير معروف لهذه المرحلة");
        }

        foreach (var slot in ops.Slots.Where(s => s.Required))
        {
            if (!dto.Files.Keys.Contains(slot.Key, StringComparer.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException($"ملف \"{slot.Label}\" مطلوب");
            }
        }

        var files = dto.Files.ToDictionary(
            x => x.Key,
            x => new StoredFile
            {
                FileName = x.Value.FileName,
                FileExtension = x.Value.FileExtension,
                FileSize = x.Value.FileSize,
                Content = x.Value.Content,
            },
            StringComparer.OrdinalIgnoreCase);

        return await ops.UploadAsync(subProjectId, files, dto.Notes, cancellationToken);
    }

    public async Task<FileDownloadDto> DownloadFileAsync(int subProjectId, ProcurementStage stage, int versionNumber, string fileKey, CancellationToken cancellationToken = default)
    {
        var ops = _stages[stage];
        var file = await ops.GetFileAsync(subProjectId, versionNumber, fileKey, cancellationToken)
            ?? throw new NotFoundException("الملف المطلوب غير موجود");

        return new FileDownloadDto
        {
            FileName = file.FileName,
            FileExtension = file.FileExtension,
            Content = file.Content,
        };
    }

    public async Task SetCompletionAsync(int subProjectId, ProcurementStage stage, bool isCompleted, CancellationToken cancellationToken = default)
    {
        await EnsureSubProjectExistsAsync(subProjectId, cancellationToken);

        if (isCompleted)
        {
            await EnsurePreviousStageCompletedAsync(stage, subProjectId, cancellationToken);
            await _stages[stage].SetCompletionAsync(subProjectId, true, cancellationToken);

            if (stage == ProcurementStage.ContractAward)
            {
                await StampSiteHandoverOnAwardAsync(subProjectId, cancellationToken);
            }

            return;
        }

        // إعادة فتح مرحلة تُبطل كل ما بعدها فعليًا — ترجع "لم تكتمل" في القاعدة نفسها،
        // مش بس تظهر مقفولة في الواجهة، عشان لو المرحلة دي اتقفلت تاني تفضل كل المراحل التالية بانتظار إعادة اعتمادها من جديد.
        await _stages[stage].SetCompletionAsync(subProjectId, false, cancellationToken);

        foreach (var laterStage in _stages.Keys.Where(s => (int)s > (int)stage).OrderBy(s => (int)s))
        {
            await _stages[laterStage].ResetIfCompletedAsync(subProjectId, cancellationToken);
        }
    }

    public async Task SetAdvancePaymentDoneAsync(int subProjectId, bool done, CancellationToken cancellationToken = default)
    {
        var doc = await GetEditableContractAwardAsync(subProjectId, cancellationToken);
        doc.AdvancePaymentDone = done;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetContractAwardDetailsAsync(int subProjectId, SetContractAwardDetailsDto dto, CancellationToken cancellationToken = default)
    {
        var doc = await GetEditableContractAwardAsync(subProjectId, cancellationToken);

        doc.AdvancePaymentDone = dto.AdvancePaymentDone;
        doc.AdvancePaymentPercentage = dto.AdvancePaymentPercentage;
        doc.AdvancePaymentSelfAmount = dto.AdvancePaymentSelfAmount;
        doc.AdvancePaymentBankAmount = dto.AdvancePaymentBankAmount;
        doc.ExecutionDurationMonths = dto.ExecutionDurationMonths;
        doc.ExecutionDurationDays = dto.ExecutionDurationDays;
        doc.SiteHandoverMode = dto.SiteHandoverMode is int mode ? (SiteHandoverMode)mode : null;
        doc.PenaltyAmount = dto.PenaltyAmount;

        // الإسناد نفسه يعيش في ProjectAssignment — مصدر حقيقة واحد لهوية المقاول،
        // تقرأ منه متابعة المشروعات وملف المقاول.
        if (dto.ContractorId is int contractorId)
        {
            await UpsertAssignmentAsync(doc, contractorId, dto, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetSiteHandoverAsync(int subProjectId, DateTime handoverDate, CancellationToken cancellationToken = default)
    {
        await EnsureSubProjectExistsAsync(subProjectId, cancellationToken);

        var doc = await _context.ContractAwards
            .FirstOrDefaultAsync(x => x.SubProjectId == subProjectId, cancellationToken)
            ?? throw new NotFoundException("لم تبدأ مرحلة الترسية بعد");

        if (!doc.IsCompleted)
        {
            throw new BusinessRuleException("لا يمكن تسجيل تسليم الأرضية قبل إكمال الترسية");
        }

        doc.SiteHandoverDate = handoverDate;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ContractAward> GetEditableContractAwardAsync(int subProjectId, CancellationToken cancellationToken)
    {
        await EnsureSubProjectExistsAsync(subProjectId, cancellationToken);
        await EnsurePreviousStageCompletedAsync(ProcurementStage.ContractAward, subProjectId, cancellationToken);

        var doc = await _context.ContractAwards
            .FirstOrDefaultAsync(x => x.SubProjectId == subProjectId, cancellationToken);

        if (doc == null)
        {
            doc = new ContractAward { SubProjectId = subProjectId };
            _context.ContractAwards.Add(doc);
        }
        else if (doc.IsCompleted)
        {
            throw new BusinessRuleException("هذه المرحلة مكتملة — يجب إعادة فتحها قبل التعديل");
        }

        return doc;
    }

    /// <summary>ينشئ الإسناد أو يحدّثه — إعادة فتح الترسية ثم إكمالها لا تُنشئ إسنادًا مكررًا.</summary>
    private async Task UpsertAssignmentAsync(ContractAward doc, int contractorId, SetContractAwardDetailsDto dto, CancellationToken cancellationToken)
    {
        var contractorExists = await _context.Set<Contractor>().AsNoTracking()
            .AnyAsync(c => c.ContractorId == contractorId, cancellationToken);
        if (!contractorExists)
        {
            throw new NotFoundException($"المقاول رقم {contractorId} غير موجود");
        }

        var contractTypeId = dto.ContractTypeId
            ?? throw new BusinessRuleException("يجب اختيار نوع العقد مع المقاول");

        if (!await _context.Set<ContractType>().AsNoTracking().AnyAsync(t => t.ContractTypeId == contractTypeId, cancellationToken))
        {
            throw new NotFoundException($"نوع العقد رقم {contractTypeId} غير موجود");
        }

        var assignment = doc.ProjectAssignmentId is int existingId
            ? await _context.Set<ProjectAssignment>().FirstOrDefaultAsync(a => a.AssignmentId == existingId, cancellationToken)
            : null;

        if (assignment == null)
        {
            assignment = new ProjectAssignment
            {
                SubProjectId = doc.SubProjectId,
                AssignmentDate = DateTime.UtcNow,
                ExpectedStartDate = DateTime.UtcNow,
                ExpectedEndDate = DateTime.UtcNow,
            };
            _context.Set<ProjectAssignment>().Add(assignment);
        }

        assignment.ContractorId = contractorId;
        assignment.ContractTypeId = contractTypeId;
        assignment.ContractNumber = dto.ContractNumber;
        assignment.ContractValue = dto.ContractValue;

        await _context.SaveChangesAsync(cancellationToken);
        doc.ProjectAssignmentId = assignment.AssignmentId;
    }

    // ------------------------------------------------------------------
    // مساعدات
    // ------------------------------------------------------------------

    private async Task EnsureSubProjectExistsAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var exists = await _context.SubProjects.AsNoTracking()
            .AnyAsync(s => s.SubProjectId == subProjectId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");
        }
    }

    private async Task<bool> IsPreviousStageCompletedAsync(ProcurementStage stage, int subProjectId, CancellationToken cancellationToken)
    {
        if (stage == ProcurementStage.TenderDocument)
        {
            return true;
        }

        var previousStage = (ProcurementStage)((int)stage - 1);
        var previousState = await _stages[previousStage].FindDocAsync(subProjectId, cancellationToken);
        return previousState?.IsCompleted ?? false;
    }

    private async Task EnsurePreviousStageCompletedAsync(ProcurementStage stage, int subProjectId, CancellationToken cancellationToken)
    {
        if (stage == ProcurementStage.TenderDocument)
        {
            return;
        }

        if (!await IsPreviousStageCompletedAsync(stage, subProjectId, cancellationToken))
        {
            var previousStage = (ProcurementStage)((int)stage - 1);
            throw new BusinessRuleException($"يجب إكمال مرحلة \"{_stages[previousStage].Label}\" أولاً");
        }
    }

    /// <summary>
    /// عند إكمال الترسية: لو الأرضية مُسلَّمة بالفعل، يبدأ العد من تاريخ الإكمال.
    /// أما وضع "لم تُسلَّم بعد" فيترك التاريخ فارغًا حتى يُسجَّل من متابعة المشروعات.
    /// </summary>
    private async Task StampSiteHandoverOnAwardAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var doc = await _context.ContractAwards
            .FirstOrDefaultAsync(x => x.SubProjectId == subProjectId, cancellationToken);

        if (doc?.SiteHandoverMode == SiteHandoverMode.AtAward && doc.SiteHandoverDate == null)
        {
            doc.SiteHandoverDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ContractAwardDetailsDto?> GetContractAwardDetailsAsync(int subProjectId, CancellationToken cancellationToken)
    {
        var project = await _context.SubProjects.AsNoTracking()
            .Where(s => s.SubProjectId == subProjectId)
            .Select(s => new { s.ProjectNature, s.BankFunding, s.SelfFunding })
            .FirstOrDefaultAsync(cancellationToken);

        if (project == null)
        {
            return null;
        }

        var doc = await _context.ContractAwards.AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId)
            .Select(x => new
            {
                x.AdvancePaymentDone,
                x.AdvancePaymentPercentage,
                x.AdvancePaymentSelfAmount,
                x.AdvancePaymentBankAmount,
                x.ExecutionDurationMonths,
                x.ExecutionDurationDays,
                x.SiteHandoverMode,
                x.SiteHandoverDate,
                x.PenaltyAmount,
                x.ProjectAssignmentId,
                ContractorId = (int?)x.ProjectAssignment!.ContractorId,
                ContractorName = x.ProjectAssignment!.Contractor!.ContractorName,
                ContractTypeId = (int?)x.ProjectAssignment!.ContractTypeId,
                x.ProjectAssignment!.ContractNumber,
                x.ProjectAssignment!.ContractValue,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var details = new ContractAwardDetailsDto
        {
            ProjectNature = project.ProjectNature,
            RequiresAdvancePayment = IsContractingProject(project.ProjectNature),
            TotalCost = project.BankFunding + project.SelfFunding,
            BankFunding = project.BankFunding,
            SelfFunding = project.SelfFunding,
        };

        if (doc == null)
        {
            return details;
        }

        details.AdvancePaymentDone = doc.AdvancePaymentDone;
        details.AdvancePaymentPercentage = doc.AdvancePaymentPercentage;
        details.AdvancePaymentSelfAmount = doc.AdvancePaymentSelfAmount;
        details.AdvancePaymentBankAmount = doc.AdvancePaymentBankAmount;
        details.ExecutionDurationMonths = doc.ExecutionDurationMonths;
        details.ExecutionDurationDays = doc.ExecutionDurationDays;
        details.SiteHandoverMode = (int?)doc.SiteHandoverMode;
        details.SiteHandoverDate = doc.SiteHandoverDate;
        details.ContractualDeliveryDate = doc.SiteHandoverDate?
            .AddMonths(doc.ExecutionDurationMonths ?? 0)
            .AddDays(doc.ExecutionDurationDays ?? 0);
        details.PenaltyAmount = doc.PenaltyAmount;
        details.ContractorId = doc.ContractorId;
        details.ContractorName = doc.ContractorName;
        details.ContractTypeId = doc.ContractTypeId;
        details.ContractNumber = doc.ContractNumber;
        details.ContractValue = doc.ContractValue;

        return details;
    }

    private async Task<bool> GetAdvancePaymentDoneAsync(int subProjectId, CancellationToken cancellationToken)
    {
        return await _context.ContractAwards.AsNoTracking()
            .Where(x => x.SubProjectId == subProjectId)
            .Select(x => x.AdvancePaymentDone)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static TDto BuildStageDto<TDto>(ProcurementStage stage, IStageOps ops, StageDocState? state)
        where TDto : ProcurementStageDto, new()
    {
        return new TDto
        {
            Stage = ProcurementStageKeys.ToKey(stage),
            StageLabel = ops.Label,
            Order = (int)stage,
            DocumentId = state?.Id,
            CurrentVersionNumber = state?.CurrentVersionNumber ?? 0,
            IsCompleted = state?.IsCompleted ?? false,
            LastUpdatedAt = state?.LastUpdatedAt,
            FileSlots = ops.Slots
                .Select(s => new ProcurementFileSlotDto { Key = s.Key, Label = s.Label, Required = s.Required })
                .ToList(),
        };
    }

    private static Dictionary<ProcurementStage, IStageOps> BuildStages(AppDbContext db) => new()
    {
        [ProcurementStage.TenderDocument] = new StageOps<TenderDocument, TenderDocumentVersion>(
            db, "كراسة الشروط",
            (v, id) => v.TenderDocumentId = id,
            docId => v => v.TenderDocumentId == docId,
            [FileSlot<TenderDocumentVersion>.Single("file", "ملف كراسة الشروط", v => v.File, (v, f) => v.File = f!)]),

        [ProcurementStage.Announcement] = new StageOps<Announcement, AnnouncementVersion>(
            db, "الإعلان",
            (v, id) => v.AnnouncementId = id,
            docId => v => v.AnnouncementId == docId,
            [
                new FileSlot<AnnouncementVersion>("newspaper-advertisement", "إعلان الجريدة", v => v.NewspaperAdvertisement, (v, f) => v.NewspaperAdvertisement = f, required: true),
                new FileSlot<AnnouncementVersion>("portal-advertisement", "إعلان البوابة", v => v.PortalAdvertisement, (v, f) => v.PortalAdvertisement = f, required: true),
                new FileSlot<AnnouncementVersion>("competent-authority-approval", "موافقة الجهة المختصة", v => v.CompetentAuthorityApproval, (v, f) => v.CompetentAuthorityApproval = f, required: true),
            ]),

        [ProcurementStage.OpeningEnvelopes] = new StageOps<OpeningEnvelopes, OpeningEnvelopesVersion>(
            db, "فتح المظاريف",
            (v, id) => v.OpeningEnvelopesId = id,
            docId => v => v.OpeningEnvelopesId == docId,
            [FileSlot<OpeningEnvelopesVersion>.Single("file", "محضر فتح المظاريف", v => v.File, (v, f) => v.File = f!)]),

        [ProcurementStage.TechnicalEvaluation] = new StageOps<TechnicalEvaluation, TechnicalEvaluationVersion>(
            db, "التقييم الفني",
            (v, id) => v.TechnicalEvaluationId = id,
            docId => v => v.TechnicalEvaluationId == docId,
            [
                new FileSlot<TechnicalEvaluationVersion>("first-committee-report", "تقرير اللجنة الأول", v => v.FirstCommitteeReport, (v, f) => v.FirstCommitteeReport = f, required: true),
                new FileSlot<TechnicalEvaluationVersion>("second-committee-report", "تقرير اللجنة الثاني", v => v.SecondCommitteeReport, (v, f) => v.SecondCommitteeReport = f, required: true),
                new FileSlot<TechnicalEvaluationVersion>("final-technical-evaluation-report", "التقرير الفني النهائي", v => v.FinalTechnicalEvaluationReport, (v, f) => v.FinalTechnicalEvaluationReport = f, required: true),
            ]),

        [ProcurementStage.FinancialEvaluation] = new StageOps<FinancialEvaluation, FinancialEvaluationVersion>(
            db, "التقييم المالي",
            (v, id) => v.FinancialEvaluationId = id,
            docId => v => v.FinancialEvaluationId == docId,
            [
                new FileSlot<FinancialEvaluationVersion>("financial-bid-opening-minutes", "محضر فتح المظاريف المالية", v => v.FinancialBidOpeningMinutes, (v, f) => v.FinancialBidOpeningMinutes = f, required: true),
                new FileSlot<FinancialEvaluationVersion>("financial-evaluation-report", "تقرير التقييم المالي", v => v.FinancialEvaluationReport, (v, f) => v.FinancialEvaluationReport = f, required: true),
                new FileSlot<FinancialEvaluationVersion>("estimated-cost-sheet", "مقايسة التكلفة التقديرية", v => v.EstimatedCostSheet, (v, f) => v.EstimatedCostSheet = f, required: true),
            ]),

        [ProcurementStage.ContractAward] = new StageOps<ContractAward, ContractAwardVersion>(
            db, "العقد والترسية",
            (v, id) => v.ContractAwardId = id,
            docId => v => v.ContractAwardId == docId,
            [
                new FileSlot<ContractAwardVersion>("award-order", "أمر الإسناد", v => v.AwardOrder, (v, f) => v.AwardOrder = f, required: true),
                new FileSlot<ContractAwardVersion>("contract", "العقد", v => v.Contract, (v, f) => v.Contract = f, required: true),
                // إثبات صرف الدفعة المقدمة — إلزاميته مشروطة بنوع المشروع، فيتحقق منها extraCompletionCheck
                new FileSlot<ContractAwardVersion>("advance-payment-proof", "إثبات صرف الدفعة المقدمة", v => v.AdvancePaymentProof, (v, f) => v.AdvancePaymentProof = f, required: false),
            ],
            extraCompletionCheck: (doc, ct) => ValidateContractAwardForCompletionAsync(db, doc, ct)),
    };

    /// <summary>«مقاولات» = أعمال تنفيذ تستحق دفعة مقدمة. «توريدات» لا يُصرف لها مقدَّم.</summary>
    internal static bool IsContractingProject(string? projectNature) => projectNature == "مقاولات";

    /// <summary>
    /// قواعد إكمال الترسية: المقاول، المدة، تسليم الأرضية — ثم الدفعة المقدمة لمشروعات «مقاولات» فقط.
    /// </summary>
    private static async Task<string?> ValidateContractAwardForCompletionAsync(
        AppDbContext db,
        ContractAward doc,
        CancellationToken ct)
    {
        var project = await db.SubProjects.AsNoTracking()
            .Where(s => s.SubProjectId == doc.SubProjectId)
            .Select(s => new { s.ProjectNature, s.BankFunding, s.SelfFunding })
            .FirstOrDefaultAsync(ct);

        if (project == null)
        {
            return "المشروع الفرعي غير موجود";
        }

        if (doc.ProjectAssignmentId == null)
        {
            return "يجب اختيار المقاول المسند إليه المشروع قبل إكمال الترسية";
        }

        if ((doc.ExecutionDurationMonths ?? 0) <= 0 && (doc.ExecutionDurationDays ?? 0) <= 0)
        {
            return "يجب تحديد المدة القصوى لتنفيذ المشروع";
        }

        if (doc.SiteHandoverMode == null)
        {
            return "يجب تحديد ما إذا كانت أرضية المشروع مُسلَّمة للمقاول أم لا";
        }

        if (!IsContractingProject(project.ProjectNature))
        {
            return null;
        }

        // من هنا: مشروع «مقاولات» — الدفعة المقدمة إلزامية
        if (!doc.AdvancePaymentDone)
        {
            return "يجب تأكيد صرف الدفعة المقدمة للمقاول قبل إكمال هذه المرحلة";
        }

        var percentage = doc.AdvancePaymentPercentage ?? 0m;
        if (percentage <= 0m || percentage > 100m)
        {
            return "نسبة الدفعة المقدمة يجب أن تكون بين 1% و100%";
        }

        var totalCost = project.BankFunding + project.SelfFunding;
        var expected = Math.Round(totalCost * percentage / 100m, 2);
        var self = doc.AdvancePaymentSelfAmount ?? 0m;
        var bank = doc.AdvancePaymentBankAmount ?? 0m;

        if (Math.Round(self + bank, 2) != expected)
        {
            return $"مجموع المصروف ذاتيًا وبنكيًا يجب أن يساوي قيمة الدفعة المقدمة ({expected:N2} ج.م)";
        }

        if (self > project.SelfFunding)
        {
            return $"المصروف من التمويل الذاتي يتجاوز المتاح ({project.SelfFunding:N2} ج.م)";
        }

        if (bank > project.BankFunding)
        {
            return $"المصروف من التمويل البنكي يتجاوز المتاح ({project.BankFunding:N2} ج.م)";
        }

        var latestHasProof = await db.ContractAwardVersions.AsNoTracking()
            .Where(v => v.ContractAwardId == doc.Id && v.VersionNumber == doc.CurrentVersionNumber)
            .Select(v => v.AdvancePaymentProof != null)
            .FirstOrDefaultAsync(ct);

        return latestHasProof ? null : "يجب رفع إثبات صرف الدفعة المقدمة قبل إكمال هذه المرحلة";
    }

    // ------------------------------------------------------------------
    // تجريد المرحلة (مستند + إصدارات) بشكل عام على أنواع الكيانات
    // ------------------------------------------------------------------

    internal sealed record StageDocState(int Id, int CurrentVersionNumber, bool IsCompleted, DateTime? LastUpdatedAt);

    internal sealed record SlotInfo(string Key, string Label, bool Required);

    internal interface IStageOps
    {
        string Label { get; }
        IReadOnlyList<SlotInfo> Slots { get; }
        Task<StageDocState?> FindDocAsync(int subProjectId, CancellationToken ct);
        Task<List<ProcurementVersionDto>> GetVersionDtosAsync(int documentId, CancellationToken ct);
        Task<ProcurementVersionDto> UploadAsync(int subProjectId, Dictionary<string, StoredFile> files, string? notes, CancellationToken ct);
        Task<StoredFile?> GetFileAsync(int subProjectId, int versionNumber, string fileKey, CancellationToken ct);
        Task SetCompletionAsync(int subProjectId, bool isCompleted, CancellationToken ct);

        /// <summary>لو المرحلة مكتملة يرجعها "غير مكتملة" بصمت (بدون التحقق من الشروط) — تُستخدم عند إعادة فتح مرحلة سابقة فتُلغي المراحل التالية تبعًا لها. لا تفعل شيئًا لو المرحلة لم تبدأ أصلاً.</summary>
        Task ResetIfCompletedAsync(int subProjectId, CancellationToken ct);
    }

    internal sealed class FileSlot<TVer>
    {
        public FileSlot(string key, string label, Func<TVer, StoredFile?> get, Action<TVer, StoredFile?> set, bool required = false)
        {
            Key = key;
            Label = label;
            Get = get;
            Set = set;
            Required = required;
        }

        public string Key { get; }
        public string Label { get; }
        public bool Required { get; }
        public Func<TVer, StoredFile?> Get { get; }
        public Action<TVer, StoredFile?> Set { get; }

        /// <summary>مرحلة بملف واحد: الملف إجباري.</summary>
        public static FileSlot<TVer> Single(string key, string label, Func<TVer, StoredFile?> get, Action<TVer, StoredFile?> set) =>
            new(key, label, get, set, required: true);
    }

    internal sealed class StageOps<TDoc, TVer> : IStageOps
        where TDoc : SubProjectDocumentBase, new()
        where TVer : DocumentVersionBase, new()
    {
        private readonly AppDbContext _db;
        private readonly Action<TVer, int> _setDocId;
        private readonly Func<int, Expression<Func<TVer, bool>>> _versionsOfDoc;
        private readonly IReadOnlyList<FileSlot<TVer>> _slots;

        /// <summary>
        /// تحقق إضافي عند الإكمال. غير متزامن لأن بعض القواعد تحتاج قراءة من القاعدة
        /// (مثل نوع المشروع لتحديد إلزامية الدفعة المقدمة).
        /// </summary>
        private readonly Func<TDoc, CancellationToken, Task<string?>>? _extraCompletionCheck;

        public StageOps(
            AppDbContext db,
            string label,
            Action<TVer, int> setDocId,
            Func<int, Expression<Func<TVer, bool>>> versionsOfDoc,
            IReadOnlyList<FileSlot<TVer>> slots,
            Func<TDoc, CancellationToken, Task<string?>>? extraCompletionCheck = null)
        {
            _db = db;
            Label = label;
            _setDocId = setDocId;
            _versionsOfDoc = versionsOfDoc;
            _slots = slots;
            _extraCompletionCheck = extraCompletionCheck;
        }

        public string Label { get; }

        public IReadOnlyList<SlotInfo> Slots =>
            _slots.Select(s => new SlotInfo(s.Key, s.Label, s.Required)).ToList();

        public async Task<StageDocState?> FindDocAsync(int subProjectId, CancellationToken ct)
        {
            return await _db.Set<TDoc>().AsNoTracking()
                .Where(d => d.SubProjectId == subProjectId)
                .Select(d => new StageDocState(d.Id, d.CurrentVersionNumber, d.IsCompleted, d.UpdatedAt ?? d.CreatedAt))
                .FirstOrDefaultAsync(ct);
        }

        public async Task<List<ProcurementVersionDto>> GetVersionDtosAsync(int documentId, CancellationToken ct)
        {
            var versions = await _db.Set<TVer>().AsNoTracking()
                .Where(_versionsOfDoc(documentId))
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync(ct);

            return versions.Select(ToDto).ToList();
        }

        public async Task<ProcurementVersionDto> UploadAsync(int subProjectId, Dictionary<string, StoredFile> files, string? notes, CancellationToken ct)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var doc = await _db.Set<TDoc>()
                .FirstOrDefaultAsync(d => d.SubProjectId == subProjectId, ct);

            if (doc == null)
            {
                doc = new TDoc { SubProjectId = subProjectId };
                _db.Set<TDoc>().Add(doc);
                await _db.SaveChangesAsync(ct);
            }

            if (doc.IsCompleted)
            {
                throw new BusinessRuleException("هذه المرحلة مكتملة — يجب إعادة فتحها قبل إضافة إصدار جديد");
            }

            doc.CurrentVersionNumber += 1;

            var version = new TVer
            {
                VersionNumber = doc.CurrentVersionNumber,
                Notes = notes,
            };
            _setDocId(version, doc.Id);

            foreach (var slot in _slots)
            {
                if (files.TryGetValue(slot.Key, out var file))
                {
                    slot.Set(version, file);
                }
            }

            _db.Set<TVer>().Add(version);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ToDto(version);
        }

        public async Task<StoredFile?> GetFileAsync(int subProjectId, int versionNumber, string fileKey, CancellationToken ct)
        {
            var slot = _slots.FirstOrDefault(s => string.Equals(s.Key, fileKey, StringComparison.OrdinalIgnoreCase));
            if (slot == null)
            {
                return null;
            }

            var docId = await _db.Set<TDoc>().AsNoTracking()
                .Where(d => d.SubProjectId == subProjectId)
                .Select(d => (int?)d.Id)
                .FirstOrDefaultAsync(ct);

            if (docId == null)
            {
                return null;
            }

            var version = await _db.Set<TVer>().AsNoTracking()
                .Where(_versionsOfDoc(docId.Value))
                .Where(v => v.VersionNumber == versionNumber)
                .FirstOrDefaultAsync(ct);

            return version == null ? null : slot.Get(version);
        }

        public async Task SetCompletionAsync(int subProjectId, bool isCompleted, CancellationToken ct)
        {
            var doc = await _db.Set<TDoc>()
                .FirstOrDefaultAsync(d => d.SubProjectId == subProjectId, ct)
                ?? throw new NotFoundException("لم تبدأ هذه المرحلة بعد — لا يوجد مستند");

            if (isCompleted)
            {
                if (doc.CurrentVersionNumber == 0)
                {
                    throw new BusinessRuleException("لا يمكن إكمال مرحلة بدون أي إصدار مرفوع");
                }

                var latestVersion = await _db.Set<TVer>().AsNoTracking()
                    .Where(_versionsOfDoc(doc.Id))
                    .FirstOrDefaultAsync(v => v.VersionNumber == doc.CurrentVersionNumber, ct);

                foreach (var slot in _slots.Where(s => s.Required))
                {
                    if (latestVersion == null || slot.Get(latestVersion) == null)
                    {
                        throw new BusinessRuleException($"يجب رفع ملف \"{slot.Label}\" قبل إكمال هذه المرحلة");
                    }
                }

                if (_extraCompletionCheck != null)
                {
                    var error = await _extraCompletionCheck(doc, ct);
                    if (error != null)
                    {
                        throw new BusinessRuleException(error);
                    }
                }
            }

            doc.IsCompleted = isCompleted;
            await _db.SaveChangesAsync(ct);
        }

        public async Task ResetIfCompletedAsync(int subProjectId, CancellationToken ct)
        {
            var doc = await _db.Set<TDoc>()
                .FirstOrDefaultAsync(d => d.SubProjectId == subProjectId, ct);

            if (doc != null && doc.IsCompleted)
            {
                doc.IsCompleted = false;
                await _db.SaveChangesAsync(ct);
            }
        }

        private ProcurementVersionDto ToDto(TVer version)
        {
            var dto = new ProcurementVersionDto
            {
                Id = version.Id,
                VersionNumber = version.VersionNumber,
                Notes = version.Notes,
                CreatedAt = version.CreatedAt,
            };

            foreach (var slot in _slots)
            {
                var file = slot.Get(version);
                if (file != null)
                {
                    dto.Files.Add(new ProcurementFileDto
                    {
                        Key = slot.Key,
                        Label = slot.Label,
                        FileName = file.FileName,
                        FileExtension = file.FileExtension,
                        FileSize = file.FileSize,
                    });
                }
            }

            return dto;
        }
    }
}
