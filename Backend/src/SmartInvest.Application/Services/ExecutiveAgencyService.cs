using AutoMapper;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class ExecutiveAgencyService : IExecutiveAgencyService
{
    private readonly IGenericRepository<ExecutiveAgency> _agencyRepository;
    private readonly ISubProjectRepository _subProjectRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ExecutiveAgencyService(
        IGenericRepository<ExecutiveAgency> agencyRepository,
        ISubProjectRepository subProjectRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _agencyRepository = agencyRepository;
        _subProjectRepository = subProjectRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ExecutiveAgencyDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var agencies = await _agencyRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<ExecutiveAgencyDto>>(agencies);
    }

    public async Task<ExecutiveAgencyDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var agency = await GetOrThrowAsync(id, cancellationToken);
        var dto = _mapper.Map<ExecutiveAgencyDto>(agency);

        var subProjects = await _subProjectRepository.GetByExecutiveAgencyAsync(id, cancellationToken);
        dto.AssignedSubProjects = subProjects
            .Select(s => new AssignedSubProjectDto
            {
                Id = s.SubProjectId,
                Name = s.SubProjectName,
                MainProjectName = s.MainProject.MainProjectName,
            })
            .ToList();

        return dto;
    }

    public async Task<ExecutiveAgencyDto> CreateAsync(CreateExecutiveAgencyDto dto, CancellationToken cancellationToken = default)
    {
        var agency = new ExecutiveAgency
        {
            AgencyName = dto.AgencyName,
            Phone = dto.Phone,
            Email = dto.Email,
            Address = dto.Address,
            IsActive = true,
        };

        await _agencyRepository.AddAsync(agency, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ExecutiveAgencyDto>(agency);
    }

    public async Task<ExecutiveAgencyDto> UpdateAsync(int id, UpdateExecutiveAgencyDto dto, CancellationToken cancellationToken = default)
    {
        var agency = await GetOrThrowAsync(id, cancellationToken);

        agency.AgencyName = dto.AgencyName;
        agency.Phone = dto.Phone;
        agency.Email = dto.Email;
        agency.Address = dto.Address;
        agency.IsActive = dto.IsActive;

        _agencyRepository.Update(agency);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ExecutiveAgencyDto>(agency);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var agency = await GetOrThrowAsync(id, cancellationToken);

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.ExecutiveAgencyId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف الجهة لوجود مشروعات فرعية مسندة إليها");
        }

        _agencyRepository.Remove(agency);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<ExecutiveAgency> GetOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        var agency = await _agencyRepository.GetByIdAsync(id, cancellationToken);
        if (agency == null)
        {
            throw new NotFoundException($"الجهة التنفيذية رقم {id} غير موجودة");
        }

        return agency;
    }
}
