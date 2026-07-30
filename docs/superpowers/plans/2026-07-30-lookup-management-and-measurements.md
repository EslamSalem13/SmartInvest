# Lookup Management + Custom Measurements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give planning staff CRUD control over every reference list this app depends on (Main Program, Sub Program, Markaz, Governorate, Village, Priority, Status, Contract Type, Component Type, Project Level, Accounting Unit) and let them define custom per-sub-program measurements (height, distance, counts, etc.) recorded per sub-project.

**Architecture:** Backend extends the existing `LookupService`/`ILookupService`/`LookupsController` (already the single home for all read-only lookups) with Create/Update/Delete for the 7 lookups that are already real tables, adds 3 brand-new tables (`ComponentType`, `ProjectLevel`, `AccountingUnit`) to replace `SubProject`'s free-text columns of the same names, and adds a small independent Measurements subsystem (`Measurement`, `MeasurementSubProgram` join, `SubProjectMeasurementValue`). Frontend gets one generic reusable table+modal component reused across all 11 lookup types on a single `/app/settings` page, plus a dedicated `/app/measurements` page (needs a sub-program multi-picker the generic component doesn't support), plus a new Step 4 on the existing sub-project wizard for recording measurement values.

**Tech Stack:** .NET 10 (EF Core, AutoMapper), Angular 21 standalone components + Signals.

## Global Constraints

- No automated test suite exists anywhere in this repo. Each task's "test" step is build/type-check, then a manual check via the browser preview tool.
- Follow existing conventions exactly: Arabic UI strings, `si-btn`/`si-modal`/`si-overlay`/`si-grid`/`si-fld`/`si-err` shared classes from `Frontend/src/styles.css`, per-component CSS (default `ViewEncapsulation.Emulated`), Signals-based state, `[ngModel]`/`(ngModelChange)` (no Reactive Forms), `AuthService.isManager` for manager-gated mutation actions (view is all-staff, create/edit/delete is manager-only — matches every existing CRUD page in this app).
- Backend: class-level `[Authorize]` broad/none, method-level narrower/explicit — never rely on class+method role intersection accidentally locking everyone out (documented pitfall in `docs/PROJECT.md`). All new mutation actions in this plan use `[Authorize(Roles = Roles.PlanningManager)]` — matches the Contractors/Agencies convention (all mutations manager-only), not `ContractTypesController`'s looser existing pattern (which is untouched by this plan).
- Migrations: follow `docs/PROJECT.md` §9's procedure exactly — generate, inspect the raw `Up()`/`Down()` SQL for anything requiring manual ordering, apply, then run the empty-probe-migration technique to verify the snapshot matches, delete the probe files.
- Table naming in this codebase is singular (e.g. `ContractType`, not `ContractTypes`) — new tables in this plan (`ComponentType`, `ProjectLevel`, `AccountingUnit`, `Measurement`, `MeasurementSubProgram`, `SubProjectMeasurementValue`) follow this convention.
- Never run dev servers via Bash — use the `preview_start` tool.
- **Known recurring issue:** a stray `SmartInvest.API.exe` process can hold the build output DLL locked. If `dotnet build` fails with a file-lock error, find and stop it first (Windows: `taskkill //F //IM SmartInvest.API.exe` via bash, or PowerShell `Get-Process -Name SmartInvest.API | Stop-Process -Force`), then rebuild.

---

### Task 1: Backend — Write API for the 7 Existing Hierarchy Lookups

**Files:**
- Modify: `Backend/src/SmartInvest.Application/DTOs/LookupDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/ILookupService.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/LookupService.cs`
- Modify: `Backend/src/SmartInvest.API/Controllers/LookupsController.cs`

**Interfaces:**
- Produces: `CreateNamedLookupDto`/`UpdateNamedLookupDto` (shared by MainProgram/Governorate/Priority/Status), `CreateSubProgramDto`/`UpdateSubProgramDto`, `CreateMarkazDto`/`UpdateMarkazDto`, `CreateVillageDto`/`UpdateVillageDto`. `ILookupService` gains 21 new methods (Create/Update/Delete × 7 entities). No schema changes — all 7 tables already exist.

- [ ] **Step 1: Add the new DTOs**

Append to `Backend/src/SmartInvest.Application/DTOs/LookupDtos.cs` (keep the existing 4 classes untouched, add these after):

```csharp
public class CreateNamedLookupDto
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateNamedLookupDto
{
    public string Name { get; set; } = string.Empty;
}

public class CreateSubProgramDto
{
    public string Name { get; set; } = string.Empty;
    public int MainProgramId { get; set; }
}

public class UpdateSubProgramDto
{
    public string Name { get; set; } = string.Empty;
    public int MainProgramId { get; set; }
}

public class CreateMarkazDto
{
    public string Name { get; set; } = string.Empty;
    public int GovernorateId { get; set; }
}

public class UpdateMarkazDto
{
    public string Name { get; set; } = string.Empty;
    public int GovernorateId { get; set; }
}

public class CreateVillageDto
{
    public string Name { get; set; } = string.Empty;
    public int MarkazId { get; set; }
}

public class UpdateVillageDto
{
    public string Name { get; set; } = string.Empty;
    public int MarkazId { get; set; }
}
```

- [ ] **Step 2: Extend `ILookupService`**

Add these method signatures to `Backend/src/SmartInvest.Application/Interfaces/ILookupService.cs`, inside the existing interface body (after the 7 existing `GetXAsync` methods):

```csharp
    Task<LookupDto> CreatePriorityAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdatePriorityAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeletePriorityAsync(int id, CancellationToken cancellationToken = default);

    Task<LookupDto> CreateStatusAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateStatusAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteStatusAsync(int id, CancellationToken cancellationToken = default);

    Task<LookupDto> CreateMainProgramAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateMainProgramAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteMainProgramAsync(int id, CancellationToken cancellationToken = default);

    Task<SubProgramLookupDto> CreateSubProgramAsync(CreateSubProgramDto dto, CancellationToken cancellationToken = default);
    Task<SubProgramLookupDto> UpdateSubProgramAsync(int id, UpdateSubProgramDto dto, CancellationToken cancellationToken = default);
    Task DeleteSubProgramAsync(int id, CancellationToken cancellationToken = default);

    Task<LookupDto> CreateGovernorateAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateGovernorateAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteGovernorateAsync(int id, CancellationToken cancellationToken = default);

    Task<MarkazLookupDto> CreateMarkazAsync(CreateMarkazDto dto, CancellationToken cancellationToken = default);
    Task<MarkazLookupDto> UpdateMarkazAsync(int id, UpdateMarkazDto dto, CancellationToken cancellationToken = default);
    Task DeleteMarkazAsync(int id, CancellationToken cancellationToken = default);

    Task<VillageLookupDto> CreateVillageAsync(CreateVillageDto dto, CancellationToken cancellationToken = default);
    Task<VillageLookupDto> UpdateVillageAsync(int id, UpdateVillageDto dto, CancellationToken cancellationToken = default);
    Task DeleteVillageAsync(int id, CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Implement in `LookupService`**

Replace `Backend/src/SmartInvest.Application/Services/LookupService.cs` in full:

```csharp
using AutoMapper;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class LookupService : ILookupService
{
    private readonly IGenericRepository<ProjectPriority> _priorityRepository;
    private readonly IGenericRepository<ProjectStatus> _statusRepository;
    private readonly IGenericRepository<MainProgram> _mainProgramRepository;
    private readonly IGenericRepository<SubProgram> _subProgramRepository;
    private readonly IGenericRepository<Governorate> _governorateRepository;
    private readonly IGenericRepository<Markaz> _markazRepository;
    private readonly IGenericRepository<Village> _villageRepository;
    private readonly IGenericRepository<MainProject> _mainProjectRepository;
    private readonly IGenericRepository<SubProject> _subProjectRepository;
    private readonly IGenericRepository<ProjectFollowUp> _followUpRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public LookupService(
        IGenericRepository<ProjectPriority> priorityRepository,
        IGenericRepository<ProjectStatus> statusRepository,
        IGenericRepository<MainProgram> mainProgramRepository,
        IGenericRepository<SubProgram> subProgramRepository,
        IGenericRepository<Governorate> governorateRepository,
        IGenericRepository<Markaz> markazRepository,
        IGenericRepository<Village> villageRepository,
        IGenericRepository<MainProject> mainProjectRepository,
        IGenericRepository<SubProject> subProjectRepository,
        IGenericRepository<ProjectFollowUp> followUpRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _priorityRepository = priorityRepository;
        _statusRepository = statusRepository;
        _mainProgramRepository = mainProgramRepository;
        _subProgramRepository = subProgramRepository;
        _governorateRepository = governorateRepository;
        _markazRepository = markazRepository;
        _villageRepository = villageRepository;
        _mainProjectRepository = mainProjectRepository;
        _subProjectRepository = subProjectRepository;
        _followUpRepository = followUpRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<LookupDto>> GetPrioritiesAsync(CancellationToken cancellationToken = default)
    {
        var priorities = await _priorityRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(priorities);
    }

    public async Task<IReadOnlyList<LookupDto>> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await _statusRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(statuses);
    }

    public async Task<IReadOnlyList<LookupDto>> GetMainProgramsAsync(CancellationToken cancellationToken = default)
    {
        var mainPrograms = await _mainProgramRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(mainPrograms);
    }

    public async Task<IReadOnlyList<SubProgramLookupDto>> GetSubProgramsAsync(int? mainProgramId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SubProgram> subPrograms;

        if (mainProgramId.HasValue)
        {
            subPrograms = await _subProgramRepository.FindAsync(x => x.ProgramId == mainProgramId.Value, cancellationToken);
        }
        else
        {
            subPrograms = await _subProgramRepository.GetAllAsync(cancellationToken);
        }

        return _mapper.Map<List<SubProgramLookupDto>>(subPrograms);
    }

    public async Task<IReadOnlyList<LookupDto>> GetGovernoratesAsync(CancellationToken cancellationToken = default)
    {
        var governorates = await _governorateRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(governorates);
    }

    public async Task<IReadOnlyList<MarkazLookupDto>> GetMarkazAsync(int? governorateId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Markaz> markazList;

        if (governorateId.HasValue)
        {
            markazList = await _markazRepository.FindAsync(x => x.GovernorateId == governorateId.Value, cancellationToken);
        }
        else
        {
            markazList = await _markazRepository.GetAllAsync(cancellationToken);
        }

        return _mapper.Map<List<MarkazLookupDto>>(markazList);
    }

    public async Task<IReadOnlyList<VillageLookupDto>> GetVillagesAsync(int? markazId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Village> villages;

        if (markazId.HasValue)
        {
            villages = await _villageRepository.FindAsync(x => x.MarkazId == markazId.Value, cancellationToken);
        }
        else
        {
            villages = await _villageRepository.GetAllAsync(cancellationToken);
        }

        return _mapper.Map<List<VillageLookupDto>>(villages);
    }

    public async Task<LookupDto> CreatePriorityAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new ProjectPriority { Priority = dto.Name.Trim() };
        await _priorityRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdatePriorityAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _priorityRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الأولوية رقم {id} غير موجودة");
        entity.Priority = dto.Name.Trim();
        _priorityRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeletePriorityAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _priorityRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الأولوية رقم {id} غير موجودة");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.PriorityId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف الأولوية لوجود مشروعات فرعية تستخدمها");
        }

        _priorityRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<LookupDto> CreateStatusAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new ProjectStatus { StatusName = dto.Name.Trim() };
        await _statusRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateStatusAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _statusRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الحالة رقم {id} غير موجودة");
        entity.StatusName = dto.Name.Trim();
        _statusRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteStatusAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _statusRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الحالة رقم {id} غير موجودة");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.StatusId == id, cancellationToken);
        var linkedFollowUps = await _followUpRepository.FindAsync(x => x.StatusId == id, cancellationToken);
        if (linkedSubProjects.Count > 0 || linkedFollowUps.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف الحالة لوجود مشروعات فرعية أو متابعات تستخدمها");
        }

        _statusRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<LookupDto> CreateMainProgramAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new MainProgram { ProgramName = dto.Name.Trim() };
        await _mainProgramRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateMainProgramAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _mainProgramRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"البرنامج الرئيسي رقم {id} غير موجود");
        entity.ProgramName = dto.Name.Trim();
        _mainProgramRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteMainProgramAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _mainProgramRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"البرنامج الرئيسي رقم {id} غير موجود");

        var linkedSubPrograms = await _subProgramRepository.FindAsync(x => x.ProgramId == id, cancellationToken);
        if (linkedSubPrograms.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف البرنامج الرئيسي لوجود برامج فرعية تابعة له");
        }

        _mainProgramRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SubProgramLookupDto> CreateSubProgramAsync(CreateSubProgramDto dto, CancellationToken cancellationToken = default)
    {
        var mainProgram = await _mainProgramRepository.GetByIdAsync(dto.MainProgramId, cancellationToken)
            ?? throw new NotFoundException("البرنامج الرئيسي المحدد غير موجود");

        var entity = new SubProgram { SubProgramName = dto.Name.Trim(), ProgramId = mainProgram.ProgramId };
        await _subProgramRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<SubProgramLookupDto>(entity);
    }

    public async Task<SubProgramLookupDto> UpdateSubProgramAsync(int id, UpdateSubProgramDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _subProgramRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"البرنامج الفرعي رقم {id} غير موجود");

        var mainProgram = await _mainProgramRepository.GetByIdAsync(dto.MainProgramId, cancellationToken)
            ?? throw new NotFoundException("البرنامج الرئيسي المحدد غير موجود");

        entity.SubProgramName = dto.Name.Trim();
        entity.ProgramId = mainProgram.ProgramId;
        _subProgramRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<SubProgramLookupDto>(entity);
    }

    public async Task DeleteSubProgramAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _subProgramRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"البرنامج الفرعي رقم {id} غير موجود");

        var linkedMainProjects = await _mainProjectRepository.FindAsync(x => x.SubProgramId == id, cancellationToken);
        if (linkedMainProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف البرنامج الفرعي لوجود مشروعات رئيسية تابعة له");
        }

        _subProgramRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<LookupDto> CreateGovernorateAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new Governorate { GovernorateName = dto.Name.Trim() };
        await _governorateRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateGovernorateAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _governorateRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المحافظة رقم {id} غير موجودة");
        entity.GovernorateName = dto.Name.Trim();
        _governorateRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteGovernorateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _governorateRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المحافظة رقم {id} غير موجودة");

        var linkedMarkaz = await _markazRepository.FindAsync(x => x.GovernorateId == id, cancellationToken);
        if (linkedMarkaz.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف المحافظة لوجود مراكز تابعة لها");
        }

        _governorateRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<MarkazLookupDto> CreateMarkazAsync(CreateMarkazDto dto, CancellationToken cancellationToken = default)
    {
        var governorate = await _governorateRepository.GetByIdAsync(dto.GovernorateId, cancellationToken)
            ?? throw new NotFoundException("المحافظة المحددة غير موجودة");

        var entity = new Markaz { MarkazName = dto.Name.Trim(), GovernorateId = governorate.GovernorateId };
        await _markazRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<MarkazLookupDto>(entity);
    }

    public async Task<MarkazLookupDto> UpdateMarkazAsync(int id, UpdateMarkazDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _markazRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المركز رقم {id} غير موجود");

        var governorate = await _governorateRepository.GetByIdAsync(dto.GovernorateId, cancellationToken)
            ?? throw new NotFoundException("المحافظة المحددة غير موجودة");

        entity.MarkazName = dto.Name.Trim();
        entity.GovernorateId = governorate.GovernorateId;
        _markazRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<MarkazLookupDto>(entity);
    }

    public async Task DeleteMarkazAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _markazRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المركز رقم {id} غير موجود");

        var linkedVillages = await _villageRepository.FindAsync(x => x.MarkazId == id, cancellationToken);
        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.MarkazId == id, cancellationToken);
        if (linkedVillages.Count > 0 || linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف المركز لوجود قرى أو مشروعات فرعية تابعة له");
        }

        _markazRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<VillageLookupDto> CreateVillageAsync(CreateVillageDto dto, CancellationToken cancellationToken = default)
    {
        var markaz = await _markazRepository.GetByIdAsync(dto.MarkazId, cancellationToken)
            ?? throw new NotFoundException("المركز المحدد غير موجود");

        var entity = new Village { VillageName = dto.Name.Trim(), MarkazId = markaz.MarkazId };
        await _villageRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<VillageLookupDto>(entity);
    }

    public async Task<VillageLookupDto> UpdateVillageAsync(int id, UpdateVillageDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _villageRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"القرية رقم {id} غير موجودة");

        var markaz = await _markazRepository.GetByIdAsync(dto.MarkazId, cancellationToken)
            ?? throw new NotFoundException("المركز المحدد غير موجود");

        entity.VillageName = dto.Name.Trim();
        entity.MarkazId = markaz.MarkazId;
        _villageRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<VillageLookupDto>(entity);
    }

    public async Task DeleteVillageAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _villageRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"القرية رقم {id} غير موجودة");

        _villageRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Add write actions to `LookupsController`**

Replace `Backend/src/SmartInvest.API/Controllers/LookupsController.cs` in full:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/lookups")]
[Authorize]
public class LookupsController : ControllerBase
{
    private readonly ILookupService _lookupService;

    public LookupsController(ILookupService lookupService)
    {
        _lookupService = lookupService;
    }

