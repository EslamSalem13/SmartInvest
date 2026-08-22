using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Services;

public class PresentationMemoService : IPresentationMemoService
{
    private const string FileKey = "file";
    private const string FileLabel = "ملف مذكرة العرض";

    private const string LegalDecisionKey = "legal-affairs-decision";
    private const string LegalDecisionLabel = "قرار لجنة الشؤون القانونية";

    private readonly AppDbContext _context;

    public PresentationMemoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PresentationMemoDto>> GetAllAsync(int? financialYearId = null, CancellationToken cancellationToken = default)
    {
        var memos = await _context.PresentationMemos.AsNoTracking()
            .Where(m => financialYearId == null || m.FinancialYearId == financialYearId)
            .OrderByDescending(m => m.Id)
            .Select(m => new PresentationMemoDto
            {
                Id = m.Id,
                FinancialYearId = m.FinancialYearId,
                FinancialYearName = m.FinancialYear != null ? m.FinancialYear.Name : null,
                Title = m.Title,
                CurrentVersionNumber = m.CurrentVersionNumber,
                IsCompleted = m.IsCompleted,
                CreatedAt = m.CreatedAt,
                ContractingMethod = (int?)m.ContractingMethod,
                SubProjects = m.MemoSubProjects
                    .Select(x => new MemoSubProjectDto
                    {
                        SubProjectId = x.SubProjectId,
                        SubProjectName = x.SubProject.SubProjectName,
                        SubProjectCode = x.SubProject.SubProjectCode,
                        ProjectNature = x.SubProject.ProjectNature,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        // التسمية بحث في قاموس — لا تُترجم إلى SQL، فتُملأ بعد التنفيذ
        foreach (var item in memos)
        {
            item.ContractingMethodLabel = ContractingMethodLabels.ToLabel((ContractingMethod?)item.ContractingMethod);
        }

        return memos;
    }

    public async Task<PresentationMemoDetailDto> GetByIdAsync(int id, int? financialYearId = null, CancellationToken cancellationToken = default)
    {
        var memo = await _context.PresentationMemos.AsNoTracking()
            .Where(m => m.Id == id && (financialYearId == null || m.FinancialYearId == financialYearId))
            .Select(m => new PresentationMemoDetailDto
            {
                Id = m.Id,
                FinancialYearId = m.FinancialYearId,
                FinancialYearName = m.FinancialYear != null ? m.FinancialYear.Name : null,
                Title = m.Title,
                CurrentVersionNumber = m.CurrentVersionNumber,
                IsCompleted = m.IsCompleted,
                CreatedAt = m.CreatedAt,
                ContractingMethod = (int?)m.ContractingMethod,
                SubProjects = m.MemoSubProjects
                    .Select(x => new MemoSubProjectDto
                    {
                        SubProjectId = x.SubProjectId,
                        SubProjectName = x.SubProject.SubProjectName,
                        SubProjectCode = x.SubProject.SubProjectCode,
                        ProjectNature = x.SubProject.ProjectNature,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"مذكرة العرض رقم {id} غير موجودة");

        memo.ContractingMethodLabel = ContractingMethodLabels.ToLabel((ContractingMethod?)memo.ContractingMethod);

        memo.Versions = await _context.PresentationMemoVersions.AsNoTracking()
            .Where(v => v.PresentationMemoId == id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ProcurementVersionDto
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                Notes = v.Notes,
                CreatedAt = v.CreatedAt,
                LegalAffairsDecisionUploadedAt = v.LegalAffairsDecisionUploadedAt,
                Files = v.LegalAffairsCommitteeDecision == null
                    ? new List<ProcurementFileDto>
                    {
                        new ProcurementFileDto
                        {
                            Key = FileKey,
                            Label = FileLabel,
                            FileName = v.File.FileName,
                            FileExtension = v.File.FileExtension,
                            FileSize = v.File.FileSize,
                        },
                    }
                    : new List<ProcurementFileDto>
                    {
                        new ProcurementFileDto
                        {
                            Key = FileKey,
                            Label = FileLabel,
                            FileName = v.File.FileName,
                            FileExtension = v.File.FileExtension,
                            FileSize = v.File.FileSize,
                        },
                        new ProcurementFileDto
                        {
                            Key = LegalDecisionKey,
                            Label = LegalDecisionLabel,
                            FileName = v.LegalAffairsCommitteeDecision.FileName,
                            FileExtension = v.LegalAffairsCommitteeDecision.FileExtension,
                            FileSize = v.LegalAffairsCommitteeDecision.FileSize,
                        },
                    },
            })
            .ToListAsync(cancellationToken);

        return memo;
    }

    /// <summary>طريقة التعاقد إلزامية على أي إنشاء أو تعديل — الـ null مسموح في القاعدة فقط للمذكرات القديمة.</summary>
    private static ContractingMethod ParseContractingMethod(int? value)
    {
        if (value is not int method)
        {
            throw new BusinessRuleException("طريقة التعاقد مطلوبة");
        }

        if (!ContractingMethodLabels.IsDefined(method))
        {
            throw new BusinessRuleException("طريقة التعاقد غير معروفة");
        }

        return (ContractingMethod)method;
    }

    public async Task<PresentationMemoDto> CreateAsync(CreatePresentationMemoDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureFinancialYearExistsAsync(dto.FinancialYearId, cancellationToken);
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new BusinessRuleException("عنوان مذكرة العرض مطلوب");
        }

        var subProjectIds = dto.SubProjectIds.Distinct().ToList();
        if (subProjectIds.Count == 0)
        {
            throw new BusinessRuleException("يجب ربط مذكرة العرض بمشروع فرعي واحد على الأقل");
        }

        await EnsureSubProjectsBelongToYearAsync(subProjectIds, dto.FinancialYearId, cancellationToken);

        var memo = new PresentationMemo
        {
            FinancialYearId = dto.FinancialYearId,
            Title = dto.Title.Trim(),
            ContractingMethod = ParseContractingMethod(dto.ContractingMethod),
        };
        foreach (var subProjectId in subProjectIds)
        {
            memo.MemoSubProjects.Add(new PresentationMemoSubProject { SubProjectId = subProjectId });
        }

        _context.PresentationMemos.Add(memo);
        await ActivateTenderStagesAsync(subProjectIds, DateTime.UtcNow, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(memo.Id, dto.FinancialYearId, cancellationToken);
    }

    public async Task<PresentationMemoDto> UpdateAsync(int id, UpdatePresentationMemoDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureFinancialYearExistsAsync(dto.FinancialYearId, cancellationToken);
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new BusinessRuleException("عنوان مذكرة العرض مطلوب");
        }

        var memo = await _context.PresentationMemos
            .Include(m => m.MemoSubProjects)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"مذكرة العرض رقم {id} غير موجودة");

        if (memo.IsCompleted)
        {
            throw new BusinessRuleException("مذكرة العرض مكتملة — يجب إعادة فتحها قبل التعديل");
        }

        if (memo.FinancialYearId.HasValue && memo.FinancialYearId != dto.FinancialYearId)
        {
            throw new BusinessRuleException("لا يمكن نقل مذكرة العرض إلى سنة مالية أخرى");
        }

        var subProjectIds = dto.SubProjectIds.Distinct().ToList();
        if (subProjectIds.Count == 0)
        {
            throw new BusinessRuleException("يجب ربط مذكرة العرض بمشروع فرعي واحد على الأقل");
        }

        await EnsureSubProjectsBelongToYearAsync(subProjectIds, dto.FinancialYearId, cancellationToken);

        memo.Title = dto.Title.Trim();
        memo.FinancialYearId = dto.FinancialYearId;
        memo.ContractingMethod = ParseContractingMethod(dto.ContractingMethod);

        var toRemove = memo.MemoSubProjects.Where(x => !subProjectIds.Contains(x.SubProjectId)).ToList();
        foreach (var link in toRemove)
        {
            _context.PresentationMemoSubProjects.Remove(link);
        }

        var existingIds = memo.MemoSubProjects.Select(x => x.SubProjectId).ToHashSet();
        var newlyLinkedIds = subProjectIds.Where(x => !existingIds.Contains(x)).ToList();
        foreach (var subProjectId in newlyLinkedIds)
        {
            memo.MemoSubProjects.Add(new PresentationMemoSubProject { SubProjectId = subProjectId });
        }

        await ActivateTenderStagesAsync(newlyLinkedIds, DateTime.UtcNow, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, dto.FinancialYearId, cancellationToken);
    }

    /// <summary>
    /// ربط المشروع بمذكرة العرض هو الحدث الذي يجعل أول مرحلة طرح متاحة وفق قواعد الخدمة الحالية.
    /// لذلك تُخزّن مدة الـ7 أيام ووقت بدايتها هنا مرة واحدة، وليس أثناء أي GET.
    /// </summary>
    private async Task ActivateTenderStagesAsync(
        IReadOnlyCollection<int> subProjectIds,
        DateTime activatedAt,
        CancellationToken cancellationToken)
    {
        if (subProjectIds.Count == 0)
        {
            return;
        }

        var existing = await _context.TenderDocuments
            .Where(x => subProjectIds.Contains(x.SubProjectId))
            .ToDictionaryAsync(x => x.SubProjectId, cancellationToken);

        foreach (var subProjectId in subProjectIds)
        {
            if (!existing.TryGetValue(subProjectId, out var document))
            {
                _context.TenderDocuments.Add(new TenderDocument
                {
                    SubProjectId = subProjectId,
                    DurationDays = ProcurementService.DefaultStageDurationDays,
                    DurationSetAt = activatedAt,
                });
                continue;
            }

            document.DurationDays ??= ProcurementService.DefaultStageDurationDays;
            document.DurationSetAt ??= activatedAt;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var memo = await _context.PresentationMemos
            .Include(m => m.MemoSubProjects)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"مذكرة العرض رقم {id} غير موجودة");

        var hasVersions = await _context.PresentationMemoVersions
            .AnyAsync(v => v.PresentationMemoId == id, cancellationToken);
        if (hasVersions)
        {
            throw new BusinessRuleException("لا يمكن حذف مذكرة عرض لها إصدارات مرفوعة — الإصدارات سجل رسمي لا يُحذف");
        }

        _context.PresentationMemoSubProjects.RemoveRange(memo.MemoSubProjects);
        _context.PresentationMemos.Remove(memo);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProcurementVersionDto> UploadVersionAsync(int id, UploadMemoVersionDto dto, CancellationToken cancellationToken = default)
    {
        var memo = await _context.PresentationMemos
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"مذكرة العرض رقم {id} غير موجودة");

        if (memo.IsCompleted)
        {
            throw new BusinessRuleException("مذكرة العرض مكتملة — يجب إعادة فتحها قبل إضافة إصدار جديد");
        }

        if (dto.File.Content.Length == 0)
        {
            throw new BusinessRuleException("ملف مذكرة العرض مطلوب");
        }

        memo.CurrentVersionNumber += 1;

        var version = new PresentationMemoVersion
        {
            PresentationMemoId = memo.Id,
            VersionNumber = memo.CurrentVersionNumber,
            Notes = dto.Notes,
            File = new StoredFile
            {
                FileName = dto.File.FileName,
                FileExtension = dto.File.FileExtension,
                FileSize = dto.File.FileSize,
                Content = dto.File.Content,
            },
        };

        if (dto.LegalAffairsCommitteeDecision is { Content.Length: > 0 } decision)
        {
            version.LegalAffairsCommitteeDecision = new StoredFile
            {
                FileName = decision.FileName,
                FileExtension = decision.FileExtension,
                FileSize = decision.FileSize,
                Content = decision.Content,
            };
            version.LegalAffairsDecisionUploadedAt = DateTime.UtcNow;
        }

        _context.PresentationMemoVersions.Add(version);
        await _context.SaveChangesAsync(cancellationToken);

        var files = new List<ProcurementFileDto>
        {
            new()
            {
                Key = FileKey,
                Label = FileLabel,
                FileName = version.File.FileName,
                FileExtension = version.File.FileExtension,
                FileSize = version.File.FileSize,
            },
        };

        if (version.LegalAffairsCommitteeDecision is not null)
        {
            files.Add(new ProcurementFileDto
            {
                Key = LegalDecisionKey,
                Label = LegalDecisionLabel,
                FileName = version.LegalAffairsCommitteeDecision.FileName,
                FileExtension = version.LegalAffairsCommitteeDecision.FileExtension,
                FileSize = version.LegalAffairsCommitteeDecision.FileSize,
            });
        }

        return new ProcurementVersionDto
        {
            Id = version.Id,
            VersionNumber = version.VersionNumber,
            Notes = version.Notes,
            CreatedAt = version.CreatedAt,
            LegalAffairsDecisionUploadedAt = version.LegalAffairsDecisionUploadedAt,
            Files = files,
        };
    }

    public async Task UploadLegalDecisionAsync(int id, FileUploadDto file, CancellationToken cancellationToken = default)
    {
        var memo = await _context.PresentationMemos
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"مذكرة العرض رقم {id} غير موجودة");

        if (memo.IsCompleted)
        {
            throw new BusinessRuleException("مذكرة العرض مكتملة — يجب إعادة فتحها قبل التعديل");
        }

        if (memo.CurrentVersionNumber == 0)
        {
            throw new BusinessRuleException("لا يوجد إصدار مرفوع لإرفاق القرار به");
        }

        if (file.Content.Length == 0)
        {
            throw new BusinessRuleException("ملف قرار لجنة الشؤون القانونية مطلوب");
        }

        // القرار يُرفق على الإصدار الحالي نفسه ولا يُنشئ إصدارًا جديدًا — القرار يصل بعد المذكرة،
        // وليس تعديلًا عليها. تاريخ الرفع يُسجَّل وقت الإدخال الفعلي.
        var version = await _context.PresentationMemoVersions
            .FirstOrDefaultAsync(v => v.PresentationMemoId == id && v.VersionNumber == memo.CurrentVersionNumber, cancellationToken)
            ?? throw new NotFoundException("الإصدار الحالي غير موجود");

        version.LegalAffairsCommitteeDecision = new StoredFile
        {
            FileName = file.FileName,
            FileExtension = file.FileExtension,
            FileSize = file.FileSize,
            Content = file.Content,
        };
        version.LegalAffairsDecisionUploadedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FileDownloadDto> DownloadFileAsync(int id, int versionNumber, string? fileKey = null, CancellationToken cancellationToken = default)
    {
        var version = await _context.PresentationMemoVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.PresentationMemoId == id && v.VersionNumber == versionNumber, cancellationToken)
            ?? throw new NotFoundException("الإصدار المطلوب غير موجود");

        var file = fileKey == LegalDecisionKey
            ? version.LegalAffairsCommitteeDecision
                ?? throw new NotFoundException("قرار لجنة الشؤون القانونية غير مرفوع لهذا الإصدار")
            : version.File;

        return new FileDownloadDto
        {
            FileName = file.FileName,
            FileExtension = file.FileExtension,
            Content = file.Content,
        };
    }

    public async Task SetCompletionAsync(int id, bool isCompleted, CancellationToken cancellationToken = default)
    {
        var memo = await _context.PresentationMemos
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"مذكرة العرض رقم {id} غير موجودة");

        if (isCompleted)
        {
            if (memo.CurrentVersionNumber == 0)
            {
                throw new BusinessRuleException("لا يمكن إكمال مذكرة عرض بدون أي إصدار مرفوع");
            }

            // القرار مطلوب على أحدث إصدار تحديدًا — إصدار جديد بدون القرار يعني أن المذكرة تغيّرت بعد صدوره
            var latestHasDecision = await _context.PresentationMemoVersions.AsNoTracking()
                .Where(v => v.PresentationMemoId == id && v.VersionNumber == memo.CurrentVersionNumber)
                .Select(v => v.LegalAffairsCommitteeDecision != null)
                .FirstOrDefaultAsync(cancellationToken);

            if (!latestHasDecision)
            {
                throw new BusinessRuleException("يجب إرفاق قرار لجنة الشؤون القانونية قبل إكمال مذكرة العرض");
            }
        }

        memo.IsCompleted = isCompleted;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureFinancialYearExistsAsync(int financialYearId, CancellationToken cancellationToken)
    {
        if (financialYearId <= 0 || !await _context.FinancialYears.AsNoTracking()
                .AnyAsync(x => x.FinancialYearId == financialYearId, cancellationToken))
        {
            throw new BusinessRuleException("السنة المالية مطلوبة ويجب أن تكون صحيحة");
        }
    }

    private async Task EnsureSubProjectsBelongToYearAsync(List<int> subProjectIds, int financialYearId, CancellationToken cancellationToken)
    {
        var found = await _context.Set<SubProjectFinancialYear>().AsNoTracking()
            .Where(x => x.FinancialYearId == financialYearId && subProjectIds.Contains(x.SubProjectId))
            .Select(s => s.SubProjectId)
            .ToListAsync(cancellationToken);

        var missing = subProjectIds.Except(found).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {missing[0]} غير موجود");
        }
    }
}
