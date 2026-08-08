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
    private readonly IGenericRepository<ExecutionStage> _executionStageRepository;
    private readonly IGenericRepository<ContractorNote> _noteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ContractorService(
        IGenericRepository<Contractor> contractorRepository,
        IGenericRepository<ProjectAssignment> assignmentRepository,
        IProjectAssignmentRepository projectAssignmentRepository,
        IGenericRepository<ExecutionStage> executionStageRepository,
        IGenericRepository<ContractorNote> noteRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _contractorRepository = contractorRepository;
        _assignmentRepository = assignmentRepository;
        _projectAssignmentRepository = projectAssignmentRepository;
        _executionStageRepository = executionStageRepository;
        _noteRepository = noteRepository;
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

        var subProjectIds = assignments.Select(a => a.SubProjectId).ToHashSet();
        var stagesWithPenalty = subProjectIds.Count == 0
            ? []
            : await _executionStageRepository.FindAsync(
                s => subProjectIds.Contains(s.SubProjectId) && s.PenaltyAmount != null, cancellationToken);

        dto.TotalFines = stagesWithPenalty.Sum(s => s.PenaltyAmount ?? 0);
        dto.UnpaidFines = stagesWithPenalty.Where(s => !s.PenaltyPaid).Sum(s => s.PenaltyAmount ?? 0);

        var notes = await _noteRepository.FindAsync(n => n.ContractorId == id, cancellationToken);
        dto.Notes = notes
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new ContractorNoteDto
            {
                Id = n.ContractorNoteId,
                SubProjectId = n.SubProjectId,
                SubProjectName = n.SubProject?.SubProjectName,
                Text = n.Text,
                IsAiGenerated = n.IsAiGenerated,
                CreatedAt = n.CreatedAt,
            })
            .ToList();

        return dto;
    }

    public async Task<ContractorDto> SetWillWorkAgainAsync(int id, SetWillWorkAgainDto dto, CancellationToken cancellationToken = default)
    {
        var contractor = await GetOrThrowAsync(id, cancellationToken);
        contractor.WillWorkAgain = dto.WillWorkAgain;

        _contractorRepository.Update(contractor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ContractorNoteDto> AddNoteAsync(int id, CreateContractorNoteDto dto, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(dto.Text))
        {
            throw new BusinessRuleException("نص الملاحظة مطلوب");
        }

        var note = new ContractorNote
        {
            ContractorId = id,
            SubProjectId = dto.SubProjectId,
            Text = dto.Text.Trim(),
            IsAiGenerated = false,
        };

        await _noteRepository.AddAsync(note, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ContractorNoteDto
        {
            Id = note.ContractorNoteId,
            SubProjectId = note.SubProjectId,
            Text = note.Text,
            IsAiGenerated = false,
            CreatedAt = note.CreatedAt,
        };
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