    [HttpGet("priorities")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetPriorities(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetPrioritiesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("priorities")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreatePriority(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreatePriorityAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("priorities/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdatePriority(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdatePriorityAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("priorities/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeletePriority(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeletePriorityAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("statuses")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetStatuses(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetStatusesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("statuses")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreateStatus(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateStatusAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("statuses/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdateStatus(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateStatusAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("statuses/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteStatus(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteStatusAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("main-programs")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetMainPrograms(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetMainProgramsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("main-programs")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreateMainProgram(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateMainProgramAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("main-programs/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdateMainProgram(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateMainProgramAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("main-programs/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteMainProgram(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteMainProgramAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("sub-programs")]
    public async Task<ActionResult<IReadOnlyList<SubProgramLookupDto>>> GetSubPrograms([FromQuery] int? mainProgramId, CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetSubProgramsAsync(mainProgramId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("sub-programs")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<SubProgramLookupDto>> CreateSubProgram(CreateSubProgramDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateSubProgramAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("sub-programs/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<SubProgramLookupDto>> UpdateSubProgram(int id, UpdateSubProgramDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateSubProgramAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("sub-programs/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteSubProgram(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteSubProgramAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("governorates")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetGovernorates(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetGovernoratesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("governorates")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreateGovernorate(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateGovernorateAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("governorates/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdateGovernorate(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateGovernorateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("governorates/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteGovernorate(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteGovernorateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("markaz")]
    public async Task<ActionResult<IReadOnlyList<MarkazLookupDto>>> GetMarkaz([FromQuery] int? governorateId, CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetMarkazAsync(governorateId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("markaz")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<MarkazLookupDto>> CreateMarkaz(CreateMarkazDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateMarkazAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("markaz/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<MarkazLookupDto>> UpdateMarkaz(int id, UpdateMarkazDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateMarkazAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("markaz/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteMarkaz(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteMarkazAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("villages")]
    public async Task<ActionResult<IReadOnlyList<VillageLookupDto>>> GetVillages([FromQuery] int? markazId, CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetVillagesAsync(markazId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("villages")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<VillageLookupDto>> CreateVillage(CreateVillageDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateVillageAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("villages/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<VillageLookupDto>> UpdateVillage(int id, UpdateVillageDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateVillageAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("villages/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteVillage(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteVillageAsync(id, cancellationToken);
        return NoContent();
    }
}
```

- [ ] **Step 5: Update the DI registration if needed**

`Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs` already has `services.AddScoped<ILookupService, LookupService>();` — no change needed there. `IGenericRepository<>` is already registered as an open generic (`services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));`), so the three newly-injected repository types (`IGenericRepository<MainProject>`, `IGenericRepository<SubProject>`, `IGenericRepository<ProjectFollowUp>`) resolve automatically — confirm this by building successfully in Step 6.

- [ ] **Step 6: Build the backend**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 7: Manual check via Swagger**

Start `backend-api`, log in as `admin`/`Admin@123`. Confirm: `POST /api/lookups/main-programs` with `{ "name": "برنامج تجريبي" }` succeeds and the new program appears in `GET /api/lookups/main-programs`; `PUT`/`DELETE` on it work; attempting to `DELETE` a Priority/Status/Markaz/MainProgram/SubProgram/Governorate that has real dependent data returns a 400 with the Arabic business-rule message; `POST /api/lookups/sub-programs` with a bad `mainProgramId` returns 404.

- [ ] **Step 8: Commit**

```bash
git add Backend/src/SmartInvest.Application/DTOs/LookupDtos.cs Backend/src/SmartInvest.Application/Interfaces/ILookupService.cs Backend/src/SmartInvest.Application/Services/LookupService.cs Backend/src/SmartInvest.API/Controllers/LookupsController.cs
git commit -m "feat: add write API for main program, sub program, governorate, markaz, village, priority, status

These 7 lookups already existed as real tables with read-only access;
planning staff can now create/edit/delete them directly instead of
requiring a database change."
```

---

### Task 2: Backend — New ComponentType/ProjectLevel/AccountingUnit Tables (Standalone)

**Files:**
- Create: `Backend/src/SmartInvest.Domain/Entities/ComponentType.cs`
- Create: `Backend/src/SmartInvest.Domain/Entities/ProjectLevel.cs`
- Create: `Backend/src/SmartInvest.Domain/Entities/AccountingUnit.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/LookupDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Common/Mappings/LookupMappingProfile.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/ILookupService.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/LookupService.cs`
- Modify: `Backend/src/SmartInvest.API/Controllers/LookupsController.cs`
- Create (generated): a new EF migration creating the 3 tables and seeding them from `SubProject`'s existing free-text values.

**Interfaces:**
- Consumes: nothing beyond Task 1 compiling cleanly (this task follows the exact same pattern Task 1 established in `LookupService`/`LookupsController`).
- Produces: `ComponentType { Id, Name }`, `ProjectLevel { Id, Name }`, `AccountingUnit { Id, Name }` entities and their full read/write API at `api/lookups/component-types`, `api/lookups/project-levels`, `api/lookups/accounting-units`. `SubProject` is **not** touched yet — its existing free-text `ComponentType`/`ProjectLevel`/`AccountingUnit` string columns keep working unchanged until Task 3. These 3 new tables are seeded with every distinct non-empty value already present in `SubProject`'s string columns, plus one guaranteed fallback row named `"غير محدد"` in each table (needed because essentially all existing rows have an empty `AccountingUnit` string today, per the codebase's own sub-project form always sending `accountingUnit: ''`).

- [ ] **Step 1: Create the 3 new entities**

`Backend/src/SmartInvest.Domain/Entities/ComponentType.cs`:
```csharp
namespace SmartInvest.Domain.Entities
{
    public class ComponentType
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
```

`Backend/src/SmartInvest.Domain/Entities/ProjectLevel.cs`:
```csharp
namespace SmartInvest.Domain.Entities
{
    public class ProjectLevel
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
```

`Backend/src/SmartInvest.Domain/Entities/AccountingUnit.cs`:
```csharp
namespace SmartInvest.Domain.Entities
{
    public class AccountingUnit
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 2: Add DTOs**

Append to `Backend/src/SmartInvest.Application/DTOs/LookupDtos.cs` (the `CreateNamedLookupDto`/`UpdateNamedLookupDto` from Task 1 are reused as-is for all 3 — no new Create/Update DTOs needed, only confirm they're still there from Task 1).

- [ ] **Step 3: Add AutoMapper mappings**

In `Backend/src/SmartInvest.Application/Common/Mappings/LookupMappingProfile.cs`, add inside the constructor (after the existing 7 `CreateMap` calls):

```csharp
        CreateMap<ComponentType, LookupDto>();

        CreateMap<ProjectLevel, LookupDto>();

        CreateMap<AccountingUnit, LookupDto>();
```

(No `.ForMember()` needed — unlike the pre-existing entities, these 3 new ones already have properties literally named `Id`/`Name`, so AutoMapper's default convention-based mapping applies directly.)

- [ ] **Step 4: Extend `ILookupService`**

Add to `Backend/src/SmartInvest.Application/Interfaces/ILookupService.cs`, after the Village methods added in Task 1:

```csharp
    Task<IReadOnlyList<LookupDto>> GetComponentTypesAsync(CancellationToken cancellationToken = default);
    Task<LookupDto> CreateComponentTypeAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateComponentTypeAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteComponentTypeAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetProjectLevelsAsync(CancellationToken cancellationToken = default);
    Task<LookupDto> CreateProjectLevelAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateProjectLevelAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteProjectLevelAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetAccountingUnitsAsync(CancellationToken cancellationToken = default);
    Task<LookupDto> CreateAccountingUnitAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateAccountingUnitAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteAccountingUnitAsync(int id, CancellationToken cancellationToken = default);
```

- [ ] **Step 5: Implement in `LookupService`**

In `Backend/src/SmartInvest.Application/Services/LookupService.cs`:

Add 3 new fields and constructor parameters (alongside the existing ones from Task 1):
```csharp
    private readonly IGenericRepository<ComponentType> _componentTypeRepository;
    private readonly IGenericRepository<ProjectLevel> _projectLevelRepository;
    private readonly IGenericRepository<AccountingUnit> _accountingUnitRepository;
```
Add matching constructor parameters and assignments (same pattern as every other repository in that constructor), e.g. add `IGenericRepository<ComponentType> componentTypeRepository` as a parameter and `_componentTypeRepository = componentTypeRepository;` in the body — do this for all 3.

Add these methods to the class body (after the Village methods from Task 1):

```csharp
    public async Task<IReadOnlyList<LookupDto>> GetComponentTypesAsync(CancellationToken cancellationToken = default)
    {
        var items = await _componentTypeRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(items);
    }

    public async Task<LookupDto> CreateComponentTypeAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new ComponentType { Name = dto.Name.Trim() };
        await _componentTypeRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateComponentTypeAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _componentTypeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المكوّن العيني رقم {id} غير موجود");
        entity.Name = dto.Name.Trim();
        _componentTypeRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteComponentTypeAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _componentTypeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المكوّن العيني رقم {id} غير موجود");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.ComponentTypeId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف المكوّن العيني لوجود مشروعات فرعية تستخدمه");
        }

        _componentTypeRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupDto>> GetProjectLevelsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _projectLevelRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(items);
    }

    public async Task<LookupDto> CreateProjectLevelAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new ProjectLevel { Name = dto.Name.Trim() };
        await _projectLevelRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateProjectLevelAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _projectLevelRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"مستوى المشروع رقم {id} غير موجود");
        entity.Name = dto.Name.Trim();
        _projectLevelRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteProjectLevelAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _projectLevelRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"مستوى المشروع رقم {id} غير موجود");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.ProjectLevelId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف مستوى المشروع لوجود مشروعات فرعية تستخدمه");
        }

        _projectLevelRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LookupDto>> GetAccountingUnitsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _accountingUnitRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(items);
    }

    public async Task<LookupDto> CreateAccountingUnitAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new AccountingUnit { Name = dto.Name.Trim() };
        await _accountingUnitRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateAccountingUnitAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _accountingUnitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الوحدة الحسابية رقم {id} غير موجودة");
        entity.Name = dto.Name.Trim();
        _accountingUnitRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteAccountingUnitAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _accountingUnitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الوحدة الحسابية رقم {id} غير موجودة");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.AccountingUnitId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف الوحدة الحسابية لوجود مشروعات فرعية تستخدمها");
        }

        _accountingUnitRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
```

**Note:** the delete guards above reference `x.ComponentTypeId`, `x.ProjectLevelId`, `x.AccountingUnitId` on `SubProject` — these properties don't exist yet (Task 3 adds them). This means `LookupService.cs` will NOT compile standalone at the end of this task with those 3 delete-guard bodies as written. Since this task's spec requires it to build independently, temporarily use a not-yet-linked check instead: replace each of the 3 delete guards' body with a simple existence check that doesn't depend on `SubProject` at all yet — since nothing references these 3 new tables until Task 3 lands, there is nothing to guard against right now. Use this simpler version for all 3 delete methods in this task instead of the `x.ComponentTypeId` version shown above:

```csharp
    public async Task DeleteComponentTypeAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _componentTypeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المكوّن العيني رقم {id} غير موجود");

        _componentTypeRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
```
(and the same simplified shape — just load, 404-check, remove, save — for `DeleteProjectLevelAsync` and `DeleteAccountingUnitAsync` in this task). Task 3 will revisit all 3 delete methods and add the real `SubProject`-based guard shown above once the FK columns exist.

- [ ] **Step 6: Add read + write actions to `LookupsController`**

Add to `Backend/src/SmartInvest.API/Controllers/LookupsController.cs`, after the Village actions from Task 1:

```csharp
    [HttpGet("component-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetComponentTypes(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetComponentTypesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("component-types")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreateComponentType(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateComponentTypeAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("component-types/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdateComponentType(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateComponentTypeAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("component-types/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteComponentType(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteComponentTypeAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("project-levels")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetProjectLevels(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetProjectLevelsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("project-levels")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreateProjectLevel(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateProjectLevelAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("project-levels/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdateProjectLevel(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateProjectLevelAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("project-levels/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteProjectLevel(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteProjectLevelAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("accounting-units")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetAccountingUnits(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetAccountingUnitsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("accounting-units")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreateAccountingUnit(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateAccountingUnitAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("accounting-units/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdateAccountingUnit(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateAccountingUnitAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("accounting-units/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteAccountingUnit(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteAccountingUnitAsync(id, cancellationToken);
        return NoContent();
    }
```

- [ ] **Step 7: Build the backend**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.` (confirms the simplified delete-guard bodies from Step 5's note compile without needing `SubProject.ComponentTypeId`/etc, which don't exist until Task 3).

- [ ] **Step 8: Generate and apply the migration**

```bash
cd Backend/src/SmartInvest.API
dotnet ef migrations add AddComponentTypeProjectLevelAccountingUnitTables --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

Inspect the generated migration. Expect three `CreateTable` calls (`ComponentType`, `ProjectLevel`, `AccountingUnit`, each with `Id` identity PK + `Name nvarchar(max) not null`). **Before applying**, manually add seed-data SQL to the end of the generated `Up()` method (after the three `CreateTable` calls), and matching cleanup to `Down()` (before the `DropTable` calls) — EF's scaffolder won't generate this data-migration SQL itself, you add it by hand in the generated file:

In `Up()`, after the `CreateTable` calls:
```csharp
            migrationBuilder.Sql(@"
                INSERT INTO ComponentType (Name)
                SELECT DISTINCT ComponentType FROM SubProjects
                WHERE ComponentType IS NOT NULL AND LTRIM(RTRIM(ComponentType)) <> ''
            ");
            migrationBuilder.Sql("INSERT INTO ComponentType (Name) VALUES (N'غير محدد')");

            migrationBuilder.Sql(@"
                INSERT INTO ProjectLevel (Name)
                SELECT DISTINCT ProjectLevel FROM SubProjects
                WHERE ProjectLevel IS NOT NULL AND LTRIM(RTRIM(ProjectLevel)) <> ''
            ");
            migrationBuilder.Sql("INSERT INTO ProjectLevel (Name) VALUES (N'غير محدد')");

            migrationBuilder.Sql(@"
                INSERT INTO AccountingUnit (Name)
                SELECT DISTINCT AccountingUnit FROM SubProjects
                WHERE AccountingUnit IS NOT NULL AND LTRIM(RTRIM(AccountingUnit)) <> ''
            ");
            migrationBuilder.Sql("INSERT INTO AccountingUnit (Name) VALUES (N'غير محدد')");
```

In `Down()`, this seed data doesn't need explicit cleanup — dropping the tables (which the scaffolded `Down()` already does) removes the seeded rows along with everything else.

```bash
dotnet ef database update --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- [ ] **Step 9: Empty-probe verify**

```bash
dotnet ef migrations add ProbeCheck --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```
Confirm both `Up()`/`Down()` are empty, then:
```bash
dotnet ef migrations remove --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- [ ] **Step 10: Manual check via Swagger**

Confirm `GET /api/lookups/component-types` returns the real distinct values already in the data (e.g. `الات ومعدات`, `تجهيزات`, `تشييدات`, `مبانى غير سكنية`, plus `غير محدد`) — not an empty list. Confirm `GET /api/lookups/accounting-units` returns at least `غير محدد` (since existing data's `AccountingUnit` column is empty for every row). Confirm create/edit/delete work on all 3.

- [ ] **Step 11: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Entities/ComponentType.cs Backend/src/SmartInvest.Domain/Entities/ProjectLevel.cs Backend/src/SmartInvest.Domain/Entities/AccountingUnit.cs Backend/src/SmartInvest.Application/DTOs/LookupDtos.cs Backend/src/SmartInvest.Application/Common/Mappings/LookupMappingProfile.cs Backend/src/SmartInvest.Application/Interfaces/ILookupService.cs Backend/src/SmartInvest.Application/Services/LookupService.cs Backend/src/SmartInvest.API/Controllers/LookupsController.cs Backend/src/SmartInvest.Infrastructure/Migrations/
git commit -m "feat: add ComponentType/ProjectLevel/AccountingUnit as managed lookups

New standalone tables, seeded from every distinct value already present
in SubProject's free-text columns of the same names, plus a guaranteed
'غير محدد' fallback row in each (needed since AccountingUnit has been
empty on every existing row). SubProject itself still uses its old
string columns until the next task switches it to these new tables."
```

---

### Task 3: Backend — Convert SubProject to Use the New FK Columns

**Files:**
- Modify: `Backend/src/SmartInvest.Domain/Entities/SubProject.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/SubProjectDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Common/Mappings/SubProjectMappingProfile.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/SubProjectService.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/LookupService.cs` (revisit Task 2's 3 simplified delete guards)
- Modify: `Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs`
- Create (generated): a new EF migration converting the 3 string columns to FK columns, preserving existing data.

**Interfaces:**
- Consumes: `ComponentType`/`ProjectLevel`/`AccountingUnit` tables (Task 2), already seeded with real distinct values + a `"غير محدد"` fallback in each.
- Produces: `SubProject.ComponentTypeId`/`ProjectLevelId`/`AccountingUnitId` (all `int`, required) replace the old free-text columns of the same base names. `SubProjectListItemDto`/`SubProjectDetailDto` gain `ComponentTypeName`/`ProjectLevelName`(+`AccountingUnitName` on Detail only, matching where the old string field existed) alongside `ComponentTypeId`/`ProjectLevelId`(+`AccountingUnitId`). `CreateSubProjectDto`/`UpdateSubProjectDto`'s `ProjectLevel`/`ComponentType`/`AccountingUnit` string fields become `ProjectLevelId`/`ComponentTypeId`/`AccountingUnitId` (`int`). Task 7 (frontend) consumes this exact shape.

- [ ] **Step 1: Update the `SubProject` entity**

Replace `Backend/src/SmartInvest.Domain/Entities/SubProject.cs` in full:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class SubProject
    {
        [Key]
        public int SubProjectId { get; set; }

        public int MainProjectId { get; set; }
        public virtual MainProject MainProject { get; set; }
        [MaxLength(250)]
        public string SubProjectName { get; set; } = string.Empty;

        [ForeignKey("ProjectLevel")]
        public int ProjectLevelId { get; set; }
        public virtual ProjectLevel ProjectLevel { get; set; }

        [ForeignKey("ComponentType")]
        public int ComponentTypeId { get; set; }
        public virtual ComponentType ComponentType { get; set; }

        [ForeignKey("AccountingUnit")]
        public int AccountingUnitId { get; set; }
        public virtual AccountingUnit AccountingUnit { get; set; }

        [NotMapped]
        public decimal TotalCost => BankFunding + SelfFunding; 
        public string ProjectNature { get; set; } = string.Empty;

        // Nullables based on ERD
        public string? GreenInvestmentLink { get; set; } 
        public string? ProjectDescription { get; set; }
        public string? ProjectGoal { get; set; }
        public string? SocialImpact { get; set; }
        public string? EconomicImpact { get; set; }
        public string? EnvironmentalImpact { get; set; }

        [ForeignKey("Markaz")]
        public int MarkazId { get; set; }
        public virtual Markaz Markaz { get; set; }

        [ForeignKey("Priority")]
        public int PriorityId { get; set; }
        public virtual ProjectPriority Priority { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        [ForeignKey("Status")]
        public int StatusId { get; set; }
        
        [MaxLength(50)]
        public string? SubProjectCode { get; set; }

        // بيانات الاعتماد
        public bool IsApproved { get; set; }
        [MaxLength(1000)]
        public string? ApprovalCancellationReason { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? ApprovalCancelledAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BankFunding { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SelfFunding { get; set; } 
        public virtual ProjectStatus Status { get; set; }

        [ForeignKey("ExecutiveAgency")]
        public int? ExecutiveAgencyId { get; set; }
        public virtual ExecutiveAgency? ExecutiveAgency { get; set; }

        public virtual ICollection<PlanProject> PlanProjects { get; set; }
        public virtual ICollection<SubProjectFinancialYear> FinancialYears { get; set; }
        public virtual ICollection<ProjectAssignment>? ProjectAssignments { get; set; }
        public virtual ICollection<ProjectSpecification>? ProjectSpecifications { get; set; }
    }
}
```

- [ ] **Step 2: Update `SubProjectDtos.cs`**

In `Backend/src/SmartInvest.Application/DTOs/SubProjectDtos.cs`:

In `SubProjectListItemDto`, replace:
```csharp
    public string ProjectLevel { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
```
with:
```csharp
    public int ProjectLevelId { get; set; }
    public string ProjectLevelName { get; set; } = string.Empty;
    public int ComponentTypeId { get; set; }
    public string ComponentTypeName { get; set; } = string.Empty;
```

In `SubProjectDetailDto`, replace:
```csharp
    public string ProjectLevel { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public string AccountingUnit { get; set; } = string.Empty;
```
with:
```csharp
    public int ProjectLevelId { get; set; }
    public string ProjectLevelName { get; set; } = string.Empty;
    public int ComponentTypeId { get; set; }
    public string ComponentTypeName { get; set; } = string.Empty;
    public int AccountingUnitId { get; set; }
    public string AccountingUnitName { get; set; } = string.Empty;
```

In `CreateSubProjectDto`, replace:
```csharp
    public string ProjectLevel { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public string AccountingUnit { get; set; } = string.Empty;
```
with:
```csharp
    public int ProjectLevelId { get; set; }
    public int ComponentTypeId { get; set; }
    public int AccountingUnitId { get; set; }
```

In `UpdateSubProjectDto`, make the identical replacement (same 3 lines, same new 3 lines).

- [ ] **Step 3: Update `SubProjectMappingProfile`**

In `Backend/src/SmartInvest.Application/Common/Mappings/SubProjectMappingProfile.cs`, in the `CreateMap<SubProject, SubProjectListItemDto>()` chain, add (anywhere among the existing `.ForMember()` calls):

```csharp
            .ForMember(
                dest => dest.ProjectLevelId,
                opt => opt.MapFrom(src => src.ProjectLevelId))
            .ForMember(
                dest => dest.ProjectLevelName,
                opt => opt.MapFrom(src => src.ProjectLevel.Name))
            .ForMember(
                dest => dest.ComponentTypeId,
                opt => opt.MapFrom(src => src.ComponentTypeId))
            .ForMember(
                dest => dest.ComponentTypeName,
                opt => opt.MapFrom(src => src.ComponentType.Name))
```

In the `CreateMap<SubProject, SubProjectDetailDto>()` chain, add:

```csharp
            .ForMember(
                dest => dest.ProjectLevelId,
                opt => opt.MapFrom(src => src.ProjectLevelId))
            .ForMember(
                dest => dest.ProjectLevelName,
                opt => opt.MapFrom(src => src.ProjectLevel.Name))
            .ForMember(
                dest => dest.ComponentTypeId,
                opt => opt.MapFrom(src => src.ComponentTypeId))
            .ForMember(
                dest => dest.ComponentTypeName,
                opt => opt.MapFrom(src => src.ComponentType.Name))
            .ForMember(
                dest => dest.AccountingUnitId,
                opt => opt.MapFrom(src => src.AccountingUnitId))
            .ForMember(
                dest => dest.AccountingUnitName,
                opt => opt.MapFrom(src => src.AccountingUnit.Name))
```

The `CreateMap<CreateSubProjectDto, SubProject>()` and `CreateMap<UpdateSubProjectDto, SubProject>()` chains need **no changes** — `ProjectLevelId`/`ComponentTypeId`/`AccountingUnitId` now exist with identical names on both the DTOs and the entity, so AutoMapper's default convention-based mapping already handles them (this is exactly why Step 2 named the new DTO properties to match the entity's new FK property names precisely).

- [ ] **Step 4: Update `SubProjectService`**

In `Backend/src/SmartInvest.Application/Services/SubProjectService.cs`:

Add 3 new repository fields and constructor parameters (alongside the existing `_markazRepository`/`_priorityRepository`/etc.):
```csharp
    private readonly IGenericRepository<ProjectLevel> _projectLevelRepository;
    private readonly IGenericRepository<ComponentType> _componentTypeRepository;
    private readonly IGenericRepository<AccountingUnit> _accountingUnitRepository;
```
(add matching constructor parameters `IGenericRepository<ProjectLevel> projectLevelRepository, IGenericRepository<ComponentType> componentTypeRepository, IGenericRepository<AccountingUnit> accountingUnitRepository` and the 3 assignment lines in the constructor body, same pattern as the existing repositories there).

Change `ValidateReferencesAsync`'s signature and body. Replace:
```csharp
    private async Task ValidateReferencesAsync(int mainProjectId, int markazId, int priorityId, int statusId, CancellationToken cancellationToken)
    {
        var mainProject = await _mainProjectRepository.GetByIdAsync(mainProjectId, cancellationToken);
        if (mainProject == null)
        {
            throw new NotFoundException("المشروع الرئيسي المحدد غير موجود");
        }

        var markaz = await _markazRepository.GetByIdAsync(markazId, cancellationToken);
        if (markaz == null)
        {
            throw new NotFoundException("المركز المحدد غير موجود");
        }

        var priority = await _priorityRepository.GetByIdAsync(priorityId, cancellationToken);
        if (priority == null)
        {
            throw new NotFoundException("الأولوية المحددة غير موجودة");
        }

        var status = await _statusRepository.GetByIdAsync(statusId, cancellationToken);
        if (status == null)
        {
            throw new NotFoundException("حالة المشروع المحددة غير موجودة");
        }
    }
```
with:
```csharp
    private async Task ValidateReferencesAsync(int mainProjectId, int markazId, int priorityId, int statusId, int projectLevelId, int componentTypeId, int accountingUnitId, CancellationToken cancellationToken)
    {
        var mainProject = await _mainProjectRepository.GetByIdAsync(mainProjectId, cancellationToken);
        if (mainProject == null)
        {
            throw new NotFoundException("المشروع الرئيسي المحدد غير موجود");
        }

        var markaz = await _markazRepository.GetByIdAsync(markazId, cancellationToken);
        if (markaz == null)
        {
            throw new NotFoundException("المركز المحدد غير موجود");
        }

        var priority = await _priorityRepository.GetByIdAsync(priorityId, cancellationToken);
        if (priority == null)
        {
            throw new NotFoundException("الأولوية المحددة غير موجودة");
        }

        var status = await _statusRepository.GetByIdAsync(statusId, cancellationToken);
        if (status == null)
        {
            throw new NotFoundException("حالة المشروع المحددة غير موجودة");
        }

        var projectLevel = await _projectLevelRepository.GetByIdAsync(projectLevelId, cancellationToken);
        if (projectLevel == null)
        {
            throw new NotFoundException("مستوى المشروع المحدد غير موجود");
        }

        var componentType = await _componentTypeRepository.GetByIdAsync(componentTypeId, cancellationToken);
        if (componentType == null)
        {
            throw new NotFoundException("المكوّن العيني المحدد غير موجود");
        }

        var accountingUnit = await _accountingUnitRepository.GetByIdAsync(accountingUnitId, cancellationToken);
        if (accountingUnit == null)
        {
            throw new NotFoundException("الوحدة الحسابية المحددة غير موجودة");
        }
    }
```

Update both call sites. In `CreateAsync`, replace:
```csharp
        await ValidateReferencesAsync(dto.MainProjectId, dto.MarkazId, dto.PriorityId, dto.StatusId, cancellationToken);
```
with:
```csharp
        await ValidateReferencesAsync(dto.MainProjectId, dto.MarkazId, dto.PriorityId, dto.StatusId, dto.ProjectLevelId, dto.ComponentTypeId, dto.AccountingUnitId, cancellationToken);
```

In `UpdateAsync`, replace:
```csharp
        await ValidateReferencesAsync(subProject.MainProjectId, dto.MarkazId, dto.PriorityId, dto.StatusId, cancellationToken);
```
with:
```csharp
        await ValidateReferencesAsync(subProject.MainProjectId, dto.MarkazId, dto.PriorityId, dto.StatusId, dto.ProjectLevelId, dto.ComponentTypeId, dto.AccountingUnitId, cancellationToken);
```

In `UpdateAsync`, replace the 3 string assignment lines:
```csharp
        subProject.ProjectLevel = dto.ProjectLevel;
        subProject.ComponentType = dto.ComponentType;
        subProject.AccountingUnit = dto.AccountingUnit;
```
with:
```csharp
        subProject.ProjectLevelId = dto.ProjectLevelId;
        subProject.ComponentTypeId = dto.ComponentTypeId;
        subProject.AccountingUnitId = dto.AccountingUnitId;
```

`CreateAsync` needs no equivalent assignment — `_mapper.Map<SubProject>(dto)` on the line `var subProject = _mapper.Map<SubProject>(dto);` already sets `ProjectLevelId`/`ComponentTypeId`/`AccountingUnitId` via AutoMapper's convention-based mapping, per Step 3's note.

- [ ] **Step 5: Update `SubProjectRepository`'s Include chains**

In `Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs`, `GetWithDetailsAsync`, add 3 more `.Include()` calls. Replace:
```csharp
        return await DbSet
            .Include(x => x.MainProject).ThenInclude(m => m.SubProgram).ThenInclude(sp => sp.MainProgram)
            .Include(x => x.Markaz).ThenInclude(m => m.Governorate)
            .Include(x => x.Priority)
            .Include(x => x.Status)
            .Include(x => x.ExecutiveAgency)
            .Include(x => x.ProjectSpecifications)
            .FirstOrDefaultAsync(x => x.SubProjectId == id, cancellationToken);
```
with:
```csharp
        return await DbSet
            .Include(x => x.MainProject).ThenInclude(m => m.SubProgram).ThenInclude(sp => sp.MainProgram)
            .Include(x => x.Markaz).ThenInclude(m => m.Governorate)
            .Include(x => x.Priority)
            .Include(x => x.Status)
            .Include(x => x.ExecutiveAgency)
            .Include(x => x.ProjectSpecifications)
            .Include(x => x.ProjectLevel)
            .Include(x => x.ComponentType)
            .Include(x => x.AccountingUnit)
            .FirstOrDefaultAsync(x => x.SubProjectId == id, cancellationToken);
```

In `SearchAsync`, replace:
```csharp
        var query = DbSet
            .Include(x => x.MainProject).ThenInclude(m => m.SubProgram).ThenInclude(sp => sp.MainProgram)
            .Include(x => x.Markaz)
            .Include(x => x.Priority)
            .Include(x => x.Status)
            .Include(x => x.ExecutiveAgency)
            .Include(x => x.ProjectAssignments).ThenInclude(a => a.Contractor)
            .AsQueryable();
```
with:
```csharp
        var query = DbSet
            .Include(x => x.MainProject).ThenInclude(m => m.SubProgram).ThenInclude(sp => sp.MainProgram)
            .Include(x => x.Markaz)
            .Include(x => x.Priority)
            .Include(x => x.Status)
            .Include(x => x.ExecutiveAgency)
            .Include(x => x.ProjectAssignments).ThenInclude(a => a.Contractor)
            .Include(x => x.ProjectLevel)
            .Include(x => x.ComponentType)
            .AsQueryable();
```
(`AccountingUnit` is intentionally omitted from `SearchAsync`'s Include — `SubProjectListItemDto` doesn't expose `AccountingUnitName`, only `SubProjectDetailDto` does, matching the same asymmetry the codebase already has between these two DTOs today.)

- [ ] **Step 6: Revisit Task 2's simplified delete guards in `LookupService`**

Now that `SubProject.ComponentTypeId`/`ProjectLevelId`/`AccountingUnitId` exist, replace the 3 simplified delete methods from Task 2 (`DeleteComponentTypeAsync`, `DeleteProjectLevelAsync`, `DeleteAccountingUnitAsync`) with their real-guard versions. Replace:
```csharp
    public async Task DeleteComponentTypeAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _componentTypeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المكوّن العيني رقم {id} غير موجود");

        _componentTypeRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
```
with:
```csharp
    public async Task DeleteComponentTypeAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _componentTypeRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"المكوّن العيني رقم {id} غير موجود");

        var linkedSubProjects = await _subProjectRepository.FindAsync(x => x.ComponentTypeId == id, cancellationToken);
        if (linkedSubProjects.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف المكوّن العيني لوجود مشروعات فرعية تستخدمه");
        }

        _componentTypeRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
```
Apply the exact same shape of change to `DeleteProjectLevelAsync` (guard on `x.ProjectLevelId == id`, message `"لا يمكن حذف مستوى المشروع لوجود مشروعات فرعية تستخدمه"`) and `DeleteAccountingUnitAsync` (guard on `x.AccountingUnitId == id`, message `"لا يمكن حذف الوحدة الحسابية لوجود مشروعات فرعية تستخدمها"`).

- [ ] **Step 7: Build the backend**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 8: Generate and apply the migration**

```bash
cd Backend/src/SmartInvest.API
dotnet ef migrations add ConvertSubProjectAttributesToLookupTables --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

Inspect the generated `Up()`. EF will likely generate `AddColumn<int>` for the 3 new FK columns as **nullable** first (since a brand-new required `int` column can't be added to a table with existing rows without a default) — if it instead generates them as non-nullable with a `defaultValue: 0`, that default is wrong (there's no lookup row with `Id = 0`) and must be corrected by hand. Rewrite the migration's `Up()` to this exact sequence regardless of what EF scaffolds, in this order:

```csharp
            migrationBuilder.AddColumn<int>(
                name: "ProjectLevelId",
                table: "SubProjects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComponentTypeId",
                table: "SubProjects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountingUnitId",
                table: "SubProjects",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE sp SET sp.ProjectLevelId = pl.Id
                FROM SubProjects sp
                JOIN ProjectLevel pl ON pl.Name = sp.ProjectLevel
            ");
            migrationBuilder.Sql(@"
                UPDATE sp SET sp.ProjectLevelId = (SELECT TOP 1 Id FROM ProjectLevel WHERE Name = N'غير محدد')
                FROM SubProjects sp
                WHERE sp.ProjectLevelId IS NULL
            ");

            migrationBuilder.Sql(@"
                UPDATE sp SET sp.ComponentTypeId = ct.Id
                FROM SubProjects sp
                JOIN ComponentType ct ON ct.Name = sp.ComponentType
            ");
            migrationBuilder.Sql(@"
                UPDATE sp SET sp.ComponentTypeId = (SELECT TOP 1 Id FROM ComponentType WHERE Name = N'غير محدد')
                FROM SubProjects sp
                WHERE sp.ComponentTypeId IS NULL
            ");

            migrationBuilder.Sql(@"
                UPDATE sp SET sp.AccountingUnitId = au.Id
                FROM SubProjects sp
                JOIN AccountingUnit au ON au.Name = sp.AccountingUnit
            ");
            migrationBuilder.Sql(@"
                UPDATE sp SET sp.AccountingUnitId = (SELECT TOP 1 Id FROM AccountingUnit WHERE Name = N'غير محدد')
                FROM SubProjects sp
                WHERE sp.AccountingUnitId IS NULL
            ");

            migrationBuilder.DropColumn(name: "ProjectLevel", table: "SubProjects");
            migrationBuilder.DropColumn(name: "ComponentType", table: "SubProjects");
            migrationBuilder.DropColumn(name: "AccountingUnit", table: "SubProjects");

            migrationBuilder.AlterColumn<int>(
                name: "ProjectLevelId",
                table: "SubProjects",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ComponentTypeId",
                table: "SubProjects",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AccountingUnitId",
                table: "SubProjects",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_ProjectLevelId",
                table: "SubProjects",
                column: "ProjectLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_ComponentTypeId",
                table: "SubProjects",
                column: "ComponentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubProjects_AccountingUnitId",
                table: "SubProjects",
                column: "AccountingUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubProjects_ProjectLevel_ProjectLevelId",
                table: "SubProjects",
                column: "ProjectLevelId",
                principalTable: "ProjectLevel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubProjects_ComponentType_ComponentTypeId",
                table: "SubProjects",
                column: "ComponentTypeId",
                principalTable: "ComponentType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SubProjects_AccountingUnit_AccountingUnitId",
                table: "SubProjects",
                column: "AccountingUnitId",
                principalTable: "AccountingUnit",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
```

This ordering matters: columns must exist (nullable) before the `UPDATE...JOIN` can populate them; the two-step `UPDATE` (first the real name match, then a fallback pass for anything still `NULL`) guarantees every row ends up with a valid FK before the column is tightened to `NOT NULL`; the old string columns are dropped only after their data has been fully consumed; indexes/FKs are added last, after the column is in its final shape. Rewrite `Down()` to reverse this (drop FKs/indexes, add back the 3 nullable string columns, populate them by joining back from the FK to the lookup table's `Name`, drop the FK columns) — since this migration is unlikely to ever need rolling back on a production-like dataset, a simpler `Down()` that just restores the string columns as nullable without perfectly repopulating them is acceptable; note this asymmetry with a comment in the migration file.

```bash
dotnet ef database update --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- [ ] **Step 9: Empty-probe verify**

```bash
dotnet ef migrations add ProbeCheck --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```
Confirm both `Up()`/`Down()` are empty, then remove the probe:
```bash
dotnet ef migrations remove --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- [ ] **Step 10: Manual check via Swagger**

Confirm `GET /api/subprojects/{id}` (an existing sub-project) now returns `componentTypeId`/`componentTypeName`/`projectLevelId`/`projectLevelName`/`accountingUnitId`/`accountingUnitName` instead of the old plain strings, and that the values are sensible (e.g. a sub-project whose old `ComponentType` was `"الات ومعدات"` now shows `componentTypeName: "الات ومعدات"` via its new FK). Confirm a sub-project whose old `AccountingUnit` was empty now shows `accountingUnitName: "غير محدد"`. Confirm `PUT /api/subprojects/{id}` with the new `projectLevelId`/`componentTypeId`/`accountingUnitId` integer fields (instead of the old strings) succeeds. Confirm attempting to delete a `ComponentType`/`ProjectLevel`/`AccountingUnit` still in use by a real sub-project now correctly fails with the business-rule message (this didn't work at the end of Task 2, only from this task onward).

- [ ] **Step 11: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Entities/SubProject.cs Backend/src/SmartInvest.Application/DTOs/SubProjectDtos.cs Backend/src/SmartInvest.Application/Common/Mappings/SubProjectMappingProfile.cs Backend/src/SmartInvest.Application/Services/SubProjectService.cs Backend/src/SmartInvest.Application/Services/LookupService.cs Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs Backend/src/SmartInvest.Infrastructure/Migrations/
git commit -m "refactor: convert SubProject's ComponentType/ProjectLevel/AccountingUnit to FK columns

Replaces the 3 free-text columns with real foreign keys into the
lookup tables added in the previous task. Existing data is preserved
via a name-match migration with a 'غير محدد' fallback for anything
that didn't match (in practice: every row's AccountingUnit, which has
always been blank)."
```

---

### Task 4: Backend — Custom Measurements

**Files:**
- Create: `Backend/src/SmartInvest.Domain/Entities/Measurement.cs`
- Create: `Backend/src/SmartInvest.Domain/Entities/MeasurementSubProgram.cs`
- Create: `Backend/src/SmartInvest.Domain/Entities/SubProjectMeasurementValue.cs`
- Create: `Backend/src/SmartInvest.Application/DTOs/MeasurementDtos.cs`
- Create: `Backend/src/SmartInvest.Application/Common/Mappings/MeasurementMappingProfile.cs`
- Create: `Backend/src/SmartInvest.Application/Interfaces/IMeasurementService.cs`
- Create: `Backend/src/SmartInvest.Application/Services/MeasurementService.cs`
- Create: `Backend/src/SmartInvest.API/Controllers/MeasurementsController.cs`
- Create: `Backend/src/SmartInvest.API/Controllers/SubProjectMeasurementValuesController.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`
- Create (generated): a new EF migration creating the 3 tables.

**Interfaces:**
- Consumes: nothing beyond `SubProgram`/`SubProject` already existing.
- Produces: `MeasurementDto { Id, Name, Unit, SubProgramIds: int[], SubProgramNames: string[] }`; `POST/PUT/DELETE api/measurements`; `GET api/measurements/applicable?subProgramId=X` (measurements linked to one sub-program, for the frontend sub-project form); `GET/PUT api/subprojects/{subProjectId}/measurement-values`. Task 8 (frontend measurements page) and Task 9 (sub-project form Step 4) consume these directly.

- [ ] **Step 1: Create the 3 entities**

`Backend/src/SmartInvest.Domain/Entities/Measurement.cs`:
```csharp
namespace SmartInvest.Domain.Entities
{
    public class Measurement
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        public virtual ICollection<MeasurementSubProgram> MeasurementSubPrograms { get; set; }
        public virtual ICollection<SubProjectMeasurementValue> Values { get; set; }
    }
}
```

`Backend/src/SmartInvest.Domain/Entities/MeasurementSubProgram.cs`:
```csharp
namespace SmartInvest.Domain.Entities
{
    public class MeasurementSubProgram
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Measurement")]
        public int MeasurementId { get; set; }
        public virtual Measurement Measurement { get; set; }

        [ForeignKey("SubProgram")]
        public int SubProgramId { get; set; }
        public virtual SubProgram SubProgram { get; set; }
    }
}
```

`Backend/src/SmartInvest.Domain/Entities/SubProjectMeasurementValue.cs`:
```csharp
namespace SmartInvest.Domain.Entities
{
    public class SubProjectMeasurementValue
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("SubProject")]
        public int SubProjectId { get; set; }
        public virtual SubProject SubProject { get; set; }

        [ForeignKey("Measurement")]
        public int MeasurementId { get; set; }
        public virtual Measurement Measurement { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; }
    }
}
```

- [ ] **Step 2: Create the DTOs**

`Backend/src/SmartInvest.Application/DTOs/MeasurementDtos.cs`:
```csharp
namespace SmartInvest.Application.DTOs;

public class MeasurementDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<int> SubProgramIds { get; set; } = new();
    public List<string> SubProgramNames { get; set; } = new();
}

public class CreateMeasurementDto
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<int> SubProgramIds { get; set; } = new();
}

public class UpdateMeasurementDto
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<int> SubProgramIds { get; set; } = new();
}

public class SubProjectMeasurementValueDto
{
    public int MeasurementId { get; set; }
    public string MeasurementName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? Value { get; set; }
}

public class SetMeasurementValueDto
{
    public int MeasurementId { get; set; }
    public decimal? Value { get; set; }
}

public class SetSubProjectMeasurementValuesDto
{
    public List<SetMeasurementValueDto> Values { get; set; } = new();
}
```

- [ ] **Step 3: Create the mapping profile**

`Backend/src/SmartInvest.Application/Common/Mappings/MeasurementMappingProfile.cs`:
```csharp
using AutoMapper;
using SmartInvest.Application.DTOs;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Application.Common.Mappings;

public class MeasurementMappingProfile : Profile
{
    public MeasurementMappingProfile()
    {
        CreateMap<Measurement, MeasurementDto>()
            .ForMember(
                dest => dest.SubProgramIds,
                opt => opt.MapFrom(src => src.MeasurementSubPrograms.Select(x => x.SubProgramId).ToList()))
            .ForMember(
                dest => dest.SubProgramNames,
                opt => opt.MapFrom(src => src.MeasurementSubPrograms.Select(x => x.SubProgram.SubProgramName).ToList()));
    }
}
```

- [ ] **Step 4: Create `IMeasurementService`**

`Backend/src/SmartInvest.Application/Interfaces/IMeasurementService.cs`:
```csharp
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IMeasurementService
{
    Task<IReadOnlyList<MeasurementDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MeasurementDto>> GetApplicableForSubProgramAsync(int subProgramId, CancellationToken cancellationToken = default);

    Task<MeasurementDto> CreateAsync(CreateMeasurementDto dto, CancellationToken cancellationToken = default);

    Task<MeasurementDto> UpdateAsync(int id, UpdateMeasurementDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubProjectMeasurementValueDto>> GetValuesForSubProjectAsync(int subProjectId, CancellationToken cancellationToken = default);

    Task SetValuesForSubProjectAsync(int subProjectId, SetSubProjectMeasurementValuesDto dto, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Implement `MeasurementService`**

`Backend/src/SmartInvest.Application/Services/MeasurementService.cs`:
```csharp
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Application.Services;

public class MeasurementService : IMeasurementService
{
    private readonly IGenericRepository<Measurement> _measurementRepository;
    private readonly IGenericRepository<MeasurementSubProgram> _linkRepository;
    private readonly IGenericRepository<SubProjectMeasurementValue> _valueRepository;
    private readonly IGenericRepository<SubProject> _subProjectRepository;
    private readonly IGenericRepository<SubProgram> _subProgramRepository;
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public MeasurementService(
        IGenericRepository<Measurement> measurementRepository,
        IGenericRepository<MeasurementSubProgram> linkRepository,
        IGenericRepository<SubProjectMeasurementValue> valueRepository,
        IGenericRepository<SubProject> subProjectRepository,
        IGenericRepository<SubProgram> subProgramRepository,
        AppDbContext context,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _measurementRepository = measurementRepository;
        _linkRepository = linkRepository;
        _valueRepository = valueRepository;
        _subProjectRepository = subProjectRepository;
        _subProgramRepository = subProgramRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<MeasurementDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var measurements = await _context.Set<Measurement>()
            .Include(x => x.MeasurementSubPrograms).ThenInclude(l => l.SubProgram)
            .ToListAsync(cancellationToken);
        return _mapper.Map<List<MeasurementDto>>(measurements);
    }

    public async Task<IReadOnlyList<MeasurementDto>> GetApplicableForSubProgramAsync(int subProgramId, CancellationToken cancellationToken = default)
    {
        var measurements = await _context.Set<Measurement>()
            .Include(x => x.MeasurementSubPrograms).ThenInclude(l => l.SubProgram)
            .Where(x => x.MeasurementSubPrograms.Any(l => l.SubProgramId == subProgramId))
            .ToListAsync(cancellationToken);
        return _mapper.Map<List<MeasurementDto>>(measurements);
    }

    public async Task<MeasurementDto> CreateAsync(CreateMeasurementDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateSubProgramIdsAsync(dto.SubProgramIds, cancellationToken);

        var entity = new Measurement
        {
            Name = dto.Name.Trim(),
            Unit = dto.Unit.Trim(),
            MeasurementSubPrograms = dto.SubProgramIds
                .Select(spId => new MeasurementSubProgram { SubProgramId = spId })
                .ToList(),
        };

        await _measurementRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdOrThrowAsync(entity.Id, cancellationToken);
    }

    public async Task<MeasurementDto> UpdateAsync(int id, UpdateMeasurementDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateSubProgramIdsAsync(dto.SubProgramIds, cancellationToken);

        var entity = await _context.Set<Measurement>()
            .Include(x => x.MeasurementSubPrograms)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException($"القياس رقم {id} غير موجود");

        entity.Name = dto.Name.Trim();
        entity.Unit = dto.Unit.Trim();

        foreach (var existingLink in entity.MeasurementSubPrograms.ToList())
        {
            _linkRepository.Remove(existingLink);
        }
        entity.MeasurementSubPrograms = dto.SubProgramIds
            .Select(spId => new MeasurementSubProgram { MeasurementId = id, SubProgramId = spId })
            .ToList();

        _measurementRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdOrThrowAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<Measurement>()
            .Include(x => x.MeasurementSubPrograms)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException($"القياس رقم {id} غير موجود");

        var linkedValues = await _valueRepository.FindAsync(x => x.MeasurementId == id, cancellationToken);
        if (linkedValues.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف القياس لوجود قيم مسجلة عليه");
        }

        if (entity.MeasurementSubPrograms.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف القياس وهو مرتبط ببرامج فرعية — قم بإلغاء الربط أولًا");
        }

        _measurementRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubProjectMeasurementValueDto>> GetValuesForSubProjectAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var subProject = await _context.Set<SubProject>()
            .Include(x => x.MainProject)
            .FirstOrDefaultAsync(x => x.SubProjectId == subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        var applicable = await GetApplicableForSubProgramAsync(subProject.MainProject.SubProgramId, cancellationToken);

        var existingValues = await _valueRepository.FindAsync(x => x.SubProjectId == subProjectId, cancellationToken);
        var valuesByMeasurementId = existingValues.ToDictionary(x => x.MeasurementId, x => x.Value);

        return applicable
            .Select(m => new SubProjectMeasurementValueDto
            {
                MeasurementId = m.Id,
                MeasurementName = m.Name,
                Unit = m.Unit,
                Value = valuesByMeasurementId.TryGetValue(m.Id, out var v) ? v : null,
            })
            .ToList();
    }

    public async Task SetValuesForSubProjectAsync(int subProjectId, SetSubProjectMeasurementValuesDto dto, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        var existingValues = await _valueRepository.FindAsync(x => x.SubProjectId == subProjectId, cancellationToken);
        var existingByMeasurementId = existingValues.ToDictionary(x => x.MeasurementId);

        foreach (var entry in dto.Values)
        {
            if (entry.Value == null)
            {
                if (existingByMeasurementId.TryGetValue(entry.MeasurementId, out var toRemove))
                {
                    _valueRepository.Remove(toRemove);
                }
                continue;
            }

            if (existingByMeasurementId.TryGetValue(entry.MeasurementId, out var toUpdate))
            {
                toUpdate.Value = entry.Value.Value;
                _valueRepository.Update(toUpdate);
            }
            else
            {
                await _valueRepository.AddAsync(new SubProjectMeasurementValue
                {
                    SubProjectId = subProjectId,
                    MeasurementId = entry.MeasurementId,
                    Value = entry.Value.Value,
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<MeasurementDto> GetByIdOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _context.Set<Measurement>()
            .Include(x => x.MeasurementSubPrograms).ThenInclude(l => l.SubProgram)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException($"القياس رقم {id} غير موجود");
        return _mapper.Map<MeasurementDto>(entity);
    }

    private async Task ValidateSubProgramIdsAsync(List<int> subProgramIds, CancellationToken cancellationToken)
    {
        foreach (var subProgramId in subProgramIds.Distinct())
        {
            var subProgram = await _subProgramRepository.GetByIdAsync(subProgramId, cancellationToken);
            if (subProgram == null)
            {
                throw new NotFoundException($"البرنامج الفرعي رقم {subProgramId} غير موجود");
            }
        }
    }
}
```

- [ ] **Step 6: Create `MeasurementsController`**

`Backend/src/SmartInvest.API/Controllers/MeasurementsController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/measurements")]
[Authorize]
public class MeasurementsController : ControllerBase
{
    private readonly IMeasurementService _measurementService;

    public MeasurementsController(IMeasurementService measurementService)
    {
        _measurementService = measurementService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MeasurementDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _measurementService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("applicable")]
    public async Task<ActionResult<IReadOnlyList<MeasurementDto>>> GetApplicable([FromQuery] int subProgramId, CancellationToken cancellationToken)
    {
        var result = await _measurementService.GetApplicableForSubProgramAsync(subProgramId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<MeasurementDto>> Create(CreateMeasurementDto dto, CancellationToken cancellationToken)
    {
        var result = await _measurementService.CreateAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<MeasurementDto>> Update(int id, UpdateMeasurementDto dto, CancellationToken cancellationToken)
    {
        var result = await _measurementService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _measurementService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
```

- [ ] **Step 7: Create `SubProjectMeasurementValuesController`**

`Backend/src/SmartInvest.API/Controllers/SubProjectMeasurementValuesController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/subprojects/{subProjectId:int}/measurement-values")]
[Authorize]
public class SubProjectMeasurementValuesController : ControllerBase
{
    private readonly IMeasurementService _measurementService;

    public SubProjectMeasurementValuesController(IMeasurementService measurementService)
    {
        _measurementService = measurementService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubProjectMeasurementValueDto>>> GetAll(int subProjectId, CancellationToken cancellationToken)
    {
        var result = await _measurementService.GetValuesForSubProjectAsync(subProjectId, cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> SetAll(int subProjectId, SetSubProjectMeasurementValuesDto dto, CancellationToken cancellationToken)
    {
        await _measurementService.SetValuesForSubProjectAsync(subProjectId, dto, cancellationToken);
        return NoContent();
    }
}
```

(`PlanningStaff`, not `PlanningManager`, on the write action here — matches `SubProjectFinancialYearsController`'s existing convention for sub-resources attached to a sub-project the staff member is actively editing, as opposed to top-level lookup management which is manager-only.)

- [ ] **Step 8: Register in DI**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, add one line among the existing service registrations:
```csharp
        services.AddScoped<IMeasurementService, MeasurementService>();
```

- [ ] **Step 9: Build the backend**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 10: Generate and apply the migration**

```bash
cd Backend/src/SmartInvest.API
dotnet ef migrations add AddMeasurements --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```
Inspect the generated migration — expect 3 `CreateTable` calls (`Measurement`, `MeasurementSubProgram` with FKs to `Measurement`/`SubProgram`, `SubProjectMeasurementValue` with FKs to `SubProject`/`Measurement`) plus their indexes. No hand-written SQL needed this time (nothing to backfill — these are brand new, empty concepts with no prior data).

```bash
dotnet ef database update --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- [ ] **Step 11: Empty-probe verify**

```bash
dotnet ef migrations add ProbeCheck --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```
Confirm empty, then remove:
```bash
dotnet ef migrations remove --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- [ ] **Step 12: Manual check via Swagger**

Create a measurement (`POST /api/measurements` with `{ "name": "الارتفاع", "unit": "متر", "subProgramIds": [1] }`), confirm it appears in `GET /api/measurements` with `subProgramNames` populated. Confirm `GET /api/measurements/applicable?subProgramId=1` returns it, and `?subProgramId=999` (or any sub-program it's not linked to) returns an empty list. Pick a real `SubProject` whose `MainProject.SubProgramId == 1`, confirm `GET /api/subprojects/{id}/measurement-values` returns this measurement with `value: null`; `PUT` the same endpoint with `{ "values": [{ "measurementId": <id>, "value": 12.5 }] }`, confirm a re-`GET` shows `value: 12.5`. Confirm deleting the measurement while still linked to the sub-program fails with the business-rule message; update it with an empty `subProgramIds` array to unlink, confirm delete now still fails (there's a recorded value); `PUT` measurement-values again with `value: null` for that measurement to clear it, confirm delete now succeeds.

- [ ] **Step 13: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Entities/Measurement.cs Backend/src/SmartInvest.Domain/Entities/MeasurementSubProgram.cs Backend/src/SmartInvest.Domain/Entities/SubProjectMeasurementValue.cs Backend/src/SmartInvest.Application/DTOs/MeasurementDtos.cs Backend/src/SmartInvest.Application/Common/Mappings/MeasurementMappingProfile.cs Backend/src/SmartInvest.Application/Interfaces/IMeasurementService.cs Backend/src/SmartInvest.Application/Services/MeasurementService.cs Backend/src/SmartInvest.API/Controllers/MeasurementsController.cs Backend/src/SmartInvest.API/Controllers/SubProjectMeasurementValuesController.cs Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs Backend/src/SmartInvest.Infrastructure/Migrations/
git commit -m "feat: add custom measurements (definitions, sub-program links, per-sub-project values)

Measurement { Name, Unit } many-to-many with SubProgram; a sub-project's
applicable measurements resolve via its Main Project's Sub Program.
Deleting a measurement is blocked while it has recorded values or
sub-program links, matching this app's existing delete-guard convention."
```

---

### Task 5: Frontend — Generic Settings Page + the 7 Hierarchy Lookups

**Files:**
- Modify: `Frontend/src/app/core/models/project.models.ts`
- Modify: `Frontend/src/app/core/services/lookups.service.ts`
- Create: `Frontend/src/app/features/settings/settings-lookup-table.ts`
- Create: `Frontend/src/app/features/settings/settings-lookup-table.html`
- Create: `Frontend/src/app/features/settings/settings-lookup-table.css`
- Create: `Frontend/src/app/features/settings/settings.ts`
- Create: `Frontend/src/app/features/settings/settings.html`
- Create: `Frontend/src/app/features/settings/settings.css`
- Modify: `Frontend/src/app/app.routes.ts`
- Modify: `Frontend/src/app/layout/main-layout/main-layout.ts`

**Interfaces:**
- Consumes: Task 1's backend write API for MainProgram/SubProgram/Governorate/Markaz/Village/Priority/Status.
- Produces: `SettingsLookupTable` — a generic, reusable presentational component (table + create/edit `si-modal`, optional parent-select) taking `items`/`hasParent`/`parentOptions`/`title`/`isManager` as inputs and emitting `create`/`update`/`delete` events. Task 6 reuses this exact component for 4 more lookup types.

- [ ] **Step 1: Add write-side models**

Append to `Frontend/src/app/core/models/project.models.ts`:

```ts
export interface CreateNamedLookup {
  name: string;
}

export type UpdateNamedLookup = CreateNamedLookup;

export interface CreateSubProgram {
  name: string;
  mainProgramId: number;
}

export type UpdateSubProgram = CreateSubProgram;

export interface CreateMarkaz {
  name: string;
  governorateId: number;
}

export type UpdateMarkaz = CreateMarkaz;

export interface CreateVillage {
  name: string;
  markazId: number;
}

export type UpdateVillage = CreateVillage;
```

- [ ] **Step 2: Extend `LookupsService` with write methods**

Replace `Frontend/src/app/core/services/lookups.service.ts` in full:

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateMarkaz,
  CreateNamedLookup,
  CreateSubProgram,
  CreateVillage,
  Lookup,
  MarkazLookup,
  SubProgramLookup,
  UpdateMarkaz,
  UpdateNamedLookup,
  UpdateSubProgram,
  UpdateVillage,
  VillageLookup,
} from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class LookupsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/lookups`;

  getPriorities(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/priorities`);
  }

  createPriority(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/priorities`, dto);
  }

  updatePriority(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/priorities/${id}`, dto);
  }

  deletePriority(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/priorities/${id}`);
  }

  getStatuses(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/statuses`);
  }

  createStatus(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/statuses`, dto);
  }

  updateStatus(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/statuses/${id}`, dto);
  }

  deleteStatus(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/statuses/${id}`);
  }

  getMainPrograms(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/main-programs`);
  }

  createMainProgram(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/main-programs`, dto);
  }

  updateMainProgram(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/main-programs/${id}`, dto);
  }

  deleteMainProgram(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/main-programs/${id}`);
  }

  getSubPrograms(mainProgramId?: number): Observable<SubProgramLookup[]> {
    let params = new HttpParams();
    if (mainProgramId != null) {
      params = params.set('mainProgramId', mainProgramId);
    }
    return this.http.get<SubProgramLookup[]>(`${this.base}/sub-programs`, { params });
  }

  createSubProgram(dto: CreateSubProgram): Observable<SubProgramLookup> {
    return this.http.post<SubProgramLookup>(`${this.base}/sub-programs`, dto);
  }

  updateSubProgram(id: number, dto: UpdateSubProgram): Observable<SubProgramLookup> {
    return this.http.put<SubProgramLookup>(`${this.base}/sub-programs/${id}`, dto);
  }

  deleteSubProgram(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/sub-programs/${id}`);
  }

  getGovernorates(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/governorates`);
  }

  createGovernorate(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/governorates`, dto);
  }

  updateGovernorate(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/governorates/${id}`, dto);
  }

  deleteGovernorate(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/governorates/${id}`);
  }

  getMarkaz(governorateId?: number): Observable<MarkazLookup[]> {
    let params = new HttpParams();
    if (governorateId != null) {
      params = params.set('governorateId', governorateId);
    }
    return this.http.get<MarkazLookup[]>(`${this.base}/markaz`, { params });
  }

  createMarkaz(dto: CreateMarkaz): Observable<MarkazLookup> {
    return this.http.post<MarkazLookup>(`${this.base}/markaz`, dto);
  }

  updateMarkaz(id: number, dto: UpdateMarkaz): Observable<MarkazLookup> {
    return this.http.put<MarkazLookup>(`${this.base}/markaz/${id}`, dto);
  }

  deleteMarkaz(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/markaz/${id}`);
  }

  getVillages(markazId?: number): Observable<VillageLookup[]> {
    let params = new HttpParams();
    if (markazId != null) {
      params = params.set('markazId', markazId);
    }
    return this.http.get<VillageLookup[]>(`${this.base}/villages`, { params });
  }

  createVillage(dto: CreateVillage): Observable<VillageLookup> {
    return this.http.post<VillageLookup>(`${this.base}/villages`, dto);
  }

  updateVillage(id: number, dto: UpdateVillage): Observable<VillageLookup> {
    return this.http.put<VillageLookup>(`${this.base}/villages/${id}`, dto);
  }

  deleteVillage(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/villages/${id}`);
  }
}
```

- [ ] **Step 3: Create the generic `SettingsLookupTable` component**

`Frontend/src/app/features/settings/settings-lookup-table.ts`:
```ts
import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface SettingsLookupItem {
  id: number;
  name: string;
  parentId?: number;
  parentName?: string;
}

export interface SettingsLookupParentOption {
  id: number;
  name: string;
}

export interface SettingsLookupSaveEvent {
  id: number | null;
  name: string;
  parentId: number | null;
}

@Component({
  selector: 'app-settings-lookup-table',
  imports: [FormsModule],
  templateUrl: './settings-lookup-table.html',
  styleUrl: './settings-lookup-table.css',
})
export class SettingsLookupTable {
  readonly title = input.required<string>();
  readonly addLabel = input.required<string>();
  readonly nameLabel = input('الاسم');
  readonly hasParent = input(false);
  readonly parentLabel = input('');
  readonly parentOptions = input<SettingsLookupParentOption[]>([]);
  readonly items = input.required<SettingsLookupItem[]>();
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly isManager = input(false);

  readonly save = output<SettingsLookupSaveEvent>();
  readonly remove = output<number>();

  protected readonly search = computed(() => this._search());
  private readonly _search = signal('');

  protected readonly filtered = computed(() => {
    const term = this._search().trim().toLowerCase();
    if (!term) return this.items();
    return this.items().filter((i) => i.name.toLowerCase().includes(term));
  });

  protected readonly showForm = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly formName = signal('');
  protected readonly formParentId = signal<number | null>(null);

  protected setSearch(value: string): void {
    this._search.set(value);
  }

  protected openAdd(): void {
    this.editingId.set(null);
    this.formName.set('');
    this.formParentId.set(null);
    this.showForm.set(true);
  }

  protected openEdit(item: SettingsLookupItem): void {
    this.editingId.set(item.id);
    this.formName.set(item.name);
    this.formParentId.set(item.parentId ?? null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
  }

  protected submitForm(): void {
    const name = this.formName().trim();
    if (!name) return;
    if (this.hasParent() && this.formParentId() == null) return;

    this.save.emit({
      id: this.editingId(),
      name,
      parentId: this.formParentId(),
    });
    this.showForm.set(false);
  }

  protected onDelete(item: SettingsLookupItem): void {
    if (!confirm(`تأكيد حذف «${item.name}»؟`)) return;
    this.remove.emit(item.id);
  }
}
```

- [ ] **Step 4: Create the component's template + styles**

`Frontend/src/app/features/settings/settings-lookup-table.html`:
```html
<div class="lookup-panel">
  <div class="panel-head">
    <div>
      <h2>{{ title() }}</h2>
    </div>
    @if (isManager()) {
      <button class="si-btn gold" (click)="openAdd()">
        <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12h14" /></svg>
        {{ addLabel() }}
      </button>
    }
  </div>

  <div class="search">
    <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="1.9"><circle cx="11" cy="11" r="7" /><path d="m21 21-4.3-4.3" /></svg>
    <input placeholder="بحث…" [ngModel]="search()" (ngModelChange)="setSearch($event)" />
  </div>

  @if (loading()) {
    <div class="state"><span class="spinner"></span> جاري التحميل…</div>
  } @else if (error()) {
    <div class="state error">{{ error() }}</div>
  } @else if (filtered().length === 0) {
    <div class="state">لا توجد عناصر مطابقة.</div>
  } @else {
    <div class="tbl-wrap">
      <table>
        <thead>
          <tr>
            <th>{{ nameLabel() }}</th>
            @if (hasParent()) { <th>{{ parentLabel() }}</th> }
            <th>إجراءات</th>
          </tr>
        </thead>
        <tbody>
          @for (item of filtered(); track item.id) {
            <tr>
              <td><b>{{ item.name }}</b></td>
              @if (hasParent()) { <td>{{ item.parentName }}</td> }
              <td>
                @if (isManager()) {
                  <div class="acts">
                    <button class="act" title="تعديل" (click)="openEdit(item)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" /></svg></button>
                    <button class="act danger" title="حذف" (click)="onDelete(item)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 7h16M9 7V5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2m3 0-1 13a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 7" /></svg></button>
                  </div>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  }

  @if (showForm()) {
    <div class="si-overlay" (click)="closeForm()">
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(480px,100%)">
        <div class="si-modal-head">
          <div class="grow"><h3>{{ editingId() ? 'تعديل' : addLabel() }}</h3></div>
          <button class="si-x" (click)="closeForm()" aria-label="إغلاق">×</button>
        </div>
        <div class="si-modal-body">
          <div class="si-grid">
            <div class="si-fld full">
              <label>{{ nameLabel() }} <span class="req">*</span></label>
              <input [ngModel]="formName()" (ngModelChange)="formName.set($event)" />
            </div>
            @if (hasParent()) {
              <div class="si-fld full">
                <label>{{ parentLabel() }} <span class="req">*</span></label>
                <select [ngModel]="formParentId()" (ngModelChange)="formParentId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (p of parentOptions(); track p.id) { <option [ngValue]="p.id">{{ p.name }}</option> }
                </select>
              </div>
            }
          </div>
        </div>
        <div class="si-modal-foot">
          <button class="si-btn primary" (click)="submitForm()">{{ editingId() ? 'حفظ التعديلات' : addLabel() }}</button>
          <button class="si-btn" (click)="closeForm()">إلغاء</button>
        </div>
      </div>
    </div>
  }
</div>
```

`Frontend/src/app/features/settings/settings-lookup-table.css`:
```css
.lookup-panel { display: flex; flex-direction: column; gap: 14px; }
.panel-head { display: flex; align-items: center; gap: 14px; }
.panel-head h2 { font-size: 17px; }
.panel-head .si-btn { margin-inline-start: auto; }

.search { display: flex; align-items: center; gap: 9px; background: var(--surface); border: 1px solid var(--line); border-radius: 11px; padding: 10px 12px; color: var(--muted); box-shadow: var(--shadow); }
.search input { border: 0; background: transparent; flex: 1; font-family: inherit; font-size: 13.5px; color: var(--ink); outline: none; }

.state { display: flex; align-items: center; justify-content: center; gap: 12px; background: var(--surface); border: 1px dashed var(--line-strong); border-radius: var(--radius); padding: 32px; color: var(--muted); box-shadow: var(--shadow); }
.state.error { color: #b32a39; border-color: #F3C6CC; background: var(--bad-bg); }
.spinner { width: 16px; height: 16px; border: 2px solid var(--line-strong); border-top-color: var(--green-700); border-radius: 50%; animation: spin .7s linear infinite; display: inline-block; }
@keyframes spin { to { transform: rotate(360deg); } }

.tbl-wrap { overflow-x: auto; background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius); box-shadow: var(--shadow); }
table { width: 100%; border-collapse: collapse; min-width: 400px; }
th { font-size: 11.5px; color: var(--muted); font-weight: 700; text-align: start; padding: 12px 14px; border-bottom: 1px solid var(--line); white-space: nowrap; background: var(--surface-2); }
td { padding: 12px 14px; border-bottom: 1px solid var(--line); font-size: 13px; }

.acts { display: inline-flex; gap: 6px; }
.act { width: 32px; height: 32px; border-radius: 8px; border: 1px solid var(--line); background: var(--surface); display: inline-grid; place-items: center; color: var(--muted); }
.act svg { width: 15px; height: 15px; }
.act.danger { color: var(--bad); border-color: #F3C6CC; }
.act.danger:hover { background: var(--bad-bg); }
```

- [ ] **Step 5: Create the settings page**

`Frontend/src/app/features/settings/settings.ts`:
```ts
import { Component, computed, inject, signal } from '@angular/core';
import { LookupsService } from '../../core/services/lookups.service';
import { AuthService } from '../../core/services/auth.service';
import { Lookup, MarkazLookup, SubProgramLookup, VillageLookup } from '../../core/models/project.models';
import { SettingsLookupItem, SettingsLookupParentOption, SettingsLookupSaveEvent, SettingsLookupTable } from './settings-lookup-table';

type TabKey = 'mainProgram' | 'subProgram' | 'governorate' | 'markaz' | 'village' | 'priority' | 'status';

interface TabDef {
  key: TabKey;
  label: string;
  addLabel: string;
  hasParent: boolean;
  parentLabel: string;
}

@Component({
  selector: 'app-settings',
  imports: [SettingsLookupTable],
  templateUrl: './settings.html',
  styleUrl: './settings.css',
})
export class Settings {
  private readonly lookups = inject(LookupsService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  protected readonly tabs: TabDef[] = [
    { key: 'mainProgram', label: 'البرامج الرئيسية', addLabel: 'إضافة برنامج رئيسي', hasParent: false, parentLabel: '' },
    { key: 'subProgram', label: 'البرامج الفرعية', addLabel: 'إضافة برنامج فرعي', hasParent: true, parentLabel: 'البرنامج الرئيسي' },
    { key: 'governorate', label: 'المحافظات', addLabel: 'إضافة محافظة', hasParent: false, parentLabel: '' },
    { key: 'markaz', label: 'المراكز', addLabel: 'إضافة مركز', hasParent: true, parentLabel: 'المحافظة' },
    { key: 'village', label: 'القرى', addLabel: 'إضافة قرية', hasParent: true, parentLabel: 'المركز' },
    { key: 'priority', label: 'الأولويات', addLabel: 'إضافة أولوية', hasParent: false, parentLabel: '' },
    { key: 'status', label: 'حالات المشروع', addLabel: 'إضافة حالة', hasParent: false, parentLabel: '' },
  ];

  protected readonly activeTab = signal<TabKey>('mainProgram');

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly mainPrograms = signal<Lookup[]>([]);
  private readonly subPrograms = signal<SubProgramLookup[]>([]);
  private readonly governorates = signal<Lookup[]>([]);
  private readonly markazList = signal<MarkazLookup[]>([]);
  private readonly villages = signal<VillageLookup[]>([]);
  private readonly priorities = signal<Lookup[]>([]);
  private readonly statuses = signal<Lookup[]>([]);

  protected readonly activeTabDef = computed(() => this.tabs.find((t) => t.key === this.activeTab())!);

  protected readonly parentOptions = computed<SettingsLookupParentOption[]>(() => {
    switch (this.activeTab()) {
      case 'subProgram':
        return this.mainPrograms().map((m) => ({ id: m.id, name: m.name }));
      case 'markaz':
        return this.governorates().map((g) => ({ id: g.id, name: g.name }));
      case 'village':
        return this.markazList().map((m) => ({ id: m.id, name: m.name }));
      default:
        return [];
    }
  });

  protected readonly items = computed<SettingsLookupItem[]>(() => {
    switch (this.activeTab()) {
      case 'mainProgram':
        return this.mainPrograms().map((m) => ({ id: m.id, name: m.name }));
      case 'subProgram':
        return this.subPrograms().map((s) => ({
          id: s.id,
          name: s.name,
          parentId: s.mainProgramId,
          parentName: this.mainPrograms().find((m) => m.id === s.mainProgramId)?.name ?? '',
        }));
      case 'governorate':
        return this.governorates().map((g) => ({ id: g.id, name: g.name }));
      case 'markaz':
        return this.markazList().map((m) => ({
          id: m.id,
          name: m.name,
          parentId: m.governorateId,
          parentName: this.governorates().find((g) => g.id === m.governorateId)?.name ?? '',
        }));
      case 'village':
        return this.villages().map((v) => ({
          id: v.id,
          name: v.name,
          parentId: v.markazId,
          parentName: this.markazList().find((m) => m.id === v.markazId)?.name ?? '',
        }));
      case 'priority':
        return this.priorities().map((p) => ({ id: p.id, name: p.name }));
      case 'status':
        return this.statuses().map((s) => ({ id: s.id, name: s.name }));
      default:
        return [];
    }
  });

  constructor() {
    this.loadAll();
  }

  protected selectTab(key: TabKey): void {
    this.activeTab.set(key);
  }

  private loadAll(): void {
    this.loading.set(true);
    this.error.set(null);
    Promise.all([
      this.toPromise(this.lookups.getMainPrograms(), this.mainPrograms),
      this.toPromise(this.lookups.getSubPrograms(), this.subPrograms),
      this.toPromise(this.lookups.getGovernorates(), this.governorates),
      this.toPromise(this.lookups.getMarkaz(), this.markazList),
      this.toPromise(this.lookups.getVillages(), this.villages),
      this.toPromise(this.lookups.getPriorities(), this.priorities),
      this.toPromise(this.lookups.getStatuses(), this.statuses),
    ])
      .then(() => this.loading.set(false))
      .catch(() => {
        this.loading.set(false);
        this.error.set('تعذّر تحميل الإعدادات');
      });
  }

  private toPromise<T>(obs: import('rxjs').Observable<T>, target: import('@angular/core').WritableSignal<T>): Promise<void> {
    return new Promise((resolve, reject) => {
      obs.subscribe({ next: (v) => { target.set(v); resolve(); }, error: reject });
    });
  }

  protected onSave(event: SettingsLookupSaveEvent): void {
    const tab = this.activeTab();
    const req = this.buildSaveRequest(tab, event);
    if (!req) return;
    req.subscribe({
      next: () => this.loadAll(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر الحفظ'),
    });
  }

  private buildSaveRequest(tab: TabKey, event: SettingsLookupSaveEvent) {
    const name = event.name;
    switch (tab) {
      case 'mainProgram':
        return event.id
          ? this.lookups.updateMainProgram(event.id, { name })
          : this.lookups.createMainProgram({ name });
      case 'subProgram': {
        const mainProgramId = event.parentId!;
        return event.id
          ? this.lookups.updateSubProgram(event.id, { name, mainProgramId })
          : this.lookups.createSubProgram({ name, mainProgramId });
      }
      case 'governorate':
        return event.id
          ? this.lookups.updateGovernorate(event.id, { name })
          : this.lookups.createGovernorate({ name });
      case 'markaz': {
        const governorateId = event.parentId!;
        return event.id
          ? this.lookups.updateMarkaz(event.id, { name, governorateId })
          : this.lookups.createMarkaz({ name, governorateId });
      }
      case 'village': {
        const markazId = event.parentId!;
        return event.id
          ? this.lookups.updateVillage(event.id, { name, markazId })
          : this.lookups.createVillage({ name, markazId });
      }
      case 'priority':
        return event.id
          ? this.lookups.updatePriority(event.id, { name })
          : this.lookups.createPriority({ name });
      case 'status':
        return event.id
          ? this.lookups.updateStatus(event.id, { name })
          : this.lookups.createStatus({ name });
      default:
        return null;
    }
  }

  protected onDelete(id: number): void {
    const tab = this.activeTab();
    const req = this.buildDeleteRequest(tab, id);
    req.subscribe({
      next: () => this.loadAll(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر الحذف'),
    });
  }

  private buildDeleteRequest(tab: TabKey, id: number) {
    switch (tab) {
      case 'mainProgram': return this.lookups.deleteMainProgram(id);
      case 'subProgram': return this.lookups.deleteSubProgram(id);
      case 'governorate': return this.lookups.deleteGovernorate(id);
      case 'markaz': return this.lookups.deleteMarkaz(id);
      case 'village': return this.lookups.deleteVillage(id);
      case 'priority': return this.lookups.deletePriority(id);
      case 'status': return this.lookups.deleteStatus(id);
    }
  }
}
```

- [ ] **Step 6: Create the settings page template + styles**

`Frontend/src/app/features/settings/settings.html`:
```html
<div class="page">
  <header class="page-head">
    <div>
      <h1>الإعدادات</h1>
      <p>إدارة القوائم الأساسية المستخدمة في المشروعات</p>
    </div>
  </header>

  <div class="layout">
    <nav class="tabs">
      @for (t of tabs; track t.key) {
        <button class="tab" [class.on]="activeTab() === t.key" (click)="selectTab(t.key)">{{ t.label }}</button>
      }
    </nav>

    <div class="content">
      <app-settings-lookup-table
        [title]="activeTabDef().label"
        [addLabel]="activeTabDef().addLabel"
        [hasParent]="activeTabDef().hasParent"
        [parentLabel]="activeTabDef().parentLabel"
        [parentOptions]="parentOptions()"
        [items]="items()"
        [loading]="loading()"
        [error]="error()"
        [isManager]="isManager()"
        (save)="onSave($event)"
        (remove)="onDelete($event)"
      />
    </div>
  </div>
</div>
```

`Frontend/src/app/features/settings/settings.css`:
```css
.page { padding: 24px 28px; }
.page-head { margin-bottom: 20px; }
.page-head h1 { font-size: 22px; }
.page-head p { margin: 2px 0 0; color: var(--muted); font-size: 13px; }

.layout { display: flex; gap: 20px; align-items: flex-start; }
.tabs { flex: 0 0 220px; display: flex; flex-direction: column; gap: 4px; background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius); padding: 8px; box-shadow: var(--shadow); }
.tab { text-align: start; padding: 10px 12px; border-radius: 9px; border: 0; background: transparent; color: var(--ink); font-weight: 700; font-size: 13px; }
.tab.on { background: var(--green-700); color: #fff; }
.content { flex: 1; min-width: 0; }

@media (max-width: 820px) {
  .layout { flex-direction: column; }
  .tabs { flex-direction: row; flex-wrap: wrap; flex-basis: auto; width: 100%; }
}
```

- [ ] **Step 7: Wire the route and nav**

In `Frontend/src/app/app.routes.ts`, add inside the `app` route's `children` array:
```ts
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/settings/settings').then((m) => m.Settings),
      },
```

In `Frontend/src/app/layout/main-layout/main-layout.ts`'s `allNav` array, add (all-staff visible — matches the Contractors/Agencies convention: page viewable by all staff, mutation buttons manager-gated inside the page):
```ts
    { label: 'الإعدادات', route: '/app/settings', icon: 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm7.4-3a7.4 7.4 0 0 1-.1 1.2l2.1 1.6-2 3.5-2.5-1a7.6 7.6 0 0 1-2 1.2l-.4 2.7H9.5l-.4-2.7a7.6 7.6 0 0 1-2-1.2l-2.5 1-2-3.5 2.1-1.6a7.4 7.4 0 0 1 0-2.4L2.6 8.6l2-3.5 2.5 1a7.6 7.6 0 0 1 2-1.2L9.5 2.2h5l.4 2.7a7.6 7.6 0 0 1 2 1.2l2.5-1 2 3.5-2.1 1.6c.1.4.1.8.1 1.2Z', managerOnly: false },
```

- [ ] **Step 8: Type-check**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 9: Manual check in the browser**

Navigate to `/app/settings`. Confirm all 7 tabs load their real data (Main Programs, Sub Programs showing their parent Main Program name, Governorates, Markaz showing parent Governorate, Villages showing parent Markaz, Priorities, Statuses). Create/edit/delete on a throwaway Main Program and a throwaway Sub Program (confirming the parent dropdown works). Confirm attempting to delete a Priority/Status/Markaz/etc. actually in use shows the backend's business-rule error via `alert()`.

- [ ] **Step 10: Commit**

```bash
git add Frontend/src/app/core/models/project.models.ts Frontend/src/app/core/services/lookups.service.ts Frontend/src/app/features/settings/ Frontend/src/app/app.routes.ts Frontend/src/app/layout/main-layout/main-layout.ts
git commit -m "feat: add settings page with generic CRUD for the 7 hierarchy lookups"
```

---

### Task 6: Frontend — Add Component Type, Project Level, Accounting Unit, Contract Type Tabs

**Files:**
- Modify: `Frontend/src/app/core/services/lookups.service.ts`
- Create: `Frontend/src/app/core/services/contract-types.service.ts`
- Modify: `Frontend/src/app/features/settings/settings.ts`
- Modify: `Frontend/src/app/features/settings/settings.html` (no change expected — confirm in Step 5)

**Interfaces:**
- Consumes: Task 2's `api/lookups/component-types`/`project-levels`/`accounting-units`, and the pre-existing (untouched) `api/contract-types`. Reuses `SettingsLookupTable` from Task 5 as-is — no changes to that component.
- Produces: 4 more working tabs on the same `/app/settings` page.

- [ ] **Step 1: Extend `LookupsService`**

Add to `Frontend/src/app/core/services/lookups.service.ts` (inside the class, after the Village methods from Task 5):

```ts
  getComponentTypes(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/component-types`);
  }

  createComponentType(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/component-types`, dto);
  }

  updateComponentType(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/component-types/${id}`, dto);
  }

  deleteComponentType(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/component-types/${id}`);
  }

  getProjectLevels(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/project-levels`);
  }

  createProjectLevel(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/project-levels`, dto);
  }

  updateProjectLevel(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/project-levels/${id}`, dto);
  }

  deleteProjectLevel(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/project-levels/${id}`);
  }

  getAccountingUnits(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/accounting-units`);
  }

  createAccountingUnit(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/accounting-units`, dto);
  }

  updateAccountingUnit(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/accounting-units/${id}`, dto);
  }

  deleteAccountingUnit(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/accounting-units/${id}`);
  }
```

- [ ] **Step 2: Create `ContractTypesService`**

`Frontend/src/app/core/services/contract-types.service.ts`:
```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { CreateNamedLookup, Lookup, UpdateNamedLookup } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class ContractTypesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/contract-types`;

  getAll(): Observable<Lookup[]> {
    return this.http.get<{ id: number; contractName: string }[]>(this.base).pipe(
      map((items) => items.map((i) => ({ id: i.id, name: i.contractName }))),
    );
  }

  create(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http
      .post<{ id: number; contractName: string }>(this.base, { contractName: dto.name })
      .pipe(map((i) => ({ id: i.id, name: i.contractName })));
  }

  update(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http
      .put<{ id: number; contractName: string }>(`${this.base}/${id}`, { contractName: dto.name })
      .pipe(map((i) => ({ id: i.id, name: i.contractName })));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
```
The backend's `ContractTypeDto` shape is `{ id, contractName }` (an older, un-migrated DTO from before this app settled on `{ id, name }` for lookups), so this service adapts it to the same `Lookup { id, name }` shape everything else in this plan uses, keeping `settings.ts` uniform.

- [ ] **Step 3: Wire into `settings.ts`**

In `Frontend/src/app/features/settings/settings.ts`:

Add the import:
```ts
import { ContractTypesService } from '../../core/services/contract-types.service';
```

Add the injected service:
```ts
  private readonly contractTypes = inject(ContractTypesService);
```

Extend the `TabKey` type:
```ts
type TabKey = 'mainProgram' | 'subProgram' | 'governorate' | 'markaz' | 'village' | 'priority' | 'status' | 'componentType' | 'projectLevel' | 'accountingUnit' | 'contractType';
```

Add 4 more entries to the `tabs` array:
```ts
    { key: 'componentType', label: 'المكوّن العيني', addLabel: 'إضافة مكوّن عيني', hasParent: false, parentLabel: '' },
    { key: 'projectLevel', label: 'مستوى المشروع', addLabel: 'إضافة مستوى', hasParent: false, parentLabel: '' },
    { key: 'accountingUnit', label: 'الوحدة الحسابية', addLabel: 'إضافة وحدة حسابية', hasParent: false, parentLabel: '' },
    { key: 'contractType', label: 'أنواع العقود', addLabel: 'إضافة نوع عقد', hasParent: false, parentLabel: '' },
```

Add 4 more state signals (alongside `mainPrograms`/`subPrograms`/etc.):
```ts
  private readonly componentTypes = signal<Lookup[]>([]);
  private readonly projectLevels = signal<Lookup[]>([]);
  private readonly accountingUnits = signal<Lookup[]>([]);
  private readonly contractTypeList = signal<Lookup[]>([]);
```

In the `items` computed's `switch`, add 4 more cases (before `default`):
```ts
      case 'componentType':
        return this.componentTypes().map((c) => ({ id: c.id, name: c.name }));
      case 'projectLevel':
        return this.projectLevels().map((p) => ({ id: p.id, name: p.name }));
      case 'accountingUnit':
        return this.accountingUnits().map((a) => ({ id: a.id, name: a.name }));
      case 'contractType':
        return this.contractTypeList().map((c) => ({ id: c.id, name: c.name }));
```

In `loadAll()`'s `Promise.all([...])` array, add 4 more entries:
```ts
      this.toPromise(this.lookups.getComponentTypes(), this.componentTypes),
      this.toPromise(this.lookups.getProjectLevels(), this.projectLevels),
      this.toPromise(this.lookups.getAccountingUnits(), this.accountingUnits),
      this.toPromise(this.contractTypes.getAll(), this.contractTypeList),
```

In `buildSaveRequest`'s `switch`, add 4 more cases (before `default: return null;`):
```ts
      case 'componentType':
        return event.id
          ? this.lookups.updateComponentType(event.id, { name })
          : this.lookups.createComponentType({ name });
      case 'projectLevel':
        return event.id
          ? this.lookups.updateProjectLevel(event.id, { name })
          : this.lookups.createProjectLevel({ name });
      case 'accountingUnit':
        return event.id
          ? this.lookups.updateAccountingUnit(event.id, { name })
          : this.lookups.createAccountingUnit({ name });
      case 'contractType':
        return event.id
          ? this.contractTypes.update(event.id, { name })
          : this.contractTypes.create({ name });
```

In `buildDeleteRequest`'s `switch`, add 4 more cases:
```ts
      case 'componentType': return this.lookups.deleteComponentType(id);
      case 'projectLevel': return this.lookups.deleteProjectLevel(id);
      case 'accountingUnit': return this.lookups.deleteAccountingUnit(id);
      case 'contractType': return this.contractTypes.delete(id);
```

- [ ] **Step 4: Type-check**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 5: Manual check in the browser**

On `/app/settings`, confirm all 11 tabs now appear and load real data — in particular, confirm "المكوّن العيني" and "مستوى المشروع" show the real distinct values already seeded from existing data (Task 2), and "الوحدة الحسابية" shows at least `غير محدد`. Create/edit/delete a throwaway Contract Type, confirming the `contractName`/`name` field adaptation round-trips correctly (the create form only ever shows/sends `name` — confirm the request body sent to `POST /api/contract-types` is `{ contractName: "..." }`, via the network tab, not `{ name: "..." }`).

- [ ] **Step 6: Commit**

```bash
git add Frontend/src/app/core/services/lookups.service.ts Frontend/src/app/core/services/contract-types.service.ts Frontend/src/app/features/settings/settings.ts
git commit -m "feat: add component type, project level, accounting unit, contract type tabs to settings"
```

---

### Task 7: Frontend — Update Sub-Project Form + Projects Filter for FK-Based Fields

**Files:**
- Modify: `Frontend/src/app/core/models/project.models.ts`
- Modify: `Frontend/src/app/features/projects/sub-project-form.ts`
- Modify: `Frontend/src/app/features/projects/projects.ts`

**Interfaces:**
- Consumes: Task 3's backend shape (`SubProjectListItemDto`/`SubProjectDetailDto` now have `projectLevelId`/`projectLevelName`/`componentTypeId`/`componentTypeName`(+`accountingUnitId`/`accountingUnitName` on Detail); `CreateSubProjectDto`/`UpdateSubProjectDto` now take `projectLevelId`/`componentTypeId`/`accountingUnitId` as `int`), and Task 5/6's `LookupsService.getProjectLevels()`/`getComponentTypes()`/`getAccountingUnits()`.

- [ ] **Step 1: Update the frontend models**

In `Frontend/src/app/core/models/project.models.ts`:

In `SubProjectListItem`, replace:
```ts
  projectLevel: string;
  componentType: string;
```
with:
```ts
  projectLevelId: number;
  projectLevelName: string;
  componentTypeId: number;
  componentTypeName: string;
```

In `SubProjectDetail`, replace:
```ts
  projectLevel: string;
  componentType: string;
  accountingUnit: string;
```
with:
```ts
  projectLevelId: number;
  projectLevelName: string;
  componentTypeId: number;
  componentTypeName: string;
  accountingUnitId: number;
  accountingUnitName: string;
```

In `CreateSubProject`, replace:
```ts
  projectLevel: string;
  componentType: string;
  accountingUnit: string;
```
with:
```ts
  projectLevelId: number;
  componentTypeId: number;
  accountingUnitId: number;
```

`UpdateSubProject` is already `type UpdateSubProject = Omit<CreateSubProject, 'mainProjectId'>;` — no separate edit needed there, it inherits the change automatically.

- [ ] **Step 2: Fix the projects-page level filter**

In `Frontend/src/app/features/projects/projects.ts`, line ~87, replace:
```ts
    if (this.fLevel() && s.projectLevel !== this.fLevel()) return false;
```
with:
```ts
    if (this.fLevel() && s.projectLevelName !== this.fLevel()) return false;
```

- [ ] **Step 3: Update `sub-project-form.ts`**

Replace `Frontend/src/app/features/projects/sub-project-form.ts` in full:

```ts
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ProjectsService } from '../../core/services/projects.service';
import { LookupsService } from '../../core/services/lookups.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { AuthService } from '../../core/services/auth.service';
import {
  FinancialYear,
  Lookup,
  MainProjectListItem,
  MarkazLookup,
  SubProjectListItem,
} from '../../core/models/project.models';

export interface LockedParent {
  id: number;
  code: string | null;
  name: string;
}

@Component({
  selector: 'app-sub-project-form',
  imports: [FormsModule],
  template: `
    @if (open()) {
      <div class="si-overlay" (click)="close.emit()">
        <div class="si-modal" (click)="$event.stopPropagation()">
          <div class="si-modal-head">
            <div class="grow">
              <h3>{{ edit() ? 'تعديل مشروع فرعي' : 'إضافة مشروع فرعي' }}</h3>
              <p>{{ edit() ? 'إدخال الكود يعتمد المشروع تلقائيًا، وإزالته يعيده لمقترح' : 'يُنشأ المشروع كمقترح ما لم يُدخَل كود' }}</p>
            </div>
            <button class="si-x" (click)="close.emit()" aria-label="إغلاق">×</button>
          </div>

          <div class="si-modal-body">
            @if (error()) { <div class="si-err">{{ error() }}</div> }

            <!-- المشروع الرئيسي -->
            @if (locked()) {
              <div class="si-locked">
                <div class="lh">
                  <div><b>{{ locked()!.name }}</b><div class="lc">الكود: {{ locked()!.code ?? 'بانتظار الاعتماد' }}</div></div>
                  <span class="lb">🔒 المشروع الرئيسي التابع له</span>
                </div>
              </div>
            } @else {
              <div class="si-grid">
                <div class="si-fld full">
                  <label>المشروع الرئيسي <span class="req">*</span></label>
                  <select [ngModel]="mainProjectId()" (ngModelChange)="mainProjectId.set($event)" [disabled]="!!edit()">
                    <option [ngValue]="null">— اختر المشروع الرئيسي —</option>
                    @for (m of mains(); track m.id) { <option [ngValue]="m.id">{{ m.code }} — {{ m.name }}</option> }
                  </select>
                </div>
              </div>
            }

            <div class="si-step"><span class="n">2</span><h4>بيانات المشروع الفرعي</h4></div>
            <div class="si-grid">
              <div class="si-fld full">
                <label>اسم المشروع الفرعي <span class="req">*</span></label>
                <input [ngModel]="name()" (ngModelChange)="name.set($event)" placeholder="مثال: رصف طريق المحطة" />
              </div>
              <div class="si-fld full">
                <label>كود المشروع الفرعي (اختياري)</label>
                <input [ngModel]="code()" (ngModelChange)="code.set($event)" placeholder="SP-2627-XXX-A" />
                <div class="hint">إدخال كود يعتمد المشروع تلقائيًا فور الحفظ؛ تركه فارغًا يبقيه مقترحًا.</div>
              </div>
              <div class="si-fld">
                <label>المستوى <span class="req">*</span></label>
                <select [ngModel]="projectLevelId()" (ngModelChange)="projectLevelId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (pl of projectLevels(); track pl.id) { <option [ngValue]="pl.id">{{ pl.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>المكوّن العيني <span class="req">*</span></label>
                <select [ngModel]="componentTypeId()" (ngModelChange)="componentTypeId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (c of componentTypes(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>الوحدة الحسابية <span class="req">*</span></label>
                <select [ngModel]="accountingUnitId()" (ngModelChange)="accountingUnitId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (a of accountingUnits(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>المركز <span class="req">*</span></label>
                <select [ngModel]="markazId()" (ngModelChange)="markazId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (mk of markazList(); track mk.id) { <option [ngValue]="mk.id">{{ mk.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>الأولوية <span class="req">*</span></label>
                <select [ngModel]="priorityId()" (ngModelChange)="priorityId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (p of priorities(); track p.id) { <option [ngValue]="p.id">{{ p.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>حالة المشروع <span class="req">*</span></label>
                <select [ngModel]="statusId()" (ngModelChange)="statusId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (st of statuses(); track st.id) { <option [ngValue]="st.id">{{ st.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>تمويل بنكي (ج.م)</label>
                <input type="number" [ngModel]="bankFunding()" (ngModelChange)="bankFunding.set($event)" placeholder="0" />
              </div>
              <div class="si-fld">
                <label>تمويل ذاتي (ج.م)</label>
                <input type="number" [ngModel]="selfFunding()" (ngModelChange)="selfFunding.set($event)" placeholder="0" />
              </div>
              <div class="si-fld full">
                <label>ملاحظات / وصف</label>
                <textarea [ngModel]="description()" (ngModelChange)="description.set($event)" placeholder="أي ملاحظات إضافية…"></textarea>
              </div>
            </div>

            <div class="si-step"><span class="n">3</span><h4>السنوات المالية</h4></div>
            <div class="si-years">
              @for (y of financialYears(); track y.id) {
                <label class="si-year-chk">
                  <input type="checkbox" [checked]="checkedYearIds().has(y.id)" (change)="toggleYear(y.id)" />
                  {{ y.name }}
                </label>
              } @empty {
                <p class="hint">لا توجد سنوات مالية بعد.</p>
              }
            </div>
          </div>

          <div class="si-modal-foot">
            <button class="si-btn primary" [disabled]="saving()" (click)="submit()">
              @if (saving()) { <span class="mini-sp"></span> جاري الحفظ… } @else { {{ edit() ? 'حفظ التعديلات' : 'إضافة المشروع الفرعي' }} }
            </button>
            @if (edit() && isManager()) {
              <button class="si-btn danger" type="button" [disabled]="saving()" (click)="onDelete()">حذف المشروع</button>
            }
            <button class="si-btn" (click)="close.emit()">إلغاء</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`.mini-sp{width:14px;height:14px;border:2px solid rgba(255,255,255,.4);border-top-color:#fff;border-radius:50%;animation:spin .7s linear infinite;display:inline-block}@keyframes spin{to{transform:rotate(360deg)}}.si-years{display:flex;flex-wrap:wrap;gap:10px;margin-bottom:16px}.si-year-chk{display:flex;align-items:center;gap:7px;border:1px solid var(--line-strong);border-radius:9px;padding:8px 12px;font-size:13px;font-weight:700;background:var(--surface)}.si-years .hint{font-size:12px;color:var(--muted)}`],
})
export class SubProjectForm {
  private readonly projectsService = inject(ProjectsService);
  private readonly lookups = inject(LookupsService);
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  readonly open = input(false);
  readonly edit = input<SubProjectListItem | null>(null);
  readonly locked = input<LockedParent | null>(null);
  readonly mains = input<MainProjectListItem[]>([]);
  readonly defaultYearId = input<number | null>(null);
  readonly close = output<void>();
  readonly saved = output<void>();
  readonly delete = output<void>();

  protected readonly priorities = signal<Lookup[]>([]);
  protected readonly statuses = signal<Lookup[]>([]);
  protected readonly markazList = signal<MarkazLookup[]>([]);
  protected readonly projectLevels = signal<Lookup[]>([]);
  protected readonly componentTypes = signal<Lookup[]>([]);
  protected readonly accountingUnits = signal<Lookup[]>([]);

  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly checkedYearIds = signal<Set<number>>(new Set());
  private originalYearIds = new Set<number>();

  protected readonly mainProjectId = signal<number | null>(null);
  protected readonly code = signal('');
  protected readonly name = signal('');
  protected readonly projectLevelId = signal<number | null>(null);
  protected readonly componentTypeId = signal<number | null>(null);
  protected readonly accountingUnitId = signal<number | null>(null);
  protected readonly markazId = signal<number | null>(null);
  protected readonly priorityId = signal<number | null>(null);
  protected readonly statusId = signal<number | null>(null);
  protected readonly bankFunding = signal<number>(0);
  protected readonly selfFunding = signal<number>(0);
  protected readonly description = signal('');

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  private lookupsLoaded = false;
  private wasOpen = false;

  constructor() {
    effect(() => {
      const isOpen = this.open();
      if (isOpen && !this.wasOpen) {
        this.wasOpen = true;
        this.onOpen();
      } else if (!isOpen) {
        this.wasOpen = false;
      }
    });
  }

  private onOpen(): void {
    this.error.set(null);
    this.ensureLookups(() => this.prefill());
  }

  private ensureLookups(done: () => void): void {
    if (this.lookupsLoaded) {
      done();
      return;
    }
    forkJoin({
      priorities: this.lookups.getPriorities(),
      statuses: this.lookups.getStatuses(),
      markaz: this.lookups.getMarkaz(),
      projectLevels: this.lookups.getProjectLevels(),
      componentTypes: this.lookups.getComponentTypes(),
      accountingUnits: this.lookups.getAccountingUnits(),
      financialYears: this.financialYearsService.getAll(),
    }).subscribe({
      next: ({ priorities, statuses, markaz, projectLevels, componentTypes, accountingUnits, financialYears }) => {
        this.priorities.set(priorities);
        this.statuses.set(statuses);
        this.markazList.set(markaz);
        this.projectLevels.set(projectLevels);
        this.componentTypes.set(componentTypes);
        this.accountingUnits.set(accountingUnits);
        this.financialYears.set(financialYears);
        this.lookupsLoaded = true;
        done();
      },
      error: () => this.error.set('تعذّر تحميل القوائم'),
    });
  }

  private prefill(): void {
    this.resetForm();
    const e = this.edit();
    const lockedParent = this.locked();

    if (lockedParent) {
      this.mainProjectId.set(lockedParent.id);
    }

    if (e) {
      // جلب التفاصيل الكاملة للتعديل
      this.projectsService.getSubProject(e.id).subscribe({
        next: (d) => {
          this.mainProjectId.set(d.mainProjectId);
          this.code.set(d.code ?? '');
          this.name.set(d.name);
          this.projectLevelId.set(d.projectLevelId);
          this.componentTypeId.set(d.componentTypeId);
          this.accountingUnitId.set(d.accountingUnitId);
          this.markazId.set(d.markazId);
          this.priorityId.set(d.priorityId);
          this.statusId.set(d.statusId);
          this.bankFunding.set(d.bankFunding);
          this.selfFunding.set(d.selfFunding);
          this.description.set(d.description ?? '');
        },
        error: () => this.error.set('تعذّر تحميل بيانات المشروع الفرعي'),
      });

      this.projectsService.getSubProjectFinancialYears(e.id).subscribe({
        next: (links) => {
          const ids = new Set(links.map((l) => l.financialYearId));
          this.originalYearIds = ids;
          this.checkedYearIds.set(new Set(ids));
        },
        error: () => this.error.set('تعذّر تحميل السنوات المالية المرتبطة بهذا المشروع'),
      });
    } else {
      this.originalYearIds = new Set<number>();
      const defaultId = this.defaultYearId();
      this.checkedYearIds.set(defaultId != null ? new Set([defaultId]) : new Set<number>());
    }
  }

  private resetForm(): void {
    this.mainProjectId.set(null);
    this.code.set('');
    this.name.set('');
    this.projectLevelId.set(null);
    this.componentTypeId.set(null);
    this.accountingUnitId.set(null);
    this.markazId.set(null);
    this.priorityId.set(null);
    this.statusId.set(null);
    this.bankFunding.set(0);
    this.selfFunding.set(0);
    this.description.set('');
    this.checkedYearIds.set(new Set());
    this.originalYearIds = new Set<number>();
  }

  protected onDelete(): void {
    this.delete.emit();
  }

  protected toggleYear(id: number): void {
    this.checkedYearIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected submit(): void {
    if (this.saving()) return;
    this.error.set(null);

    if (!this.name().trim()) { this.error.set('برجاء إدخال اسم المشروع الفرعي'); return; }
    if (this.projectLevelId() == null) { this.error.set('برجاء اختيار المستوى'); return; }
    if (this.componentTypeId() == null) { this.error.set('برجاء اختيار المكوّن العيني'); return; }
    if (this.accountingUnitId() == null) { this.error.set('برجاء اختيار الوحدة الحسابية'); return; }
    if (this.markazId() == null) { this.error.set('برجاء اختيار المركز'); return; }
    if (this.priorityId() == null) { this.error.set('برجاء اختيار الأولوية'); return; }
    if (this.statusId() == null) { this.error.set('برجاء اختيار حالة المشروع'); return; }
    if (!this.edit() && this.mainProjectId() == null) { this.error.set('برجاء اختيار المشروع الرئيسي'); return; }

    const base = {
      code: this.code().trim() || null,
      name: this.name().trim(),
      projectLevelId: this.projectLevelId()!,
      componentTypeId: this.componentTypeId()!,
      accountingUnitId: this.accountingUnitId()!,
      projectNature: '',
      markazId: this.markazId()!,
      priorityId: this.priorityId()!,
      statusId: this.statusId()!,
      bankFunding: Number(this.bankFunding()) || 0,
      selfFunding: Number(this.selfFunding()) || 0,
      latitude: null,
      longitude: null,
      description: this.description().trim() || null,
    };

    this.saving.set(true);
    const editing = this.edit();
    const req = editing
      ? this.projectsService.updateSubProject(editing.id, base)
      : this.projectsService.createSubProject({ ...base, mainProjectId: this.mainProjectId()! });

    req.subscribe({
      next: (result) => {
        const subProjectId = editing ? editing.id : result.id;
        this.syncFinancialYears(subProjectId);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر حفظ المشروع الفرعي');
      },
    });
  }

  private syncFinancialYears(subProjectId: number): void {
    const desired = this.checkedYearIds();
    const toLink = [...desired].filter((id) => !this.originalYearIds.has(id));
    const toUnlink = [...this.originalYearIds].filter((id) => !desired.has(id));
    const calls = [
      ...toLink.map((id) => this.projectsService.linkFinancialYear(subProjectId, id)),
      ...toUnlink.map((id) => this.projectsService.unlinkFinancialYear(subProjectId, id)),
    ];

    if (calls.length === 0) {
      this.saving.set(false);
      this.saved.emit();
      return;
    }

    forkJoin(calls).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر تحديث ربط السنوات المالية');
      },
    });
  }
}
```

Changes from the original: the hardcoded `componentTypes` string array and the 2-option inline `<select>` for level are gone, replaced by `projectLevels`/`componentTypes`/`accountingUnits` signals populated from `LookupsService` alongside the existing `priorities`/`statuses`/`markazList`; all 3 form fields switch from string signals to `number | null` id signals; a new "الوحدة الحسابية" field is added to the form (previously absent — the form always sent a hardcoded `accountingUnit: ''`, which is why every existing sub-project's `AccountingUnit` was blank).

- [ ] **Step 4: Type-check**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 5: Manual check in the browser**

Open the sub-project form (add and edit). Confirm "المستوى"/"المكوّن العيني"/"الوحدة الحسابية" are now populated from real API data (not the old hardcoded lists), and editing an existing sub-project correctly pre-selects its current values. Save a new sub-project and confirm its `accountingUnit` is no longer forced blank. On `/app/projects`, confirm the "المستوى" filter dropdown still narrows the list correctly using the two existing values.

- [ ] **Step 6: Commit**

```bash
git add Frontend/src/app/core/models/project.models.ts Frontend/src/app/features/projects/sub-project-form.ts Frontend/src/app/features/projects/projects.ts
git commit -m "refactor: switch sub-project form to FK-based project level/component type/accounting unit

Also adds the previously-missing Accounting Unit field to the form —
it existed on the backend but was always silently sent as an empty
string until now."
```

---

### Task 8: Frontend — Measurements Management Page

**Files:**
- Modify: `Frontend/src/app/core/models/project.models.ts`
- Create: `Frontend/src/app/core/services/measurements.service.ts`
- Create: `Frontend/src/app/features/measurements/measurements.ts`
- Create: `Frontend/src/app/features/measurements/measurements.html`
- Create: `Frontend/src/app/features/measurements/measurements.css`
- Modify: `Frontend/src/app/app.routes.ts`
- Modify: `Frontend/src/app/layout/main-layout/main-layout.ts`

**Interfaces:**
- Consumes: Task 4's `api/measurements` CRUD + `LookupsService.getSubPrograms()` (already exists) for the multi-select picker.
- Produces: `Measurement`, `CreateMeasurement`, `UpdateMeasurement` models; `MeasurementsService`; route `/app/measurements`. Task 9 (sub-project form Step 4) consumes `MeasurementsService` too.

- [ ] **Step 1: Add the models**

Append to `Frontend/src/app/core/models/project.models.ts`:
```ts
export interface Measurement {
  id: number;
  name: string;
  unit: string;
  subProgramIds: number[];
  subProgramNames: string[];
}

export interface CreateMeasurement {
  name: string;
  unit: string;
  subProgramIds: number[];
}

export type UpdateMeasurement = CreateMeasurement;

export interface SubProjectMeasurementValue {
  measurementId: number;
  measurementName: string;
  unit: string;
  value: number | null;
}

export interface SetMeasurementValue {
  measurementId: number;
  value: number | null;
}
```

- [ ] **Step 2: Create `MeasurementsService`**

`Frontend/src/app/core/services/measurements.service.ts`:
```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateMeasurement, Measurement, SetMeasurementValue, SubProjectMeasurementValue, UpdateMeasurement } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class MeasurementsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/measurements`;

  getAll(): Observable<Measurement[]> {
    return this.http.get<Measurement[]>(this.base);
  }

  getApplicable(subProgramId: number): Observable<Measurement[]> {
    return this.http.get<Measurement[]>(`${this.base}/applicable`, { params: { subProgramId } });
  }

  create(dto: CreateMeasurement): Observable<Measurement> {
    return this.http.post<Measurement>(this.base, dto);
  }

  update(id: number, dto: UpdateMeasurement): Observable<Measurement> {
    return this.http.put<Measurement>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  getValuesForSubProject(subProjectId: number): Observable<SubProjectMeasurementValue[]> {
    return this.http.get<SubProjectMeasurementValue[]>(`${environment.apiUrl}/subprojects/${subProjectId}/measurement-values`);
  }

  setValuesForSubProject(subProjectId: number, values: SetMeasurementValue[]): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/subprojects/${subProjectId}/measurement-values`, { values });
  }
}
```

- [ ] **Step 3: Create the measurements page component**

`Frontend/src/app/features/measurements/measurements.ts`:
```ts
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MeasurementsService } from '../../core/services/measurements.service';
import { LookupsService } from '../../core/services/lookups.service';
import { AuthService } from '../../core/services/auth.service';
import { CreateMeasurement, Measurement, SubProgramLookup } from '../../core/models/project.models';

@Component({
  selector: 'app-measurements',
  imports: [FormsModule],
  templateUrl: './measurements.html',
  styleUrl: './measurements.css',
})
export class Measurements {
  private readonly measurementsService = inject(MeasurementsService);
  private readonly lookups = inject(LookupsService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly measurements = signal<Measurement[]>([]);
  protected readonly subPrograms = signal<SubProgramLookup[]>([]);
  protected readonly search = signal('');

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) return this.measurements();
    return this.measurements().filter((m) => m.name.toLowerCase().includes(term));
  });

  protected readonly showForm = signal(false);
  protected readonly editing = signal<Measurement | null>(null);
  protected readonly fName = signal('');
  protected readonly fUnit = signal('');
  protected readonly fSubProgramIds = signal<Set<number>>(new Set());
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  constructor() {
    this.load();
    this.lookups.getSubPrograms().subscribe({ next: (list) => this.subPrograms.set(list) });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.measurementsService.getAll().subscribe({
      next: (data) => {
        this.measurements.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل القياسات');
        this.loading.set(false);
      },
    });
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.fName.set('');
    this.fUnit.set('');
    this.fSubProgramIds.set(new Set());
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected openEdit(m: Measurement): void {
    this.editing.set(m);
    this.fName.set(m.name);
    this.fUnit.set(m.unit);
    this.fSubProgramIds.set(new Set(m.subProgramIds));
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
  }

  protected toggleSubProgram(id: number): void {
    this.fSubProgramIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  protected submitForm(): void {
    if (this.saving()) return;
    this.formError.set(null);

    if (!this.fName().trim()) {
      this.formError.set('اسم القياس مطلوب');
      return;
    }
    if (!this.fUnit().trim()) {
      this.formError.set('وحدة القياس مطلوبة');
      return;
    }

    const dto: CreateMeasurement = {
      name: this.fName().trim(),
      unit: this.fUnit().trim(),
      subProgramIds: [...this.fSubProgramIds()],
    };

    this.saving.set(true);
    const editing = this.editing();
    const req = editing
      ? this.measurementsService.update(editing.id, dto)
      : this.measurementsService.create(dto);

    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message ?? 'تعذّر حفظ القياس');
      },
    });
  }

  protected deleteMeasurement(m: Measurement): void {
    if (!confirm(`تأكيد حذف «${m.name}»؟`)) return;
    this.measurementsService.delete(m.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر حذف القياس'),
    });
  }
}
```

- [ ] **Step 4: Create the template + styles**

`Frontend/src/app/features/measurements/measurements.html`:
```html
<div class="page">
  <header class="page-head">
    <div>
      <h1>القياسات المخصصة</h1>
      <p>تعريف قياسات مثل الارتفاع أو المسافة وربطها بالبرامج الفرعية</p>
    </div>
    @if (isManager()) {
      <button class="si-btn gold" (click)="openAdd()">
        <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12h14" /></svg>
        إضافة قياس جديد
      </button>
    }
  </header>

  <div class="toolbar">
    <div class="search">
      <svg viewBox="0 0 24 24" width="17" fill="none" stroke="currentColor" stroke-width="1.9"><circle cx="11" cy="11" r="7" /><path d="m21 21-4.3-4.3" /></svg>
      <input placeholder="البحث بالاسم…" [ngModel]="search()" (ngModelChange)="search.set($event)" />
    </div>
  </div>

  @if (loading()) {
    <div class="state"><span class="spinner"></span> جاري تحميل القياسات…</div>
  } @else if (error()) {
    <div class="state error">{{ error() }} <button class="si-btn" (click)="load()">إعادة المحاولة</button></div>
  } @else if (filtered().length === 0) {
    <div class="state">لا توجد قياسات مطابقة.</div>
  } @else {
    <div class="card">
      <div class="tbl-wrap">
        <table>
          <thead>
            <tr>
              <th>اسم القياس</th>
              <th>الوحدة</th>
              <th>البرامج الفرعية المرتبطة</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            @for (m of filtered(); track m.id) {
              <tr>
                <td><b>{{ m.name }}</b></td>
                <td>{{ m.unit }}</td>
                <td>
                  @if (m.subProgramNames.length === 0) {
                    <span class="muted">غير مرتبط</span>
                  } @else {
                    <div class="chips">
                      @for (name of m.subProgramNames; track name) { <span class="chip">{{ name }}</span> }
                    </div>
                  }
                </td>
                <td>
                  @if (isManager()) {
                    <div class="acts">
                      <button class="act" title="تعديل" (click)="openEdit(m)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" /></svg></button>
                      <button class="act danger" title="حذف" (click)="deleteMeasurement(m)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 7h16M9 7V5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2m3 0-1 13a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 7" /></svg></button>
                    </div>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  }

  @if (showForm()) {
    <div class="si-overlay" (click)="closeForm()">
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(560px,100%)">
        <div class="si-modal-head">
          <div class="grow"><h3>{{ editing() ? 'تعديل قياس' : 'إضافة قياس جديد' }}</h3></div>
          <button class="si-x" (click)="closeForm()" aria-label="إغلاق">×</button>
        </div>
        <div class="si-modal-body">
          @if (formError()) { <div class="si-err">{{ formError() }}</div> }
          <div class="si-grid">
            <div class="si-fld"><label>اسم القياس <span class="req">*</span></label><input [ngModel]="fName()" (ngModelChange)="fName.set($event)" placeholder="مثال: الارتفاع" /></div>
            <div class="si-fld"><label>الوحدة <span class="req">*</span></label><input [ngModel]="fUnit()" (ngModelChange)="fUnit.set($event)" placeholder="مثال: متر" /></div>
            <div class="si-fld full">
              <label>البرامج الفرعية المرتبطة</label>
              <div class="picker">
                @for (sp of subPrograms(); track sp.id) {
                  <label class="pick-chk">
                    <input type="checkbox" [checked]="fSubProgramIds().has(sp.id)" (change)="toggleSubProgram(sp.id)" />
                    {{ sp.name }}
                  </label>
                }
              </div>
            </div>
          </div>
        </div>
        <div class="si-modal-foot">
          <button class="si-btn primary" [disabled]="saving()" (click)="submitForm()">
            @if (saving()) { جاري الحفظ… } @else { {{ editing() ? 'حفظ التعديلات' : 'إضافة القياس' }} }
          </button>
          <button class="si-btn" (click)="closeForm()">إلغاء</button>
        </div>
      </div>
    </div>
  }
</div>
```

`Frontend/src/app/features/measurements/measurements.css`:
```css
.page { padding: 24px 28px; }
.page-head { display: flex; align-items: center; gap: 14px; margin-bottom: 20px; }
.page-head h1 { font-size: 22px; }
.page-head p { margin: 2px 0 0; color: var(--muted); font-size: 13px; }
.page-head .si-btn { margin-inline-start: auto; }

.toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; }
.search { flex: 1; min-width: 220px; display: flex; align-items: center; gap: 9px; background: var(--surface); border: 1px solid var(--line); border-radius: 11px; padding: 11px 13px; color: var(--muted); box-shadow: var(--shadow); }
.search input { border: 0; background: transparent; flex: 1; font-family: inherit; font-size: 13.5px; color: var(--ink); outline: none; }

.state { display: flex; align-items: center; justify-content: center; gap: 12px; background: var(--surface); border: 1px dashed var(--line-strong); border-radius: var(--radius); padding: 40px; color: var(--muted); box-shadow: var(--shadow); flex-wrap: wrap; }
.state.error { color: #b32a39; border-color: #F3C6CC; background: var(--bad-bg); }
.spinner { width: 16px; height: 16px; border: 2px solid var(--line-strong); border-top-color: var(--green-700); border-radius: 50%; animation: spin .7s linear infinite; display: inline-block; }
@keyframes spin { to { transform: rotate(360deg); } }

.card { background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius); box-shadow: var(--shadow); overflow: hidden; }
.tbl-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; min-width: 600px; }
th { font-size: 11.5px; color: var(--muted); font-weight: 700; text-align: start; padding: 12px 14px; border-bottom: 1px solid var(--line); white-space: nowrap; background: var(--surface-2); }
td { padding: 12px 14px; border-bottom: 1px solid var(--line); font-size: 13px; }

.chips { display: flex; flex-wrap: wrap; gap: 6px; }
.chip { display: inline-block; padding: 3px 10px; border-radius: 999px; background: var(--surface-2); border: 1px solid var(--line); font-size: 11.5px; font-weight: 700; }
.muted { color: var(--muted); font-size: 12.5px; }

.picker { display: flex; flex-direction: column; gap: 8px; max-height: 200px; overflow-y: auto; border: 1px solid var(--line); border-radius: 9px; padding: 10px; }
.pick-chk { display: flex; align-items: center; gap: 8px; font-size: 13px; }

.acts { display: inline-flex; gap: 6px; }
.act { width: 32px; height: 32px; border-radius: 8px; border: 1px solid var(--line); background: var(--surface); display: inline-grid; place-items: center; color: var(--muted); }
.act svg { width: 15px; height: 15px; }
.act.danger { color: var(--bad); border-color: #F3C6CC; }
.act.danger:hover { background: var(--bad-bg); }
```

- [ ] **Step 5: Wire the route and nav**

In `Frontend/src/app/app.routes.ts`, add inside the `app` route's `children`:
```ts
      {
        path: 'measurements',
        loadComponent: () =>
          import('./features/measurements/measurements').then((m) => m.Measurements),
      },
```

In `Frontend/src/app/layout/main-layout/main-layout.ts`'s `allNav` array, add (all-staff visible):
```ts
    { label: 'القياسات', route: '/app/measurements', icon: 'M4 4h16v4H4V4Zm0 8h10v4H4v-4Zm0 8h7v-4H4v4', managerOnly: false },
```

- [ ] **Step 6: Type-check**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 7: Manual check in the browser**

Navigate to `/app/measurements`. Create a measurement (e.g. "الارتفاع" / "متر"), linking it to a real sub-program via the checkbox picker. Confirm it appears in the table with the sub-program name as a chip. Edit it to unlink from that sub-program, confirm the chip disappears and it now shows "غير مرتبط". Attempt delete on one with no values/links — should succeed.

- [ ] **Step 8: Commit**

```bash
git add Frontend/src/app/core/models/project.models.ts Frontend/src/app/core/services/measurements.service.ts Frontend/src/app/features/measurements/ Frontend/src/app/app.routes.ts Frontend/src/app/layout/main-layout/main-layout.ts
git commit -m "feat: add custom measurements management page"
```

---

### Task 9: Frontend — Record Measurement Values on the Sub-Project Form (Step 4)

**Files:**
- Modify: `Frontend/src/app/features/projects/sub-project-form.ts`

**Interfaces:**
- Consumes: `MeasurementsService.getApplicable(subProgramId)`/`getValuesForSubProject(subProjectId)`/`setValuesForSubProject(subProjectId, values)` (Task 8), `MainProjectListItem.subProgramId` (already exists on the model).

- [ ] **Step 1: Add Step 4 to the sub-project form**

Replace `Frontend/src/app/features/projects/sub-project-form.ts` in full — this is Task 7's version with a new Step 4 section added to the template, plus the supporting signals/logic:

```ts
import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ProjectsService } from '../../core/services/projects.service';
import { LookupsService } from '../../core/services/lookups.service';
import { FinancialYearsService } from '../../core/services/financial-years.service';
import { MeasurementsService } from '../../core/services/measurements.service';
import { AuthService } from '../../core/services/auth.service';
import {
  FinancialYear,
  Lookup,
  MainProjectListItem,
  MarkazLookup,
  SubProjectListItem,
  SubProjectMeasurementValue,
} from '../../core/models/project.models';

export interface LockedParent {
  id: number;
  code: string | null;
  name: string;
}

@Component({
  selector: 'app-sub-project-form',
  imports: [FormsModule],
  template: `
    @if (open()) {
      <div class="si-overlay" (click)="close.emit()">
        <div class="si-modal" (click)="$event.stopPropagation()">
          <div class="si-modal-head">
            <div class="grow">
              <h3>{{ edit() ? 'تعديل مشروع فرعي' : 'إضافة مشروع فرعي' }}</h3>
              <p>{{ edit() ? 'إدخال الكود يعتمد المشروع تلقائيًا، وإزالته يعيده لمقترح' : 'يُنشأ المشروع كمقترح ما لم يُدخَل كود' }}</p>
            </div>
            <button class="si-x" (click)="close.emit()" aria-label="إغلاق">×</button>
          </div>

          <div class="si-modal-body">
            @if (error()) { <div class="si-err">{{ error() }}</div> }

            <!-- المشروع الرئيسي -->
            @if (locked()) {
              <div class="si-locked">
                <div class="lh">
                  <div><b>{{ locked()!.name }}</b><div class="lc">الكود: {{ locked()!.code ?? 'بانتظار الاعتماد' }}</div></div>
                  <span class="lb">🔒 المشروع الرئيسي التابع له</span>
                </div>
              </div>
            } @else {
              <div class="si-grid">
                <div class="si-fld full">
                  <label>المشروع الرئيسي <span class="req">*</span></label>
                  <select [ngModel]="mainProjectId()" (ngModelChange)="mainProjectId.set($event)" [disabled]="!!edit()">
                    <option [ngValue]="null">— اختر المشروع الرئيسي —</option>
                    @for (m of mains(); track m.id) { <option [ngValue]="m.id">{{ m.code }} — {{ m.name }}</option> }
                  </select>
                </div>
              </div>
            }

            <div class="si-step"><span class="n">2</span><h4>بيانات المشروع الفرعي</h4></div>
            <div class="si-grid">
              <div class="si-fld full">
                <label>اسم المشروع الفرعي <span class="req">*</span></label>
                <input [ngModel]="name()" (ngModelChange)="name.set($event)" placeholder="مثال: رصف طريق المحطة" />
              </div>
              <div class="si-fld full">
                <label>كود المشروع الفرعي (اختياري)</label>
                <input [ngModel]="code()" (ngModelChange)="code.set($event)" placeholder="SP-2627-XXX-A" />
                <div class="hint">إدخال كود يعتمد المشروع تلقائيًا فور الحفظ؛ تركه فارغًا يبقيه مقترحًا.</div>
              </div>
              <div class="si-fld">
                <label>المستوى <span class="req">*</span></label>
                <select [ngModel]="projectLevelId()" (ngModelChange)="projectLevelId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (pl of projectLevels(); track pl.id) { <option [ngValue]="pl.id">{{ pl.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>المكوّن العيني <span class="req">*</span></label>
                <select [ngModel]="componentTypeId()" (ngModelChange)="componentTypeId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (c of componentTypes(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>الوحدة الحسابية <span class="req">*</span></label>
                <select [ngModel]="accountingUnitId()" (ngModelChange)="accountingUnitId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (a of accountingUnits(); track a.id) { <option [ngValue]="a.id">{{ a.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>المركز <span class="req">*</span></label>
                <select [ngModel]="markazId()" (ngModelChange)="markazId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (mk of markazList(); track mk.id) { <option [ngValue]="mk.id">{{ mk.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>الأولوية <span class="req">*</span></label>
                <select [ngModel]="priorityId()" (ngModelChange)="priorityId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (p of priorities(); track p.id) { <option [ngValue]="p.id">{{ p.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>حالة المشروع <span class="req">*</span></label>
                <select [ngModel]="statusId()" (ngModelChange)="statusId.set($event)">
                  <option [ngValue]="null">— اختر —</option>
                  @for (st of statuses(); track st.id) { <option [ngValue]="st.id">{{ st.name }}</option> }
                </select>
              </div>
              <div class="si-fld">
                <label>تمويل بنكي (ج.م)</label>
                <input type="number" [ngModel]="bankFunding()" (ngModelChange)="bankFunding.set($event)" placeholder="0" />
              </div>
              <div class="si-fld">
                <label>تمويل ذاتي (ج.م)</label>
                <input type="number" [ngModel]="selfFunding()" (ngModelChange)="selfFunding.set($event)" placeholder="0" />
              </div>
              <div class="si-fld full">
                <label>ملاحظات / وصف</label>
                <textarea [ngModel]="description()" (ngModelChange)="description.set($event)" placeholder="أي ملاحظات إضافية…"></textarea>
              </div>
            </div>

            <div class="si-step"><span class="n">3</span><h4>السنوات المالية</h4></div>
            <div class="si-years">
              @for (y of financialYears(); track y.id) {
                <label class="si-year-chk">
                  <input type="checkbox" [checked]="checkedYearIds().has(y.id)" (change)="toggleYear(y.id)" />
                  {{ y.name }}
                </label>
              } @empty {
                <p class="hint">لا توجد سنوات مالية بعد.</p>
              }
            </div>

            @if (applicableMeasurements().length > 0) {
              <div class="si-step"><span class="n">4</span><h4>القياسات المخصصة</h4></div>
              <div class="si-grid">
                @for (m of applicableMeasurements(); track m.id) {
                  <div class="si-fld">
                    <label>{{ m.name }} ({{ m.unit }})</label>
                    <input
                      type="number"
                      [ngModel]="measurementValues()[m.id] ?? null"
                      (ngModelChange)="setMeasurementValue(m.id, $event)"
                      placeholder="اختياري"
                    />
                  </div>
                }
              </div>
            }
          </div>

          <div class="si-modal-foot">
            <button class="si-btn primary" [disabled]="saving()" (click)="submit()">
              @if (saving()) { <span class="mini-sp"></span> جاري الحفظ… } @else { {{ edit() ? 'حفظ التعديلات' : 'إضافة المشروع الفرعي' }} }
            </button>
            @if (edit() && isManager()) {
              <button class="si-btn danger" type="button" [disabled]="saving()" (click)="onDelete()">حذف المشروع</button>
            }
            <button class="si-btn" (click)="close.emit()">إلغاء</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`.mini-sp{width:14px;height:14px;border:2px solid rgba(255,255,255,.4);border-top-color:#fff;border-radius:50%;animation:spin .7s linear infinite;display:inline-block}@keyframes spin{to{transform:rotate(360deg)}}.si-years{display:flex;flex-wrap:wrap;gap:10px;margin-bottom:16px}.si-year-chk{display:flex;align-items:center;gap:7px;border:1px solid var(--line-strong);border-radius:9px;padding:8px 12px;font-size:13px;font-weight:700;background:var(--surface)}.si-years .hint{font-size:12px;color:var(--muted)}`],
})
export class SubProjectForm {
  private readonly projectsService = inject(ProjectsService);
  private readonly lookups = inject(LookupsService);
  private readonly financialYearsService = inject(FinancialYearsService);
  private readonly measurementsService = inject(MeasurementsService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  readonly open = input(false);
  readonly edit = input<SubProjectListItem | null>(null);
  readonly locked = input<LockedParent | null>(null);
  readonly mains = input<MainProjectListItem[]>([]);
  readonly defaultYearId = input<number | null>(null);
  readonly close = output<void>();
  readonly saved = output<void>();
  readonly delete = output<void>();

  protected readonly priorities = signal<Lookup[]>([]);
  protected readonly statuses = signal<Lookup[]>([]);
  protected readonly markazList = signal<MarkazLookup[]>([]);
  protected readonly projectLevels = signal<Lookup[]>([]);
  protected readonly componentTypes = signal<Lookup[]>([]);
  protected readonly accountingUnits = signal<Lookup[]>([]);

  protected readonly financialYears = signal<FinancialYear[]>([]);
  protected readonly checkedYearIds = signal<Set<number>>(new Set());
  private originalYearIds = new Set<number>();

  protected readonly mainProjectId = signal<number | null>(null);
  protected readonly code = signal('');
  protected readonly name = signal('');
  protected readonly projectLevelId = signal<number | null>(null);
  protected readonly componentTypeId = signal<number | null>(null);
  protected readonly accountingUnitId = signal<number | null>(null);
  protected readonly markazId = signal<number | null>(null);
  protected readonly priorityId = signal<number | null>(null);
  protected readonly statusId = signal<number | null>(null);
  protected readonly bankFunding = signal<number>(0);
  protected readonly selfFunding = signal<number>(0);
  protected readonly description = signal('');

  protected readonly applicableMeasurements = signal<{ id: number; name: string; unit: string }[]>([]);
  protected readonly measurementValues = signal<Record<number, number | null>>({});

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  private lookupsLoaded = false;
  private wasOpen = false;

  constructor() {
    effect(() => {
      const isOpen = this.open();
      if (isOpen && !this.wasOpen) {
        this.wasOpen = true;
        this.onOpen();
      } else if (!isOpen) {
        this.wasOpen = false;
      }
    });
  }

  private onOpen(): void {
    this.error.set(null);
    this.ensureLookups(() => this.prefill());
  }

  private ensureLookups(done: () => void): void {
    if (this.lookupsLoaded) {
      done();
      return;
    }
    forkJoin({
      priorities: this.lookups.getPriorities(),
      statuses: this.lookups.getStatuses(),
      markaz: this.lookups.getMarkaz(),
      projectLevels: this.lookups.getProjectLevels(),
      componentTypes: this.lookups.getComponentTypes(),
      accountingUnits: this.lookups.getAccountingUnits(),
      financialYears: this.financialYearsService.getAll(),
    }).subscribe({
      next: ({ priorities, statuses, markaz, projectLevels, componentTypes, accountingUnits, financialYears }) => {
        this.priorities.set(priorities);
        this.statuses.set(statuses);
        this.markazList.set(markaz);
        this.projectLevels.set(projectLevels);
        this.componentTypes.set(componentTypes);
        this.accountingUnits.set(accountingUnits);
        this.financialYears.set(financialYears);
        this.lookupsLoaded = true;
        done();
      },
      error: () => this.error.set('تعذّر تحميل القوائم'),
    });
  }

  private prefill(): void {
    this.resetForm();
    const e = this.edit();
    const lockedParent = this.locked();

    if (lockedParent) {
      this.mainProjectId.set(lockedParent.id);
      this.loadApplicableMeasurements(lockedParent.id);
    }

    if (e) {
      // جلب التفاصيل الكاملة للتعديل
      this.projectsService.getSubProject(e.id).subscribe({
        next: (d) => {
          this.mainProjectId.set(d.mainProjectId);
          this.code.set(d.code ?? '');
          this.name.set(d.name);
          this.projectLevelId.set(d.projectLevelId);
          this.componentTypeId.set(d.componentTypeId);
          this.accountingUnitId.set(d.accountingUnitId);
          this.markazId.set(d.markazId);
          this.priorityId.set(d.priorityId);
          this.statusId.set(d.statusId);
          this.bankFunding.set(d.bankFunding);
          this.selfFunding.set(d.selfFunding);
          this.description.set(d.description ?? '');
          this.loadApplicableMeasurements(d.mainProjectId, e.id);
        },
        error: () => this.error.set('تعذّر تحميل بيانات المشروع الفرعي'),
      });

      this.projectsService.getSubProjectFinancialYears(e.id).subscribe({
        next: (links) => {
          const ids = new Set(links.map((l) => l.financialYearId));
          this.originalYearIds = ids;
          this.checkedYearIds.set(new Set(ids));
        },
        error: () => this.error.set('تعذّر تحميل السنوات المالية المرتبطة بهذا المشروع'),
      });
    } else {
      this.originalYearIds = new Set<number>();
      const defaultId = this.defaultYearId();
      this.checkedYearIds.set(defaultId != null ? new Set([defaultId]) : new Set<number>());
    }
  }

  private loadApplicableMeasurements(mainProjectId: number, subProjectId?: number): void {
    const mainProject = this.mains().find((m) => m.id === mainProjectId);
    if (!mainProject) {
      this.applicableMeasurements.set([]);
      this.measurementValues.set({});
      return;
    }

    this.measurementsService.getApplicable(mainProject.subProgramId).subscribe({
      next: (measurements) => {
        this.applicableMeasurements.set(measurements.map((m) => ({ id: m.id, name: m.name, unit: m.unit })));

        if (subProjectId != null) {
          this.measurementsService.getValuesForSubProject(subProjectId).subscribe({
            next: (values: SubProjectMeasurementValue[]) => {
              const map: Record<number, number | null> = {};
              for (const v of values) {
                map[v.measurementId] = v.value;
              }
              this.measurementValues.set(map);
            },
            error: () => {},
          });
        } else {
          this.measurementValues.set({});
        }
      },
      error: () => this.applicableMeasurements.set([]),
    });
  }

  protected setMeasurementValue(measurementId: number, value: number | null): void {
    this.measurementValues.update((current) => ({ ...current, [measurementId]: value }));
  }

  private resetForm(): void {
    this.mainProjectId.set(null);
    this.code.set('');
    this.name.set('');
    this.projectLevelId.set(null);
    this.componentTypeId.set(null);
    this.accountingUnitId.set(null);
    this.markazId.set(null);
    this.priorityId.set(null);
    this.statusId.set(null);
    this.bankFunding.set(0);
    this.selfFunding.set(0);
    this.description.set('');
    this.checkedYearIds.set(new Set());
    this.originalYearIds = new Set<number>();
    this.applicableMeasurements.set([]);
    this.measurementValues.set({});
  }

  protected onDelete(): void {
    this.delete.emit();
  }

  protected toggleYear(id: number): void {
    this.checkedYearIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected submit(): void {
    if (this.saving()) return;
    this.error.set(null);

    if (!this.name().trim()) { this.error.set('برجاء إدخال اسم المشروع الفرعي'); return; }
    if (this.projectLevelId() == null) { this.error.set('برجاء اختيار المستوى'); return; }
    if (this.componentTypeId() == null) { this.error.set('برجاء اختيار المكوّن العيني'); return; }
    if (this.accountingUnitId() == null) { this.error.set('برجاء اختيار الوحدة الحسابية'); return; }
    if (this.markazId() == null) { this.error.set('برجاء اختيار المركز'); return; }
    if (this.priorityId() == null) { this.error.set('برجاء اختيار الأولوية'); return; }
    if (this.statusId() == null) { this.error.set('برجاء اختيار حالة المشروع'); return; }
    if (!this.edit() && this.mainProjectId() == null) { this.error.set('برجاء اختيار المشروع الرئيسي'); return; }

    const base = {
      code: this.code().trim() || null,
      name: this.name().trim(),
      projectLevelId: this.projectLevelId()!,
      componentTypeId: this.componentTypeId()!,
      accountingUnitId: this.accountingUnitId()!,
      projectNature: '',
      markazId: this.markazId()!,
      priorityId: this.priorityId()!,
      statusId: this.statusId()!,
      bankFunding: Number(this.bankFunding()) || 0,
      selfFunding: Number(this.selfFunding()) || 0,
      latitude: null,
      longitude: null,
      description: this.description().trim() || null,
    };

    this.saving.set(true);
    const editing = this.edit();
    const req = editing
      ? this.projectsService.updateSubProject(editing.id, base)
      : this.projectsService.createSubProject({ ...base, mainProjectId: this.mainProjectId()! });

    req.subscribe({
      next: (result) => {
        const subProjectId = editing ? editing.id : result.id;
        this.syncFinancialYears(subProjectId);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر حفظ المشروع الفرعي');
      },
    });
  }

  private syncFinancialYears(subProjectId: number): void {
    const desired = this.checkedYearIds();
    const toLink = [...desired].filter((id) => !this.originalYearIds.has(id));
    const toUnlink = [...this.originalYearIds].filter((id) => !desired.has(id));
    const calls = [
      ...toLink.map((id) => this.projectsService.linkFinancialYear(subProjectId, id)),
      ...toUnlink.map((id) => this.projectsService.unlinkFinancialYear(subProjectId, id)),
    ];

    if (calls.length === 0) {
      this.syncMeasurementValues(subProjectId);
      return;
    }

    forkJoin(calls).subscribe({
      next: () => this.syncMeasurementValues(subProjectId),
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر تحديث ربط السنوات المالية');
      },
    });
  }

  private syncMeasurementValues(subProjectId: number): void {
    const applicable = this.applicableMeasurements();
    if (applicable.length === 0) {
      this.saving.set(false);
      this.saved.emit();
      return;
    }

    const values = this.measurementValues();
    const payload = applicable.map((m) => ({ measurementId: m.id, value: values[m.id] ?? null }));

    this.measurementsService.setValuesForSubProject(subProjectId, payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'تعذّر حفظ القياسات');
      },
    });
  }
}
```

Key additions over Task 7's version: `applicableMeasurements`/`measurementValues` signals; `loadApplicableMeasurements()` (called both when a locked parent is set and after an edit's detail fetch resolves the Main Project, resolving `subProgramId` via `mains()` — the same array already passed into this component — then fetching applicable measurements and, for edits, existing recorded values); a new optional Step 4 section in the template rendered only when `applicableMeasurements().length > 0`; `submit()`'s save chain extended from `syncFinancialYears` → `syncMeasurementValues` (mirrors the existing "create/update parent, then attach children via follow-up calls" pattern already used for financial years, so measurement values save in the same round-trip from the staff member's perspective).

**Note:** for a brand-new sub-project (no `locked()` parent, no `edit()`), `applicableMeasurements` only populates once `mainProjectId()` is chosen from the dropdown — this task doesn't wire a live `effect()` on `mainProjectId()` changes for the non-locked, non-edit path (selecting a different Main Project from the dropdown after the modal is already open won't refresh Step 4). This is an acceptable gap for this task: the common paths (adding a sub-project under an already-locked Main Project, or editing an existing one) both work correctly; add a `mainProjectId` change `effect()` calling `loadApplicableMeasurements` as a follow-up if this proves to matter in practice.

- [ ] **Step 2: Type-check**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 3: Manual check in the browser**

Create a measurement in `/app/measurements` linked to a real sub-program (if not already done in Task 8's manual check, do it now). Open the sub-project form for a **locked** parent (e.g. via "إضافة مشروع فرعي" from within a Main Project belonging to that sub-program) — confirm Step 4 appears with that measurement's input. Enter a value, save, re-open the same sub-project for editing — confirm the value is pre-filled. Open the form for a sub-project under a *different* sub-program with no linked measurements — confirm Step 4 doesn't render at all.

- [ ] **Step 4: Commit**

```bash
git add Frontend/src/app/features/projects/sub-project-form.ts
git commit -m "feat: record custom measurement values as Step 4 of the sub-project form"
```

---

### Task 10: Final End-to-End Verification

**Files:** none (verification only).

- [ ] **Step 1: Full regression pass in the browser**

With `frontend-dev` and `backend-api` running:
1. Login as `superadmin`/`admin` still works.
2. `/app/settings`: all 11 tabs load real data. Create/edit/delete a Main Program and its Sub Program (confirm parent picker). Confirm deleting an in-use Priority/Status/Markaz/MainProgram/SubProgram/Governorate/ComponentType/ProjectLevel/AccountingUnit fails with a clear message.
3. `/app/measurements`: create a measurement, link/unlink sub-programs, confirm chips update.
4. `/app/projects`: open the sub-project add/edit form — confirm المستوى/المكوّن العيني/الوحدة الحسابية are populated from real API data (not hardcoded lists), and the المستوى filter dropdown on the page still narrows results.
5. Add a new sub-project under a Main Project whose Sub Program has a linked measurement — confirm Step 4 appears, record a value, save, re-open to confirm it's pre-filled.
6. Confirm the nav shows "الإعدادات" and "القياسات" for both a manager and a non-manager account (both all-staff visible), with add/edit/delete controls hidden for non-managers.

- [ ] **Step 2: Confirm no stray console errors**

Use `read_console_messages` (`onlyErrors: true`) during the pass above.

- [ ] **Step 3: Confirm existing data survived the ComponentType/ProjectLevel/AccountingUnit migration**

Pick 2-3 sub-projects that existed before this plan's work began. Confirm their `componentTypeName`/`projectLevelName` still show the same values they always displayed as free text (spot-check against what Task 3's manual check already recorded), and `accountingUnitName` shows `"غير محدد"` (since it's always been blank).

- [ ] **Step 4: Final `git status`**

```bash
git status
```
Confirm only files touched by Tasks 1-9 show as modified/new.

