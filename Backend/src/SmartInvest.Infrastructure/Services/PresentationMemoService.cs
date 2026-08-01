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

    private readonly AppDbContext _context;

    public PresentationMemoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PresentationMemoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PresentationMemos.AsNoTracking()
            .OrderByDescending(m => m.Id)
            .Select(m => new PresentationMemoDto
            {
                Id = m.Id,
                Title = m.Title,
                CurrentVersionNumber = m.CurrentVersionNumber,
                IsCompleted = m.IsCompleted,
                CreatedAt = m.CreatedAt,
                SubProjects = m.MemoSubProjects
                    .Select(x => new MemoSubProjectDto
                    {
                        SubProjectId = x.SubProjectId,
                        SubProjectName = x.SubProject.SubProjectName,
                        SubProjectCode = x.SubProject.SubProjectCode,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PresentationMemoDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var memo = await _context.PresentationMemos.AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new PresentationMemoDetailDto
            {
                Id = m.Id,
                Title = m.Title,
                CurrentVersionNumber = m.CurrentVersionNumber,
                IsCompleted = m.IsCompleted,
                CreatedAt = m.CreatedAt,
                SubProjects = m.MemoSubProjects
                    .Select(x => new MemoSubProjectDto
                    {
                        SubProjectId = x.SubProjectId,
                        SubProjectName = x.SubProject.SubProjectName,
                        SubProjectCode = x.SubProject.SubProjectCode,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"مذكرة العرض رقم {id} غير موجودة");

        memo.Versions = await _context.PresentationMemoVersions.AsNoTracking()
            .Where(v => v.PresentationMemoId == id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ProcurementVersionDto
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                Notes = v.Notes,
                CreatedAt = v.CreatedAt,
                Files = new List<ProcurementFileDto>
                {
                    new ProcurementFileDto
                    {
                        Key = FileKey,
                        Label = FileLabel,
                        FileName = v.File.FileName,
                        FileExtension = v.File.FileExtension,
                        FileSize = v.File.FileSize,
                    },
                },
            })
            .ToListAsync(cancellationToken);

        return memo;
    }

    public async Task<PresentationMemoDto> CreateAsync(CreatePresentationMemoDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw new BusinessRuleException("عنوان مذكرة العرض مطلوب");
        }

        var subProjectIds = dto.SubProjectIds.Distinct().ToList();
        if (subProjectIds.Count == 0)
        {
            throw new BusinessRuleException("يجب ربط مذكرة العرض بمشروع فرعي واحد على الأقل");
        }

        await EnsureSubProjectsExistAsync(subProjectIds, cancellationToken);

        var memo = new PresentationMemo { Title = dto.Title.Trim() };
        foreach (var subProjectId in subProjectIds)
        {
            memo.MemoSubProjects.Add(new PresentationMemoSubProject { SubProjectId = subProjectId });
        }

        _context.PresentationMemos.Add(memo);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(memo.Id, cancellationToken);
    }

    public async Task<PresentationMemoDto> UpdateAsync(int id, UpdatePresentationMemoDto dto, CancellationToken cancellationToken = default)
    {
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

        var subProjectIds = dto.SubProjectIds.Distinct().ToList();
        if (subProjectIds.Count == 0)
        {
            throw new BusinessRuleException("يجب ربط مذكرة العرض بمشروع فرعي واحد على الأقل");
        }

        await EnsureSubProjectsExistAsync(subProjectIds, cancellationToken);

        memo.Title = dto.Title.Trim();

        var toRemove = memo.MemoSubProjects.Where(x => !subProjectIds.Contains(x.SubProjectId)).ToList();
        foreach (var link in toRemove)
        {
            _context.PresentationMemoSubProjects.Remove(link);
        }

        var existingIds = memo.MemoSubProjects.Select(x => x.SubProjectId).ToHashSet();
        foreach (var subProjectId in subProjectIds.Where(x => !existingIds.Contains(x)))
        {
            memo.MemoSubProjects.Add(new PresentationMemoSubProject { SubProjectId = subProjectId });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
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

        _context.PresentationMemoVersions.Add(version);
        await _context.SaveChangesAsync(cancellationToken);

        return new ProcurementVersionDto
        {
            Id = version.Id,
            VersionNumber = version.VersionNumber,
            Notes = version.Notes,
            CreatedAt = version.CreatedAt,
            Files =
            [
                new ProcurementFileDto
                {
                    Key = FileKey,
                    Label = FileLabel,
                    FileName = version.File.FileName,
                    FileExtension = version.File.FileExtension,
                    FileSize = version.File.FileSize,
                },
            ],
        };
    }

    public async Task<FileDownloadDto> DownloadFileAsync(int id, int versionNumber, CancellationToken cancellationToken = default)
    {
        var version = await _context.PresentationMemoVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.PresentationMemoId == id && v.VersionNumber == versionNumber, cancellationToken)
            ?? throw new NotFoundException("الإصدار المطلوب غير موجود");

        return new FileDownloadDto
        {
            FileName = version.File.FileName,
            FileExtension = version.File.FileExtension,
            Content = version.File.Content,
        };
    }

    public async Task SetCompletionAsync(int id, bool isCompleted, CancellationToken cancellationToken = default)
    {
        var memo = await _context.PresentationMemos
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"مذكرة العرض رقم {id} غير موجودة");

        if (isCompleted && memo.CurrentVersionNumber == 0)
        {
            throw new BusinessRuleException("لا يمكن إكمال مذكرة عرض بدون أي إصدار مرفوع");
        }

        memo.IsCompleted = isCompleted;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSubProjectsExistAsync(List<int> subProjectIds, CancellationToken cancellationToken)
    {
        var found = await _context.SubProjects.AsNoTracking()
            .Where(s => subProjectIds.Contains(s.SubProjectId))
            .Select(s => s.SubProjectId)
            .ToListAsync(cancellationToken);

        var missing = subProjectIds.Except(found).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {missing[0]} غير موجود");
        }
    }
}
