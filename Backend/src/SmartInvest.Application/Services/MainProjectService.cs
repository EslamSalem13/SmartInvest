using AutoMapper;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.DTOs.Common;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class MainProjectService : IMainProjectService
{
    private readonly IMainProjectRepository _mainProjectRepository;
    private readonly IGenericRepository<SubProgram> _subProgramRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public MainProjectService(IMainProjectRepository mainProjectRepository, IGenericRepository<SubProgram> subProgramRepository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _mainProjectRepository = mainProjectRepository;
        _subProgramRepository = subProgramRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<MainProjectListItemDto>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (mainProjects, totalCount) = await _mainProjectRepository.GetAllWithDetailsAsync(page, pageSize, cancellationToken);
        var dtos = _mapper.Map<List<MainProjectListItemDto>>(mainProjects);

        var aggregates = await _mainProjectRepository.GetSubProjectAggregatesAsync(
            mainProjects.Select(m => m.MainProjectId).ToList(), cancellationToken);

        foreach (var dto in dtos)
        {
            if (aggregates.TryGetValue(dto.Id, out var agg))
            {
                dto.SubProjectsCount = agg.Count;
                dto.TotalBankFunding = agg.BankSum;
                dto.TotalSelfFunding = agg.SelfSum;
            }
        }

        return new PagedResultDto<MainProjectListItemDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<MainProjectDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var mainProject = await _mainProjectRepository.GetWithSubProjectsAsync(id, cancellationToken);
        if (mainProject == null)
        {
            throw new NotFoundException($"المشروع الرئيسي رقم {id} غير موجود");
        }

        return _mapper.Map<MainProjectDetailDto>(mainProject);
    }

    public async Task<MainProjectDetailDto> CreateAsync(CreateMainProjectDto dto, CancellationToken cancellationToken = default)
    {
        var subProgram = await _subProgramRepository.GetByIdAsync(dto.SubProgramId, cancellationToken);
        if (subProgram == null)
        {
            throw new NotFoundException("البرنامج الفرعي المحدد غير موجود");
        }

        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();

        var mainProject = _mapper.Map<MainProject>(dto);
        mainProject.MainProjectCode = code;
        mainProject.IsApproved = code != null;

        await _mainProjectRepository.AddAsync(mainProject, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _mainProjectRepository.GetWithSubProjectsAsync(mainProject.MainProjectId, cancellationToken);
        return _mapper.Map<MainProjectDetailDto>(created);
    }

    public async Task<MainProjectDetailDto> UpdateAsync(int id, UpdateMainProjectDto dto, CancellationToken cancellationToken = default)
    {
        var mainProject = await _mainProjectRepository.GetByIdAsync(id, cancellationToken);
        if (mainProject == null)
        {
            throw new NotFoundException($"المشروع الرئيسي رقم {id} غير موجود");
        }

        var subProgram = await _subProgramRepository.GetByIdAsync(dto.SubProgramId, cancellationToken);
        if (subProgram == null)
        {
            throw new NotFoundException("البرنامج الفرعي المحدد غير موجود");
        }

        var code = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();

        mainProject.MainProjectCode = code;
        mainProject.IsApproved = code != null;
        mainProject.MainProjectName = dto.Name;
        mainProject.ExecutingAgency = dto.ExecutingAgency;
        mainProject.SubProgramId = dto.SubProgramId;

        _mainProjectRepository.Update(mainProject);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _mainProjectRepository.GetWithSubProjectsAsync(id, cancellationToken);
        return _mapper.Map<MainProjectDetailDto>(updated);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var mainProject = await _mainProjectRepository.GetWithSubProjectsAsync(id, cancellationToken);
        if (mainProject == null)
        {
            throw new NotFoundException($"المشروع الرئيسي رقم {id} غير موجود");
        }

        if (mainProject.SubProjects != null && mainProject.SubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف المشروع الرئيسي لأنه يحتوي على مشاريع فرعية. احذف المشاريع الفرعية أولًا");
        }

        _mainProjectRepository.Remove(mainProject);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}