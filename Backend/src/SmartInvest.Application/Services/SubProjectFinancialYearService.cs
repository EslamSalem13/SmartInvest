using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class SubProjectFinancialYearService : ISubProjectFinancialYearService
{
    private readonly IGenericRepository<SubProjectFinancialYear> _linkRepository;
    private readonly IGenericRepository<SubProject> _subProjectRepository;
    private readonly IGenericRepository<FinancialYear> _financialYearRepository;
    private readonly IGenericRepository<ProjectFollowUp> _followUpRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SubProjectFinancialYearService(
        IGenericRepository<SubProjectFinancialYear> linkRepository,
        IGenericRepository<SubProject> subProjectRepository,
        IGenericRepository<FinancialYear> financialYearRepository,
        IGenericRepository<ProjectFollowUp> followUpRepository,
        IUnitOfWork unitOfWork)
    {
        _linkRepository = linkRepository;
        _subProjectRepository = subProjectRepository;
        _financialYearRepository = financialYearRepository;
        _followUpRepository = followUpRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SubProjectFinancialYearDto>> GetForSubProjectAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var links = await _linkRepository.FindAsync(x => x.SubProjectId == subProjectId, cancellationToken);
        var result = new List<SubProjectFinancialYearDto>();

        foreach (var link in links)
        {
            var year = await _financialYearRepository.GetByIdAsync(link.FinancialYearId, cancellationToken);
            if (year == null)
            {
                continue;
            }

            result.Add(new SubProjectFinancialYearDto
            {
                Id = link.SubProjectFinancialYearId,
                FinancialYearId = year.FinancialYearId,
                FinancialYearName = year.Name,
                StartDate = year.StartDate,
                EndDate = year.EndDate,
                IsClosed = year.IsClosed,
            });
        }

        return result;
    }

    public async Task<SubProjectFinancialYearDto> LinkAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken);
        if (subProject == null)
        {
            throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");
        }

        var year = await _financialYearRepository.GetByIdAsync(financialYearId, cancellationToken);
        if (year == null)
        {
            throw new NotFoundException($"السنة المالية رقم {financialYearId} غير موجودة");
        }

        var existing = await _linkRepository.FindAsync(
            x => x.SubProjectId == subProjectId && x.FinancialYearId == financialYearId, cancellationToken);
        if (existing.Count > 0)
        {
            throw new BusinessRuleException("المشروع الفرعي مرتبط بالفعل بهذه السنة المالية");
        }

        var link = new SubProjectFinancialYear
        {
            SubProjectId = subProjectId,
            FinancialYearId = financialYearId,
        };

        await _linkRepository.AddAsync(link, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubProjectFinancialYearDto
        {
            Id = link.SubProjectFinancialYearId,
            FinancialYearId = year.FinancialYearId,
            FinancialYearName = year.Name,
            StartDate = year.StartDate,
            EndDate = year.EndDate,
            IsClosed = year.IsClosed,
        };
    }

    public async Task UnlinkAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken = default)
    {
        var link = (await _linkRepository.FindAsync(
            x => x.SubProjectId == subProjectId && x.FinancialYearId == financialYearId, cancellationToken))
            .FirstOrDefault();

        if (link == null)
        {
            throw new NotFoundException("لا يوجد ربط بين هذا المشروع الفرعي وهذه السنة المالية");
        }

        var followUps = await _followUpRepository.FindAsync(
            x => x.SubProjectFinancialYearId == link.SubProjectFinancialYearId, cancellationToken);
        if (followUps.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن فك الربط لوجود بيانات متابعة مسجلة لهذه السنة");
        }

        _linkRepository.Remove(link);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
