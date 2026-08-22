using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Services;

/// <summary>
/// سجل الإتاحات البنكية لكل سنة مالية. يستخدم AppDbContext مباشرة (نفس نمط ProcurementService)
/// لأن إضافة أو تعديل إتاحة تحتاج معاملة صريحة (Serializable) تمنع تجاوز سقف التمويل البنكي
/// عند الإضافة أو التعديل المتزامن.
/// </summary>
public class BankAvailabilityService : IBankAvailabilityService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx", ".xls", ".xlsx",
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private const int MaxFilesPerAvailability = 5;

    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public BankAvailabilityService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<BankAvailabilityListDto> GetForFinancialYearAsync(int financialYearId, CancellationToken cancellationToken = default)
    {
        var yearExists = await _context.FinancialYears.AsNoTracking()
            .AnyAsync(y => y.FinancialYearId == financialYearId, cancellationToken);
        if (!yearExists)
        {
            throw new NotFoundException($"السنة المالية رقم {financialYearId} غير موجودة");
        }

        var totalBankFunding = await GetTotalBankFundingAsync(financialYearId, cancellationToken);

        var items = await _context.BankAvailabilities.AsNoTracking()
            .Where(a => a.FinancialYearId == financialYearId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new BankAvailabilityDto
            {
                Id = a.BankAvailabilityId,
                FinancialYearId = a.FinancialYearId,
                Amount = a.Amount,
                ReceivedDate = a.ReceivedDate,
                CreatedAt = a.CreatedAt,
                Notes = a.Notes,
                Documents = a.Documents.Select(d => new BankAvailabilityDocumentDto
                {
                    Id = d.BankAvailabilityDocumentId,
                    FileName = d.File.FileName,
                }).ToList(),
            })
            .ToListAsync(cancellationToken);

        var receipts = items.Sum(x => x.Amount);
        var advancesSpent = await BankSpendCalculator.GetAdvancePaymentsSpentAsync(_context, financialYearId, cancellationToken);
        var executionSpent = await BankSpendCalculator.GetExecutionBankSpendAsync(_context, financialYearId, cancellationToken);
        var totalAvailable = receipts - advancesSpent - executionSpent;

        return new BankAvailabilityListDto
        {
            TotalAvailable = totalAvailable,
            TotalBankFunding = totalBankFunding,
            RemainingAvailable = totalBankFunding - receipts,
            TotalReceived = receipts,
            Items = items,
        };
    }

    public async Task<BankAvailabilityDto> CreateAsync(int financialYearId, CreateBankAvailabilityDto dto, CancellationToken cancellationToken = default)
    {
        var year = await _context.FinancialYears.AsNoTracking()
            .FirstOrDefaultAsync(y => y.FinancialYearId == financialYearId, cancellationToken)
            ?? throw new NotFoundException($"السنة المالية رقم {financialYearId} غير موجودة");

        if (year.IsClosed)
        {
            throw new BusinessRuleException("لا يمكن إضافة إتاحة بنكية لسنة مالية مقفولة");
        }

        if (dto.Amount <= 0)
        {
            throw new BusinessRuleException("قيمة الإتاحة يجب أن تكون أكبر من صفر");
        }

        if (dto.ReceivedDate.Date < year.StartDate.Date || dto.ReceivedDate.Date > year.EndDate.Date)
        {
            throw new BusinessRuleException("تاريخ استلام الإتاحة يجب أن يقع ضمن فترة السنة المالية");
        }

        if (dto.Documents.Count == 0)
        {
            throw new BusinessRuleException("يجب إرفاق مستند إثبات واحد على الأقل");
        }
        if (dto.Documents.Count > MaxFilesPerAvailability)
        {
            throw new BusinessRuleException($"لا يمكن إرفاق أكثر من {MaxFilesPerAvailability} مستندات لكل إتاحة");
        }

        foreach (var file in dto.Documents)
        {
            var extension = file.FileExtension ?? string.Empty;
            if (!AllowedExtensions.Contains(extension))
            {
                throw new BusinessRuleException($"صيغة الملف '{file.FileName}' غير مدعومة");
            }
            if (file.FileSize > MaxFileSizeBytes)
            {
                throw new BusinessRuleException($"حجم الملف '{file.FileName}' يتجاوز الحد المسموح (10 ميجابايت)");
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var totalBankFunding = await GetTotalBankFundingAsync(financialYearId, cancellationToken);
        var existingTotal = await _context.BankAvailabilities.AsNoTracking()
            .Where(a => a.FinancialYearId == financialYearId)
            .SumAsync(a => (decimal?)a.Amount, cancellationToken) ?? 0m;

        var newTotal = existingTotal + dto.Amount;
        if (newTotal > totalBankFunding)
        {
            throw new BusinessRuleException(
                $"إجمالي الإتاحات ({newTotal:N2} ج.م) يتجاوز إجمالي التمويل البنكي المسجل لهذه السنة ({totalBankFunding:N2} ج.م)");
        }

        var availability = new BankAvailability
        {
            FinancialYearId = financialYearId,
            Amount = dto.Amount,
            ReceivedDate = dto.ReceivedDate.Date,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedByUserId = _currentUser.UserId,
        };

        foreach (var file in dto.Documents)
        {
            availability.Documents.Add(new BankAvailabilityDocument
            {
                File = new StoredFile
                {
                    FileName = file.FileName,
                    FileExtension = file.FileExtension,
                    FileSize = file.FileSize,
                    Content = file.Content,
                },
            });
        }

        _context.BankAvailabilities.Add(availability);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BankAvailabilityDto
        {
            Id = availability.BankAvailabilityId,
            FinancialYearId = availability.FinancialYearId,
            Amount = availability.Amount,
            ReceivedDate = availability.ReceivedDate,
            CreatedAt = availability.CreatedAt,
            Notes = availability.Notes,
            Documents = availability.Documents.Select(d => new BankAvailabilityDocumentDto
            {
                Id = d.BankAvailabilityDocumentId,
                FileName = d.File.FileName,
            }).ToList(),
        };
    }

    public async Task<BankAvailabilityDto> UpdateAsync(int financialYearId, int availabilityId, UpdateBankAvailabilityDto dto, CancellationToken cancellationToken = default)
    {
        var year = await _context.FinancialYears.AsNoTracking()
            .FirstOrDefaultAsync(y => y.FinancialYearId == financialYearId, cancellationToken)
            ?? throw new NotFoundException($"السنة المالية رقم {financialYearId} غير موجودة");

        if (year.IsClosed)
        {
            throw new BusinessRuleException("لا يمكن تعديل إتاحة بنكية لسنة مالية مقفولة");
        }

        if (dto.Amount <= 0)
        {
            throw new BusinessRuleException("قيمة الإتاحة يجب أن تكون أكبر من صفر");
        }

        if (dto.ReceivedDate.Date < year.StartDate.Date || dto.ReceivedDate.Date > year.EndDate.Date)
        {
            throw new BusinessRuleException("تاريخ استلام الإتاحة يجب أن يقع ضمن فترة السنة المالية");
        }

        foreach (var file in dto.NewDocuments)
        {
            var extension = file.FileExtension ?? string.Empty;
            if (!AllowedExtensions.Contains(extension))
            {
                throw new BusinessRuleException($"صيغة الملف '{file.FileName}' غير مدعومة");
            }
            if (file.FileSize > MaxFileSizeBytes)
            {
                throw new BusinessRuleException($"حجم الملف '{file.FileName}' يتجاوز الحد المسموح (10 ميجابايت)");
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var availability = await _context.BankAvailabilities
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.BankAvailabilityId == availabilityId && a.FinancialYearId == financialYearId, cancellationToken)
            ?? throw new NotFoundException($"الإتاحة البنكية رقم {availabilityId} غير موجودة لهذه السنة المالية");

        var keepIds = dto.KeepDocumentIds.ToHashSet();
        var invalidIds = keepIds.Except(availability.Documents.Select(d => d.BankAvailabilityDocumentId)).ToList();
        if (invalidIds.Count > 0)
        {
            throw new BusinessRuleException("أحد معرفات المستندات المُرسلة غير تابع لهذه الإتاحة");
        }

        var documentsToRemove = availability.Documents.Where(d => !keepIds.Contains(d.BankAvailabilityDocumentId)).ToList();
        var finalDocumentCount = keepIds.Count + dto.NewDocuments.Count;

        if (finalDocumentCount == 0)
        {
            throw new BusinessRuleException("يجب أن تحتفظ الإتاحة بمستند إثبات واحد على الأقل");
        }
        if (finalDocumentCount > MaxFilesPerAvailability)
        {
            throw new BusinessRuleException($"لا يمكن أن يتجاوز عدد المستندات {MaxFilesPerAvailability} لكل إتاحة");
        }

        var totalBankFunding = await GetTotalBankFundingAsync(financialYearId, cancellationToken);
        var otherAvailabilitiesTotal = await _context.BankAvailabilities.AsNoTracking()
            .Where(a => a.FinancialYearId == financialYearId && a.BankAvailabilityId != availabilityId)
            .SumAsync(a => (decimal?)a.Amount, cancellationToken) ?? 0m;

        var newTotal = otherAvailabilitiesTotal + dto.Amount;
        if (newTotal > totalBankFunding)
        {
            throw new BusinessRuleException(
                $"إجمالي الإتاحات ({newTotal:N2} ج.م) يتجاوز إجمالي التمويل البنكي المسجل لهذه السنة ({totalBankFunding:N2} ج.م)");
        }

        foreach (var doc in documentsToRemove)
        {
            availability.Documents.Remove(doc);
            _context.BankAvailabilityDocuments.Remove(doc);
        }

        foreach (var file in dto.NewDocuments)
        {
            availability.Documents.Add(new BankAvailabilityDocument
            {
                File = new StoredFile
                {
                    FileName = file.FileName,
                    FileExtension = file.FileExtension,
                    FileSize = file.FileSize,
                    Content = file.Content,
                },
            });
        }

        availability.Amount = dto.Amount;
        availability.ReceivedDate = dto.ReceivedDate.Date;
        availability.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BankAvailabilityDto
        {
            Id = availability.BankAvailabilityId,
            FinancialYearId = availability.FinancialYearId,
            Amount = availability.Amount,
            ReceivedDate = availability.ReceivedDate,
            CreatedAt = availability.CreatedAt,
            Notes = availability.Notes,
            Documents = availability.Documents.Select(d => new BankAvailabilityDocumentDto
            {
                Id = d.BankAvailabilityDocumentId,
                FileName = d.File.FileName,
            }).ToList(),
        };
    }

    public async Task DeleteAsync(int financialYearId, int availabilityId, CancellationToken cancellationToken = default)
    {
        var year = await _context.FinancialYears.AsNoTracking()
            .FirstOrDefaultAsync(y => y.FinancialYearId == financialYearId, cancellationToken)
            ?? throw new NotFoundException($"السنة المالية رقم {financialYearId} غير موجودة");

        if (year.IsClosed)
        {
            throw new BusinessRuleException("لا يمكن حذف إتاحة بنكية من سنة مالية مقفولة");
        }

        var availability = await _context.BankAvailabilities
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.BankAvailabilityId == availabilityId && a.FinancialYearId == financialYearId, cancellationToken)
            ?? throw new NotFoundException($"الإتاحة البنكية رقم {availabilityId} غير موجودة لهذه السنة المالية");

        _context.BankAvailabilityDocuments.RemoveRange(availability.Documents);
        _context.BankAvailabilities.Remove(availability);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FileDownloadDto> DownloadDocumentAsync(int financialYearId, int availabilityId, int documentId, CancellationToken cancellationToken = default)
    {
        var file = await _context.BankAvailabilityDocuments.AsNoTracking()
            .Where(d => d.BankAvailabilityDocumentId == documentId
                && d.BankAvailabilityId == availabilityId
                && d.BankAvailability.FinancialYearId == financialYearId)
            .Select(d => new FileDownloadDto
            {
                FileName = d.File.FileName,
                FileExtension = d.File.FileExtension,
                Content = d.File.Content,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (file == null)
        {
            throw new NotFoundException("المستند المطلوب غير موجود");
        }

        return file;
    }

    /// <summary>سقف الإتاحات البنكية لسنة مالية — مجموع التمويل البنكي المخطط للمشروعات
    /// المعتمدة فقط. مشروع مقترح غير معتمد لا يُحسب ضمن السقف حتى لا يُتاح تمويل بنكي
    /// لمشروع قد لا يُعتمد أصلًا.</summary>
    private async Task<decimal> GetTotalBankFundingAsync(int financialYearId, CancellationToken cancellationToken)
    {
        return await _context.SubProjects.AsNoTracking()
            .Where(s => s.IsApproved && s.FinancialYears.Any(fy => fy.FinancialYearId == financialYearId))
            .SumAsync(s => s.BankFunding, cancellationToken);
    }
}
