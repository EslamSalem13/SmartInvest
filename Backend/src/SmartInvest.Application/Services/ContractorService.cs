using AutoMapper;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class ContractorService : IContractorService
{
    private readonly IGenericRepository<Contractor> _contractorRepository;
    private readonly IGenericRepository<ProjectAssignment> _assignmentRepository;
    private readonly IProjectAssignmentRepository _projectAssignmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ContractorService(
        IGenericRepository<Contractor> contractorRepository,
        IGenericRepository<ProjectAssignment> assignmentRepository,
        IProjectAssignmentRepository projectAssignmentRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _contractorRepository = contractorRepository;
        _assignmentRepository = assignmentRepository;
        _projectAssignmentRepository = projectAssignmentRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ContractorDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var contractors = await _contractorRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<ContractorDto>>(contractors);
    }

    public async Task<ContractorDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var contractor = await GetOrThrowAsync(id, cancellationToken);
        var dto = _mapper.Map<ContractorDto>(contractor);

        var assignments = await _projectAssignmentRepository.GetByContractorAsync(id, cancellationToken);
        dto.AssignedSubProjects = assignments
            .Select(a => new AssignedSubProjectDto
            {
                Id = a.SubProject.SubProjectId,
                Name = a.SubProject.SubProjectName,
                MainProjectName = a.SubProject.MainProject.MainProjectName,
            })
            .ToList();

        return dto;
    }

    public async Task<ContractorDto> CreateAsync(CreateContractorDto dto, CancellationToken cancellationToken = default)
    {
        var contractor = new Contractor
        {
            ContractorName = dto.ContractorName,
            CompanyType = dto.CompanyType,
            NationalIdOrCommercialRegister = dto.NationalIdOrCommercialRegister,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            Address = dto.Address,
            Category = dto.Category,
            IsActive = true,
        };

        await _contractorRepository.AddAsync(contractor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ContractorDto>(contractor);
    }

    public async Task<ContractorDto> UpdateAsync(int id, UpdateContractorDto dto, CancellationToken cancellationToken = default)
    {
        var contractor = await GetOrThrowAsync(id, cancellationToken);

        contractor.ContractorName = dto.ContractorName;
        contractor.CompanyType = dto.CompanyType;
        contractor.NationalIdOrCommercialRegister = dto.NationalIdOrCommercialRegister;
        contractor.PhoneNumber = dto.PhoneNumber;
        contractor.Email = dto.Email;
        contractor.Address = dto.Address;
        contractor.Category = dto.Category;
        contractor.IsActive = dto.IsActive;

        _contractorRepository.Update(contractor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ContractorDto>(contractor);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var contractor = await GetOrThrowAsync(id, cancellationToken);

        var linkedAssignments = await _assignmentRepository.FindAsync(x => x.ContractorId == id, cancellationToken);
        if (linkedAssignments.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف المقاول لوجود تعيينات مرتبطة به");
        }

        _contractorRepository.Remove(contractor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Contractor> GetOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        var contractor = await _contractorRepository.GetByIdAsync(id, cancellationToken);
        if (contractor == null)
        {
            throw new NotFoundException($"المقاول رقم {id} غير موجود");
        }

        return contractor;
    }
}
