# Settings Consolidation + Measurement Units Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold Contractors/Agencies/Users/Measurements into a routed `/app/settings` shell alongside the 12 generic lookup tabs, replace `Measurement`'s single fixed `Unit` string with a many-to-many `Unit` lookup chosen per recorded value, and turn the measurement modal's flat sub-program checkbox list into a Main-Program-grouped picker.

**Architecture:** Backend adds `Unit` as a 12th flat lookup on the existing `LookupService`/`ILookupService`/`LookupsController`, adds a `MeasurementUnit` join table (mirroring the existing `MeasurementSubProgram` join) and a `UnitId` FK on `SubProjectMeasurementValue`, then reworks the `Measurement` DTOs/mapping/repository/service to expose/consume unit lists instead of a single string. Frontend converts `/app/settings` from an in-memory tab-switching component into a routed shell (`Settings` shell + `SettingsLookupPage` generic child, reused across all 12 lookup tabs) with `users`/`contractors`/`agencies`/`measurements` as sibling child routes; the measurements page gains a unit multi-select and a Main-Program-grouped sub-program picker; the sub-project form's Step 4 gains a required per-row unit dropdown.

**Tech Stack:** .NET 10 (EF Core, AutoMapper), Angular 21 standalone components + Signals.

## Global Constraints

- No automated test suite exists anywhere in this repo. Each task's "test" step is build/type-check, then a manual check via the browser preview tool.
- Follow existing conventions exactly: Arabic UI strings, `si-btn`/`si-modal`/`si-overlay`/`si-grid`/`si-fld`/`si-err` shared classes from `Frontend/src/styles.css`, per-component CSS (default `ViewEncapsulation.Emulated`), Signals-based state, `[ngModel]`/`(ngModelChange)` (no Reactive Forms), `AuthService.isManager` for manager-gated mutation actions.
- Backend: class-level `[Authorize]` broad/none, method-level narrower/explicit. All new mutation actions use `[Authorize(Roles = Roles.PlanningManager)]`, matching the existing lookup-management convention.
- Domain entity files need no `using` statements — `Backend/src/SmartInvest.Domain/Common/global.cs` globally provides `System.ComponentModel.DataAnnotations`, `.Schema`, `SmartInvest.Domain.Entities`, `SmartInvest.Domain.Interfaces`, `SmartInvest.Domain.Enums`.
- Migrations: generate, inspect the raw `Up()`/`Down()`, apply, then run the empty-probe-migration technique (`dotnet ef migrations add ProbeCheck`, confirm both methods are empty, then `dotnet ef migrations remove`) to confirm the model snapshot matches.
- `npx tsc --noEmit -p tsconfig.app.json` does NOT type-check `.html` templateUrl files or catch template-binding errors against renamed/removed TS properties — only `npx ng build` does. Run `ng build` (not just `tsc --noEmit`) as the final check of every frontend task in this plan, since `Measurement.unit`/`SubProjectMeasurementValueDto.unit` (string fields) are being removed/renamed and old templates reference them directly.
- Never run dev servers via Bash — use the `preview_start` tool.
- **Known recurring issue:** a stray `SmartInvest.API.exe` process can hold the build output DLL locked. If `dotnet build` fails with a file-lock error, stop it first (`taskkill //F //IM SmartInvest.API.exe` via bash, or PowerShell `Get-Process -Name SmartInvest.API | Stop-Process -Force`), then rebuild.

---

### Task 1: Backend — New `Unit` Lookup

**Files:**
- Create: `Backend/src/SmartInvest.Domain/Entities/Unit.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs`
- Modify: `Backend/src/SmartInvest.Application/Common/Mappings/LookupMappingProfile.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/ILookupService.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/LookupService.cs`
- Modify: `Backend/src/SmartInvest.API/Controllers/LookupsController.cs`
- Create (generated): EF migration `AddUnitLookup`

**Interfaces:**
- Produces: `GET/POST/PUT/DELETE api/lookups/units`, reusing the existing `CreateNamedLookupDto`/`UpdateNamedLookupDto`/`LookupDto` (no new DTOs — `Unit` is a flat `{Id, Name}` lookup exactly like `ComponentType`/`ProjectLevel`/`AccountingUnit`).
- Consumes: nothing new — `IGenericRepository<Unit>` resolves automatically via the existing open-generic registration in `DependencyInjection.cs` (`services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));`), so no DI changes are needed.

- [ ] **Step 1: Create the `Unit` entity**

Create `Backend/src/SmartInvest.Domain/Entities/Unit.cs`:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class Unit
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 2: Register the `DbSet`**

In `Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs`, add a line right after the existing `Measurements` line:

```csharp
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<Unit> Units => Set<Unit>();
```

- [ ] **Step 3: Add the AutoMapper mapping**

In `Backend/src/SmartInvest.Application/Common/Mappings/LookupMappingProfile.cs`, add after the existing `CreateMap<AccountingUnit, LookupDto>();` line:

```csharp
        CreateMap<AccountingUnit, LookupDto>();

        CreateMap<Unit, LookupDto>();
```

- [ ] **Step 4: Extend `ILookupService`**

In `Backend/src/SmartInvest.Application/Interfaces/ILookupService.cs`, add after the `AccountingUnit` group (before the closing `}`):

```csharp
    Task<IReadOnlyList<LookupDto>> GetUnitsAsync(CancellationToken cancellationToken = default);
    Task<LookupDto> CreateUnitAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task<LookupDto> UpdateUnitAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default);
    Task DeleteUnitAsync(int id, CancellationToken cancellationToken = default);
```

- [ ] **Step 5: Implement in `LookupService`**

In `Backend/src/SmartInvest.Application/Services/LookupService.cs`, add a new field after `_accountingUnitRepository`:

```csharp
    private readonly IGenericRepository<AccountingUnit> _accountingUnitRepository;
    private readonly IGenericRepository<Unit> _unitRepository;
```

Add the matching constructor parameter (after `accountingUnitRepository`) and assignment:

```csharp
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
        IGenericRepository<ComponentType> componentTypeRepository,
        IGenericRepository<ProjectLevel> projectLevelRepository,
        IGenericRepository<AccountingUnit> accountingUnitRepository,
        IGenericRepository<Unit> unitRepository,
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
        _componentTypeRepository = componentTypeRepository;
        _projectLevelRepository = projectLevelRepository;
        _accountingUnitRepository = accountingUnitRepository;
        _unitRepository = unitRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }
```

Add the 4 methods at the end of the class, right before the final closing `}`:

```csharp
    public async Task<IReadOnlyList<LookupDto>> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _unitRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<List<LookupDto>>(items);
    }

    public async Task<LookupDto> CreateUnitAsync(CreateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var name = dto.Name.Trim();
        var duplicates = await _unitRepository.FindAsync(x => x.Name == name, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم الوحدة «{name}» مستخدم بالفعل");
        }

        var entity = new Unit { Name = name };
        await _unitRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task<LookupDto> UpdateUnitAsync(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _unitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الوحدة رقم {id} غير موجودة");

        var name = dto.Name.Trim();
        var duplicates = await _unitRepository.FindAsync(x => x.Name == name && x.Id != id, cancellationToken);
        if (duplicates.Count > 0)
        {
            throw new BusinessRuleException($"اسم الوحدة «{name}» مستخدم بالفعل");
        }

        entity.Name = name;
        _unitRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<LookupDto>(entity);
    }

    public async Task DeleteUnitAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الوحدة رقم {id} غير موجودة");

        _unitRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
```

Note: `DeleteUnitAsync` has no dependent-usage guard yet — `MeasurementUnit` (the join table that will reference `Unit`) doesn't exist until Task 2, which will extend this method with the guard once it does.

- [ ] **Step 6: Add controller routes**

In `Backend/src/SmartInvest.API/Controllers/LookupsController.cs`, add at the end of the class, right before the final closing `}`:

```csharp
    [HttpGet("units")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> GetUnits(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetUnitsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("units")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> CreateUnit(CreateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.CreateUnitAsync(dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("units/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<LookupDto>> UpdateUnit(int id, UpdateNamedLookupDto dto, CancellationToken cancellationToken)
    {
        var result = await _lookupService.UpdateUnitAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("units/{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> DeleteUnit(int id, CancellationToken cancellationToken)
    {
        await _lookupService.DeleteUnitAsync(id, cancellationToken);
        return NoContent();
    }
```

- [ ] **Step 7: Build the backend**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 8: Generate and apply the migration**

```bash
cd Backend/src/SmartInvest.API
dotnet ef migrations add AddUnitLookup --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

Inspect the generated migration: expect one `CreateTable` call for `Units` (Id identity PK + `Name nvarchar(max) not null`).

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

Confirm `GET /api/lookups/units` returns `[]`. Create a unit named `متر`, confirm it appears; confirm editing and deleting it work.

- [ ] **Step 11: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Entities/Unit.cs Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs Backend/src/SmartInvest.Application/Common/Mappings/LookupMappingProfile.cs Backend/src/SmartInvest.Application/Interfaces/ILookupService.cs Backend/src/SmartInvest.Application/Services/LookupService.cs Backend/src/SmartInvest.API/Controllers/LookupsController.cs Backend/src/SmartInvest.Infrastructure/Migrations/
git commit -m "feat: add Unit as a 12th managed lookup

Standalone flat lookup for measurement units (متر، سنتيمتر، كيلومتر...),
deliberately separate from AccountingUnit (an unrelated per-sub-project
budget classification). Not yet referenced by anything — Task 2 wires
it into the Measurement model."
```

---

### Task 2: Backend — Measurement/Unit Model Rework

**Files:**
- Create: `Backend/src/SmartInvest.Domain/Entities/MeasurementUnit.cs`
- Modify: `Backend/src/SmartInvest.Domain/Entities/Measurement.cs`
- Modify: `Backend/src/SmartInvest.Domain/Entities/SubProjectMeasurementValue.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/MeasurementDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Common/Mappings/MeasurementMappingProfile.cs`
- Modify: `Backend/src/SmartInvest.Domain/Interfaces/IMeasurementRepository.cs` (doc-only — no signature changes)
- Modify: `Backend/src/SmartInvest.Infrastructure/Repositories/MeasurementRepository.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/MeasurementService.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/LookupService.cs` (extend `DeleteUnitAsync` with a dependent-usage guard)
- Create (generated): EF migration `AddMeasurementUnits`

**Interfaces:**
- Consumes: `Unit` entity/table (Task 1).
- Produces: `MeasurementDto`/`CreateMeasurementDto`/`UpdateMeasurementDto` gain `UnitIds: List<int>` / `UnitNames: List<string>` (write/read respectively), replacing the old single `Unit: string`. `SubProjectMeasurementValueDto` gains nullable `UnitId: int?`/`UnitName: string?` replacing `Unit: string`. `SetMeasurementValueDto` gains nullable `UnitId: int?`. Task 3 (frontend models) consumes this exact shape.

- [ ] **Step 1: Create the `MeasurementUnit` join entity**

Create `Backend/src/SmartInvest.Domain/Entities/MeasurementUnit.cs`, mirroring `MeasurementSubProgram.cs` exactly:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class MeasurementUnit
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Measurement")]
        public int MeasurementId { get; set; }
        public virtual Measurement Measurement { get; set; }

        [ForeignKey("Unit")]
        public int UnitId { get; set; }
        public virtual Unit Unit { get; set; }
    }
}
```

- [ ] **Step 2: Update the `Measurement` entity**

Replace `Backend/src/SmartInvest.Domain/Entities/Measurement.cs` in full:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class Measurement
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public virtual ICollection<MeasurementSubProgram> MeasurementSubPrograms { get; set; }
        public virtual ICollection<MeasurementUnit> MeasurementUnits { get; set; }
        public virtual ICollection<SubProjectMeasurementValue> Values { get; set; }
    }
}
```

- [ ] **Step 3: Update `SubProjectMeasurementValue`**

Replace `Backend/src/SmartInvest.Domain/Entities/SubProjectMeasurementValue.cs` in full:

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

        [ForeignKey("Unit")]
        public int UnitId { get; set; }
        public virtual Unit Unit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; }
    }
}
```

- [ ] **Step 4: Rework `MeasurementDtos.cs`**

Replace `Backend/src/SmartInvest.Application/DTOs/MeasurementDtos.cs` in full:

```csharp
namespace SmartInvest.Application.DTOs;

public class MeasurementDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<int> SubProgramIds { get; set; } = new();
    public List<string> SubProgramNames { get; set; } = new();
    public List<int> UnitIds { get; set; } = new();
    public List<string> UnitNames { get; set; } = new();
}

public class CreateMeasurementDto
{
    public string Name { get; set; } = string.Empty;
    public List<int> SubProgramIds { get; set; } = new();
    public List<int> UnitIds { get; set; } = new();
}

public class UpdateMeasurementDto
{
    public string Name { get; set; } = string.Empty;
    public List<int> SubProgramIds { get; set; } = new();
    public List<int> UnitIds { get; set; } = new();
}

public class SubProjectMeasurementValueDto
{
    public int MeasurementId { get; set; }
    public string MeasurementName { get; set; } = string.Empty;
    public int? UnitId { get; set; }
    public string? UnitName { get; set; }
    public decimal? Value { get; set; }
}

public class SetMeasurementValueDto
{
    public int MeasurementId { get; set; }
    public int? UnitId { get; set; }
    public decimal? Value { get; set; }
}

public class SetSubProjectMeasurementValuesDto
{
    public List<SetMeasurementValueDto> Values { get; set; } = new();
}
```

- [ ] **Step 5: Update the mapping profile**

Replace `Backend/src/SmartInvest.Application/Common/Mappings/MeasurementMappingProfile.cs` in full:

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
                opt => opt.MapFrom(src => src.MeasurementSubPrograms.Select(x => x.SubProgram.SubProgramName).ToList()))
            .ForMember(
                dest => dest.UnitIds,
                opt => opt.MapFrom(src => src.MeasurementUnits.Select(x => x.UnitId).ToList()))
            .ForMember(
                dest => dest.UnitNames,
                opt => opt.MapFrom(src => src.MeasurementUnits.Select(x => x.Unit.Name).ToList()));
    }
}
```

- [ ] **Step 6: Extend the repository's `Include` chains**

Replace `Backend/src/SmartInvest.Infrastructure/Repositories/MeasurementRepository.cs` in full:

```csharp
using Microsoft.EntityFrameworkCore;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Repositories;

public class MeasurementRepository : GenericRepository<Measurement>, IMeasurementRepository
{
    public MeasurementRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Measurement>> GetAllWithSubProgramsAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.MeasurementSubPrograms).ThenInclude(l => l.SubProgram)
            .Include(x => x.MeasurementUnits).ThenInclude(u => u.Unit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Measurement>> GetApplicableForSubProgramAsync(int subProgramId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.MeasurementSubPrograms).ThenInclude(l => l.SubProgram)
            .Include(x => x.MeasurementUnits).ThenInclude(u => u.Unit)
            .Where(x => x.MeasurementSubPrograms.Any(l => l.SubProgramId == subProgramId))
            .ToListAsync(cancellationToken);
    }

    public async Task<Measurement?> GetByIdWithSubProgramsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.MeasurementSubPrograms).ThenInclude(l => l.SubProgram)
            .Include(x => x.MeasurementUnits).ThenInclude(u => u.Unit)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
```

(`IMeasurementRepository.cs` keeps the same 3 method signatures — no changes needed there, only the implementation's `Include` chains grew. Method names stay `...WithSubProgramsAsync` even though they now also load units, to avoid an unnecessary rename ripple through `MeasurementService.cs`.)

- [ ] **Step 7: Rework `MeasurementService`**

Replace `Backend/src/SmartInvest.Application/Services/MeasurementService.cs` in full:

```csharp
using AutoMapper;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;

namespace SmartInvest.Application.Services;

public class MeasurementService : IMeasurementService
{
    private readonly IMeasurementRepository _measurementRepository;
    private readonly IGenericRepository<MeasurementSubProgram> _linkRepository;
    private readonly IGenericRepository<MeasurementUnit> _unitLinkRepository;
    private readonly IGenericRepository<SubProjectMeasurementValue> _valueRepository;
    private readonly IGenericRepository<SubProject> _subProjectRepository;
    private readonly IGenericRepository<MainProject> _mainProjectRepository;
    private readonly IGenericRepository<SubProgram> _subProgramRepository;
    private readonly IGenericRepository<Unit> _unitRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public MeasurementService(
        IMeasurementRepository measurementRepository,
        IGenericRepository<MeasurementSubProgram> linkRepository,
        IGenericRepository<MeasurementUnit> unitLinkRepository,
        IGenericRepository<SubProjectMeasurementValue> valueRepository,
        IGenericRepository<SubProject> subProjectRepository,
        IGenericRepository<MainProject> mainProjectRepository,
        IGenericRepository<SubProgram> subProgramRepository,
        IGenericRepository<Unit> unitRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _measurementRepository = measurementRepository;
        _linkRepository = linkRepository;
        _unitLinkRepository = unitLinkRepository;
        _valueRepository = valueRepository;
        _subProjectRepository = subProjectRepository;
        _mainProjectRepository = mainProjectRepository;
        _subProgramRepository = subProgramRepository;
        _unitRepository = unitRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<MeasurementDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var measurements = await _measurementRepository.GetAllWithSubProgramsAsync(cancellationToken);
        return _mapper.Map<List<MeasurementDto>>(measurements);
    }

    public async Task<IReadOnlyList<MeasurementDto>> GetApplicableForSubProgramAsync(int subProgramId, CancellationToken cancellationToken = default)
    {
        var measurements = await _measurementRepository.GetApplicableForSubProgramAsync(subProgramId, cancellationToken);
        return _mapper.Map<List<MeasurementDto>>(measurements);
    }

    public async Task<MeasurementDto> CreateAsync(CreateMeasurementDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateSubProgramIdsAsync(dto.SubProgramIds, cancellationToken);
        await ValidateUnitIdsAsync(dto.UnitIds, cancellationToken);

        var entity = new Measurement
        {
            Name = dto.Name.Trim(),
            MeasurementSubPrograms = dto.SubProgramIds
                .Select(spId => new MeasurementSubProgram { SubProgramId = spId })
                .ToList(),
            MeasurementUnits = dto.UnitIds
                .Select(uId => new MeasurementUnit { UnitId = uId })
                .ToList(),
        };

        await _measurementRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdOrThrowAsync(entity.Id, cancellationToken);
    }

    public async Task<MeasurementDto> UpdateAsync(int id, UpdateMeasurementDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateSubProgramIdsAsync(dto.SubProgramIds, cancellationToken);
        await ValidateUnitIdsAsync(dto.UnitIds, cancellationToken);

        var entity = await _measurementRepository.GetByIdWithSubProgramsAsync(id, cancellationToken)
            ?? throw new NotFoundException($"القياس رقم {id} غير موجود");

        entity.Name = dto.Name.Trim();

        foreach (var existingLink in entity.MeasurementSubPrograms.ToList())
        {
            _linkRepository.Remove(existingLink);
        }
        entity.MeasurementSubPrograms = dto.SubProgramIds
            .Select(spId => new MeasurementSubProgram { MeasurementId = id, SubProgramId = spId })
            .ToList();

        foreach (var existingUnitLink in entity.MeasurementUnits.ToList())
        {
            _unitLinkRepository.Remove(existingUnitLink);
        }
        entity.MeasurementUnits = dto.UnitIds
            .Select(uId => new MeasurementUnit { MeasurementId = id, UnitId = uId })
            .ToList();

        _measurementRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdOrThrowAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _measurementRepository.GetByIdWithSubProgramsAsync(id, cancellationToken)
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

        if (entity.MeasurementUnits.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف القياس وهو مرتبط بوحدات قياس — قم بإلغاء الربط أولًا");
        }

        _measurementRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubProjectMeasurementValueDto>> GetValuesForSubProjectAsync(int subProjectId, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        var mainProject = await _mainProjectRepository.GetByIdAsync(subProject.MainProjectId, cancellationToken)
            ?? throw new NotFoundException("المشروع الرئيسي التابع له غير موجود");

        var applicable = await GetApplicableForSubProgramAsync(mainProject.SubProgramId, cancellationToken);

        var existingValues = await _valueRepository.FindAsync(x => x.SubProjectId == subProjectId, cancellationToken);
        var valuesByMeasurementId = existingValues.ToDictionary(x => x.MeasurementId);

        var units = await _unitRepository.GetAllAsync(cancellationToken);
        var unitNamesById = units.ToDictionary(u => u.Id, u => u.Name);

        return applicable
            .Select(m =>
            {
                var hasValue = valuesByMeasurementId.TryGetValue(m.Id, out var existing);
                int? unitId = hasValue ? existing!.UnitId : null;
                return new SubProjectMeasurementValueDto
                {
                    MeasurementId = m.Id,
                    MeasurementName = m.Name,
                    UnitId = unitId,
                    UnitName = unitId.HasValue && unitNamesById.TryGetValue(unitId.Value, out var n) ? n : null,
                    Value = hasValue ? existing!.Value : null,
                };
            })
            .ToList();
    }

    public async Task SetValuesForSubProjectAsync(int subProjectId, SetSubProjectMeasurementValuesDto dto, CancellationToken cancellationToken = default)
    {
        var subProject = await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken)
            ?? throw new NotFoundException($"المشروع الفرعي رقم {subProjectId} غير موجود");

        var mainProject = await _mainProjectRepository.GetByIdAsync(subProject.MainProjectId, cancellationToken)
            ?? throw new NotFoundException("المشروع الرئيسي التابع له غير موجود");

        var applicable = await GetApplicableForSubProgramAsync(mainProject.SubProgramId, cancellationToken);
        var applicableById = applicable.ToDictionary(m => m.Id);

        foreach (var entry in dto.Values)
        {
            if (!applicableById.TryGetValue(entry.MeasurementId, out var measurement))
            {
                throw new NotFoundException($"القياس رقم {entry.MeasurementId} غير مرتبط بالبرنامج الفرعي لهذا المشروع");
            }

            if (entry.Value != null && (entry.UnitId == null || !measurement.UnitIds.Contains(entry.UnitId.Value)))
            {
                throw new BusinessRuleException($"وحدة القياس غير صحيحة أو غير مرتبطة بالقياس «{measurement.Name}»");
            }
        }

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
                toUpdate.UnitId = entry.UnitId!.Value;
                _valueRepository.Update(toUpdate);
            }
            else
            {
                await _valueRepository.AddAsync(new SubProjectMeasurementValue
                {
                    SubProjectId = subProjectId,
                    MeasurementId = entry.MeasurementId,
                    UnitId = entry.UnitId!.Value,
                    Value = entry.Value.Value,
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<MeasurementDto> GetByIdOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _measurementRepository.GetByIdWithSubProgramsAsync(id, cancellationToken)
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

    private async Task ValidateUnitIdsAsync(List<int> unitIds, CancellationToken cancellationToken)
    {
        foreach (var unitId in unitIds.Distinct())
        {
            var unit = await _unitRepository.GetByIdAsync(unitId, cancellationToken);
            if (unit == null)
            {
                throw new NotFoundException($"الوحدة رقم {unitId} غير موجودة");
            }
        }
    }
}
```

- [ ] **Step 8: Extend `LookupService.DeleteUnitAsync` with a dependent-usage guard**

In `Backend/src/SmartInvest.Application/Services/LookupService.cs`, replace the `DeleteUnitAsync` method added in Task 1:

```csharp
    public async Task DeleteUnitAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"الوحدة رقم {id} غير موجودة");

        var linkedMeasurementUnits = await _measurementUnitRepository.FindAsync(x => x.UnitId == id, cancellationToken);
        if (linkedMeasurementUnits.Count > 0)
        {
            throw new BusinessRuleException("لا يمكن حذف الوحدة لارتباطها بقياسات مستخدمة");
        }

        _unitRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
```

This references a new `_measurementUnitRepository` field — add it next to `_unitRepository`:

```csharp
    private readonly IGenericRepository<AccountingUnit> _accountingUnitRepository;
    private readonly IGenericRepository<Unit> _unitRepository;
    private readonly IGenericRepository<MeasurementUnit> _measurementUnitRepository;
```

Add the constructor parameter (after `unitRepository`) and assignment:

```csharp
        IGenericRepository<AccountingUnit> accountingUnitRepository,
        IGenericRepository<Unit> unitRepository,
        IGenericRepository<MeasurementUnit> measurementUnitRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        // ...
        _accountingUnitRepository = accountingUnitRepository;
        _unitRepository = unitRepository;
        _measurementUnitRepository = measurementUnitRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }
```

- [ ] **Step 9: Build the backend**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 10: Generate and apply the migration**

```bash
cd Backend/src/SmartInvest.API
dotnet ef migrations add AddMeasurementUnits --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

Inspect the generated migration. Expect:
- `DropColumn` for `Measurement.Unit`.
- `CreateTable` for `MeasurementUnits` (Id PK, MeasurementId FK, UnitId FK).
- `AddColumn` for `SubProjectMeasurementValue.UnitId` (int, not null) with its FK to `Units`.

Since no real measurement values exist in any environment yet (per the design's Out of Scope section), the non-nullable `UnitId` column needs no default-value backfill SQL — the table is empty everywhere. If the generated migration includes a `DEFAULT 0` clause for the new column (EF's default behavior for a non-nullable int column added to a non-empty table), leave it as-is; it's inert since the table has no rows.

```bash
dotnet ef database update --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- [ ] **Step 11: Empty-probe verify**

```bash
dotnet ef migrations add ProbeCheck --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```
Confirm both `Up()`/`Down()` are empty, then:
```bash
dotnet ef migrations remove --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- [ ] **Step 12: Manual check via Swagger**

Create 2 units (`متر`, `سنتيمتر`) if not already present. Create a measurement linked to both units and a sub-program. Confirm `GET /api/measurements` returns `unitIds`/`unitNames` with both. Confirm `PUT /api/subprojects/{id}/measurement-values` rejects a value submitted with a `unitId` not in the measurement's linked units (expect 400 with the Arabic message), and accepts one that is, then `GET` reflects the saved `unitId`/`unitName`/`value`. Confirm `DELETE /api/lookups/units/{id}` fails with a clear message while a measurement still links to it.

- [ ] **Step 13: Commit**

```bash
git add Backend/src/SmartInvest.Domain/Entities/ Backend/src/SmartInvest.Application/DTOs/MeasurementDtos.cs Backend/src/SmartInvest.Application/Common/Mappings/MeasurementMappingProfile.cs Backend/src/SmartInvest.Infrastructure/Repositories/MeasurementRepository.cs Backend/src/SmartInvest.Application/Services/MeasurementService.cs Backend/src/SmartInvest.Application/Services/LookupService.cs Backend/src/SmartInvest.Infrastructure/Migrations/
git commit -m "feat: measurements record a unit per value instead of one fixed unit

Measurement now links to many Units (MeasurementUnit join, mirroring
the existing MeasurementSubProgram join) instead of storing a single
fixed Unit string. SubProjectMeasurementValue gains a required UnitId,
chosen per sub-project at record time — the same measurement can be
recorded in meters on one sub-project and centimeters on another."
```

---

### Task 3: Frontend — Models + Unit Lookup Service Methods

**Files:**
- Modify: `Frontend/src/app/core/models/project.models.ts`
- Modify: `Frontend/src/app/core/services/lookups.service.ts`

**Interfaces:**
- Produces: `Measurement`/`CreateMeasurement`/`UpdateMeasurement` gain `unitIds: number[]` (+`unitNames: string[]` read-side), replacing `unit: string`. `SubProjectMeasurementValue` gains `unitId: number | null`/`unitName: string | null` replacing `unit: string`. `SetMeasurementValue` gains `unitId: number | null`. `LookupsService` gains `getUnits`/`createUnit`/`updateUnit`/`deleteUnit`. Tasks 4 and 5 consume these.

- [ ] **Step 1: Update the models**

In `Frontend/src/app/core/models/project.models.ts`, replace the `Measurement`/`CreateMeasurement`/`UpdateMeasurement`/`SubProjectMeasurementValue`/`SetMeasurementValue` block at the end of the file:

```typescript
export interface Measurement {
  id: number;
  name: string;
  subProgramIds: number[];
  subProgramNames: string[];
  unitIds: number[];
  unitNames: string[];
}

export interface CreateMeasurement {
  name: string;
  subProgramIds: number[];
  unitIds: number[];
}

export type UpdateMeasurement = CreateMeasurement;

export interface SubProjectMeasurementValue {
  measurementId: number;
  measurementName: string;
  unitId: number | null;
  unitName: string | null;
  value: number | null;
}

export interface SetMeasurementValue {
  measurementId: number;
  unitId: number | null;
  value: number | null;
}
```

- [ ] **Step 2: Add Unit CRUD methods to `LookupsService`**

In `Frontend/src/app/core/services/lookups.service.ts`, add at the end of the class, right before the final closing `}`:

```typescript
  getUnits(): Observable<Lookup[]> {
    return this.http.get<Lookup[]>(`${this.base}/units`);
  }

  createUnit(dto: CreateNamedLookup): Observable<Lookup> {
    return this.http.post<Lookup>(`${this.base}/units`, dto);
  }

  updateUnit(id: number, dto: UpdateNamedLookup): Observable<Lookup> {
    return this.http.put<Lookup>(`${this.base}/units/${id}`, dto);
  }

  deleteUnit(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/units/${id}`);
  }
```

(`CreateNamedLookup`/`UpdateNamedLookup` are already imported at the top of this file — no import changes needed.)

- [ ] **Step 3: Type-check**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: errors in `measurements.ts` and `sub-project-form.ts` (both still reference the old `m.unit`/`v.unit` shape) — these are expected and fixed in Tasks 4 and 5. Confirm `project.models.ts` and `lookups.service.ts` themselves report no errors.

- [ ] **Step 4: Commit**

```bash
git add Frontend/src/app/core/models/project.models.ts Frontend/src/app/core/services/lookups.service.ts
git commit -m "feat: model Measurement's unit as a list, add Unit lookup service methods"
```

---

### Task 4: Frontend — Measurements Page Rework

**Files:**
- Modify: `Frontend/src/app/features/measurements/measurements.ts`
- Modify: `Frontend/src/app/features/measurements/measurements.html`
- Modify: `Frontend/src/app/features/measurements/measurements.css`

**Interfaces:**
- Consumes: `Measurement`/`CreateMeasurement`/`UpdateMeasurement` (Task 3), `LookupsService.getUnits()` (Task 3), `LookupsService.getMainPrograms()`/`getSubPrograms()` (existing).
- Produces: no new exports — this is a leaf feature page.

- [ ] **Step 1: Read the current `measurements.ts` for its exact current shape**

(Already done during planning — current file is 138 lines, imports `LookupsService`, `MeasurementsService`, `AuthService`, uses `fUnit = signal('')` and a flat `fSubProgramIds: Set<number>`.)

- [ ] **Step 2: Replace `measurements.ts` in full**

```typescript
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LookupsService } from '../../core/services/lookups.service';
import { MeasurementsService } from '../../core/services/measurements.service';
import { AuthService } from '../../core/services/auth.service';
import { CreateMeasurement, Lookup, Measurement, SubProgramLookup } from '../../core/models/project.models';

interface MainProgramGroup {
  id: number;
  name: string;
  subPrograms: SubProgramLookup[];
}

@Component({
  selector: 'app-measurements',
  imports: [FormsModule],
  templateUrl: './measurements.html',
  styleUrl: './measurements.css',
})
export class Measurements {
  private readonly lookups = inject(LookupsService);
  private readonly measurementsService = inject(MeasurementsService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly search = signal('');

  private readonly measurements = signal<Measurement[]>([]);
  protected readonly subPrograms = signal<SubProgramLookup[]>([]);
  private readonly mainPrograms = signal<Lookup[]>([]);
  protected readonly units = signal<Lookup[]>([]);

  protected readonly mainProgramGroups = computed<MainProgramGroup[]>(() =>
    this.mainPrograms().map((mp) => ({
      id: mp.id,
      name: mp.name,
      subPrograms: this.subPrograms().filter((sp) => sp.mainProgramId === mp.id),
    })),
  );

  protected readonly expandedMainProgramIds = signal<Set<number>>(new Set());

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) return this.measurements();
    return this.measurements().filter((m) => m.name.toLowerCase().includes(term));
  });

  protected readonly showForm = signal(false);
  protected readonly editing = signal<Measurement | null>(null);
  protected readonly fName = signal('');
  protected readonly fSubProgramIds = signal<Set<number>>(new Set());
  protected readonly fUnitIds = signal<Set<number>>(new Set());
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    Promise.all([
      new Promise<void>((resolve, reject) =>
        this.measurementsService.getAll().subscribe({ next: (v) => { this.measurements.set(v); resolve(); }, error: reject }),
      ),
      new Promise<void>((resolve, reject) =>
        this.lookups.getMainPrograms().subscribe({ next: (v) => { this.mainPrograms.set(v); resolve(); }, error: reject }),
      ),
      new Promise<void>((resolve, reject) =>
        this.lookups.getSubPrograms().subscribe({ next: (v) => { this.subPrograms.set(v); resolve(); }, error: reject }),
      ),
      new Promise<void>((resolve, reject) =>
        this.lookups.getUnits().subscribe({ next: (v) => { this.units.set(v); resolve(); }, error: reject }),
      ),
    ])
      .then(() => this.loading.set(false))
      .catch(() => {
        this.loading.set(false);
        this.error.set('تعذّر تحميل القياسات');
      });
  }

  protected toggleMainProgramExpanded(id: number): void {
    this.expandedMainProgramIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected openAdd(): void {
    this.editing.set(null);
    this.fName.set('');
    this.fSubProgramIds.set(new Set());
    this.fUnitIds.set(new Set());
    this.expandedMainProgramIds.set(new Set());
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected openEdit(m: Measurement): void {
    this.editing.set(m);
    this.fName.set(m.name);
    this.fSubProgramIds.set(new Set(m.subProgramIds));
    this.fUnitIds.set(new Set(m.unitIds));
    const linkedMainProgramIds = new Set(
      this.subPrograms()
        .filter((sp) => m.subProgramIds.includes(sp.id))
        .map((sp) => sp.mainProgramId),
    );
    this.expandedMainProgramIds.set(linkedMainProgramIds);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    if (this.saving()) return;
    this.showForm.set(false);
  }

  protected toggleSubProgram(id: number): void {
    this.fSubProgramIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected toggleUnit(id: number): void {
    this.fUnitIds.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected submitForm(): void {
    if (this.saving()) return;
    this.formError.set(null);

    const name = this.fName().trim();
    if (!name) {
      this.formError.set('برجاء إدخال اسم القياس');
      return;
    }

    const dto: CreateMeasurement = {
      name,
      subProgramIds: [...this.fSubProgramIds()],
      unitIds: [...this.fUnitIds()],
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
        this.formError.set(err?.error?.message ?? 'تعذّر الحفظ');
      },
    });
  }

  protected deleteMeasurement(m: Measurement): void {
    if (!confirm(`تأكيد حذف «${m.name}»؟`)) return;
    this.measurementsService.delete(m.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر الحذف'),
    });
  }
}
```

- [ ] **Step 3: Replace `measurements.html` in full**

```html
<div class="page">
  <header class="page-head">
    <div>
      <h1>القياسات المخصصة</h1>
      <p>تعريف قياسات مثل الارتفاع أو المسافة وربطها بالبرامج الفرعية ووحدات القياس</p>
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
              <th>الوحدات</th>
              <th>البرامج الفرعية المرتبطة</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            @for (m of filtered(); track m.id) {
              <tr>
                <td><b>{{ m.name }}</b></td>
                <td>
                  @if (m.unitNames.length === 0) {
                    <span class="muted">غير محدد</span>
                  } @else {
                    <div class="chips">
                      @for (name of m.unitNames; track name) { <span class="chip">{{ name }}</span> }
                    </div>
                  }
                </td>
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
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(640px,100%)">
        <div class="si-modal-head">
          <div class="grow"><h3>{{ editing() ? 'تعديل قياس' : 'إضافة قياس جديد' }}</h3></div>
          <button class="si-x" (click)="closeForm()" aria-label="إغلاق">×</button>
        </div>
        <div class="si-modal-body">
          @if (formError()) { <div class="si-err">{{ formError() }}</div> }
          <div class="si-grid">
            <div class="si-fld full"><label>اسم القياس <span class="req">*</span></label><input [ngModel]="fName()" (ngModelChange)="fName.set($event)" placeholder="مثال: الارتفاع" /></div>

            <div class="si-fld full">
              <label>وحدات القياس</label>
              <div class="picker">
                @for (u of units(); track u.id) {
                  <label class="pick-chk">
                    <input type="checkbox" [checked]="fUnitIds().has(u.id)" (change)="toggleUnit(u.id)" />
                    {{ u.name }}
                  </label>
                } @empty {
                  <p class="hint">لا توجد وحدات قياس بعد — أضفها من الإعدادات أولًا.</p>
                }
              </div>
            </div>

            <div class="si-fld full">
              <label>البرامج الفرعية المرتبطة</label>
              <div class="picker groups">
                @for (g of mainProgramGroups(); track g.id) {
                  <div class="group">
                    <button type="button" class="group-head" (click)="toggleMainProgramExpanded(g.id)">
                      <svg class="chevron" [class.open]="expandedMainProgramIds().has(g.id)" viewBox="0 0 24 24" width="14" fill="none" stroke="currentColor" stroke-width="2"><path d="m9 6 6 6-6 6" /></svg>
                      {{ g.name }}
                    </button>
                    @if (expandedMainProgramIds().has(g.id)) {
                      <div class="group-body">
                        @for (sp of g.subPrograms; track sp.id) {
                          <label class="pick-chk">
                            <input type="checkbox" [checked]="fSubProgramIds().has(sp.id)" (change)="toggleSubProgram(sp.id)" />
                            {{ sp.name }}
                          </label>
                        } @empty {
                          <p class="hint">لا توجد برامج فرعية ضمن هذا البرنامج.</p>
                        }
                      </div>
                    }
                  </div>
                } @empty {
                  <p class="hint">لا توجد برامج رئيسية بعد.</p>
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

- [ ] **Step 4: Extend `measurements.css`**

In `Frontend/src/app/features/measurements/measurements.css`, add after the existing `.pick-chk` rule:

```css
.hint { font-size: 12px; color: var(--muted); margin: 0; }

.picker.groups { max-height: 260px; }
.group { border-bottom: 1px solid var(--line); }
.group:last-child { border-bottom: 0; }
.group-head { display: flex; align-items: center; gap: 8px; width: 100%; text-align: start; padding: 8px 4px; border: 0; background: transparent; font-weight: 700; font-size: 13px; color: var(--ink); }
.chevron { transition: transform .15s ease; }
.chevron.open { transform: rotate(90deg); }
.group-body { display: flex; flex-direction: column; gap: 6px; padding: 4px 4px 10px 26px; }
```

- [ ] **Step 5: Type-check and build**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors in `measurements.ts`/`.html` (remaining errors, if any, are in `sub-project-form.ts`, fixed in Task 5).

- [ ] **Step 6: Manual check via browser**

With `frontend-dev` and `backend-api` running, navigate to `/app/measurements`: confirm the units multi-select shows real units, the sub-program picker groups by Main Program (collapsed by default) and expands/collapses on click, and the table shows both new chip columns. Create a measurement with 2 units and 1 sub-program; edit it and confirm the linked Main Program is expanded automatically and both pickers are pre-checked correctly.

- [ ] **Step 7: Commit**

```bash
git add Frontend/src/app/features/measurements/
git commit -m "feat: measurements modal gets a unit multi-select and Main-Program-grouped sub-program picker"
```

---

### Task 5: Frontend — Sub-Project Form Step 4 Rework

**Files:**
- Modify: `Frontend/src/app/features/projects/sub-project-form.ts`

**Interfaces:**
- Consumes: `SubProjectMeasurementValue`/`SetMeasurementValue` (Task 3), `MeasurementsService.getApplicable()`/`getValuesForSubProject()`/`setValuesForSubProject()` (existing, unchanged signatures).

- [ ] **Step 1: Update the `applicableMeasurements` signal's element type and Step 4 template**

In `Frontend/src/app/features/projects/sub-project-form.ts`, replace the line:

```typescript
  protected readonly applicableMeasurements = signal<{ id: number; name: string; unit: string }[]>([]);
```

with:

```typescript
  protected readonly applicableMeasurements = signal<{ id: number; name: string; unitIds: number[]; unitNames: string[] }[]>([]);
  protected readonly measurementUnits = signal<Record<number, number | null>>({});
```

- [ ] **Step 2: Replace the Step 4 template block**

Replace:

```html
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
```

with:

```html
            @if (applicableMeasurements().length > 0) {
              <div class="si-step"><span class="n">4</span><h4>القياسات المخصصة</h4></div>
              <div class="si-grid">
                @for (m of applicableMeasurements(); track m.id) {
                  <div class="si-fld">
                    <label>{{ m.name }} — الوحدة <span class="req">*</span></label>
                    <select
                      [ngModel]="measurementUnits()[m.id] ?? null"
                      (ngModelChange)="setMeasurementUnit(m.id, $event)"
                    >
                      <option [ngValue]="null">اختر الوحدة</option>
                      @for (unitId of m.unitIds; track unitId; let i = $index) {
                        <option [ngValue]="unitId">{{ m.unitNames[i] }}</option>
                      }
                    </select>
                  </div>
                  <div class="si-fld">
                    <label>{{ m.name }} — القيمة</label>
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
```

- [ ] **Step 3: Update `loadApplicableMeasurements`**

Replace:

```typescript
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
```

with:

```typescript
  private loadApplicableMeasurements(mainProjectId: number, subProjectId?: number): void {
    const mainProject = this.mains().find((m) => m.id === mainProjectId);
    if (!mainProject) {
      this.applicableMeasurements.set([]);
      this.measurementValues.set({});
      this.measurementUnits.set({});
      return;
    }

    this.measurementsService.getApplicable(mainProject.subProgramId).subscribe({
      next: (measurements) => {
        this.applicableMeasurements.set(
          measurements.map((m) => ({ id: m.id, name: m.name, unitIds: m.unitIds, unitNames: m.unitNames })),
        );

        if (subProjectId != null) {
          this.measurementsService.getValuesForSubProject(subProjectId).subscribe({
            next: (values: SubProjectMeasurementValue[]) => {
              const valueMap: Record<number, number | null> = {};
              const unitMap: Record<number, number | null> = {};
              for (const v of values) {
                valueMap[v.measurementId] = v.value;
                unitMap[v.measurementId] = v.unitId;
              }
              this.measurementValues.set(valueMap);
              this.measurementUnits.set(unitMap);
            },
            error: () => {},
          });
        } else {
          this.measurementValues.set({});
          this.measurementUnits.set({});
        }
      },
      error: () => this.applicableMeasurements.set([]),
    });
  }

  protected setMeasurementValue(measurementId: number, value: number | null): void {
    this.measurementValues.update((current) => ({ ...current, [measurementId]: value }));
  }

  protected setMeasurementUnit(measurementId: number, unitId: number | null): void {
    this.measurementUnits.update((current) => ({ ...current, [measurementId]: unitId }));
  }
```

- [ ] **Step 4: Reset the new signal alongside the existing ones**

In `onMainProjectSelected` and `resetForm`, add `this.measurementUnits.set({});` next to every existing `this.measurementValues.set({});` call. There are 2 occurrences of `this.measurementValues.set({});` outside of `loadApplicableMeasurements` (already updated in Step 3) — one in `onMainProjectSelected`, one in `resetForm`. Update both:

```typescript
  protected onMainProjectSelected(mainProjectId: number | null): void {
    this.mainProjectId.set(mainProjectId);
    if (mainProjectId != null) {
      this.loadApplicableMeasurements(mainProjectId);
    } else {
      this.applicableMeasurements.set([]);
      this.measurementValues.set({});
      this.measurementUnits.set({});
    }
  }
```

```typescript
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
    this.measurementUnits.set({});
  }
```

- [ ] **Step 5: Validate and submit the unit alongside the value**

Replace `syncMeasurementValues`:

```typescript
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
```

with:

```typescript
  private syncMeasurementValues(subProjectId: number): void {
    const applicable = this.applicableMeasurements();
    if (applicable.length === 0) {
      this.saving.set(false);
      this.saved.emit();
      return;
    }

    const values = this.measurementValues();
    const units = this.measurementUnits();
    const payload = applicable.map((m) => ({
      measurementId: m.id,
      unitId: units[m.id] ?? null,
      value: values[m.id] ?? null,
    }));

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
```

Add a client-side pre-check in `submit()`, right after the existing validation block (before `const base = {`):

```typescript
    for (const m of this.applicableMeasurements()) {
      const value = this.measurementValues()[m.id];
      const unitId = this.measurementUnits()[m.id];
      if (value != null && unitId == null) {
        this.error.set(`برجاء اختيار وحدة القياس لـ «${m.name}»`);
        return;
      }
    }
```

- [ ] **Step 6: Type-check and build**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
cd Frontend && npx ng build
```
Expected: no errors (the inline template in `sub-project-form.ts` is part of the same file tsc already checks, but `ng build` is the authoritative check per this repo's established convention).

- [ ] **Step 7: Manual check via browser**

Add a sub-project under a sub-program with a measurement linked to 2 units. Confirm Step 4 shows a required unit `<select>` (only those 2 units) plus a value input. Enter a value without selecting a unit — confirm the clear validation error blocks submit. Select a unit and value, save, re-open the edit form — confirm both the unit and value are pre-filled.

- [ ] **Step 8: Commit**

```bash
git add Frontend/src/app/features/projects/sub-project-form.ts
git commit -m "feat: require a per-measurement unit selection when recording a value"
```

---

### Task 6: Frontend — Settings Routed Shell

**Files:**
- Modify: `Frontend/src/app/app.config.ts`
- Modify: `Frontend/src/app/app.routes.ts`
- Modify: `Frontend/src/app/layout/main-layout/main-layout.ts`
- Create: `Frontend/src/app/features/settings/settings-tabs.ts`
- Create: `Frontend/src/app/features/settings/settings-lookup-page.ts`
- Create: `Frontend/src/app/features/settings/settings-lookup-page.html`
- Replace: `Frontend/src/app/features/settings/settings.ts`
- Replace: `Frontend/src/app/features/settings/settings.html`
- (`Frontend/src/app/features/settings/settings.css` and `settings-lookup-table.ts`/`.html`/`.css` are unchanged — reused as-is.)

**Interfaces:**
- Consumes: `LookupsService.getUnits`/`createUnit`/`updateUnit`/`deleteUnit` (Task 3), the existing `SettingsLookupTable` component (unmodified).
- Produces: `/app/settings/*` child routes; `Settings` (shell) and `SettingsLookupPage` (generic tab) components.

- [ ] **Step 1: Enable route-data-to-input binding**

In `Frontend/src/app/app.config.ts`, add `withComponentInputBinding` to the `provideRouter` call:

```typescript
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' }), withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
  ],
};
```

- [ ] **Step 2: Create the shared tabs config**

Create `Frontend/src/app/features/settings/settings-tabs.ts`:

```typescript
export type TabKey =
  | 'mainProgram'
  | 'subProgram'
  | 'governorate'
  | 'markaz'
  | 'village'
  | 'priority'
  | 'status'
  | 'componentType'
  | 'projectLevel'
  | 'accountingUnit'
  | 'contractType'
  | 'unit';

export interface TabDef {
  key: TabKey;
  slug: string;
  label: string;
  addLabel: string;
  hasParent: boolean;
  parentLabel: string;
}

export const SETTINGS_LOOKUP_TABS: TabDef[] = [
  { key: 'mainProgram', slug: 'main-programs', label: 'البرامج الرئيسية', addLabel: 'إضافة برنامج رئيسي', hasParent: false, parentLabel: '' },
  { key: 'subProgram', slug: 'sub-programs', label: 'البرامج الفرعية', addLabel: 'إضافة برنامج فرعي', hasParent: true, parentLabel: 'البرنامج الرئيسي' },
  { key: 'governorate', slug: 'governorates', label: 'المحافظات', addLabel: 'إضافة محافظة', hasParent: false, parentLabel: '' },
  { key: 'markaz', slug: 'markaz', label: 'المراكز', addLabel: 'إضافة مركز', hasParent: true, parentLabel: 'المحافظة' },
  { key: 'village', slug: 'villages', label: 'القرى', addLabel: 'إضافة قرية', hasParent: true, parentLabel: 'المركز' },
  { key: 'priority', slug: 'priorities', label: 'الأولويات', addLabel: 'إضافة أولوية', hasParent: false, parentLabel: '' },
  { key: 'status', slug: 'statuses', label: 'حالات المشروع', addLabel: 'إضافة حالة', hasParent: false, parentLabel: '' },
  { key: 'componentType', slug: 'component-types', label: 'المكوّن العيني', addLabel: 'إضافة مكوّن عيني', hasParent: false, parentLabel: '' },
  { key: 'projectLevel', slug: 'project-levels', label: 'مستوى المشروع', addLabel: 'إضافة مستوى', hasParent: false, parentLabel: '' },
  { key: 'accountingUnit', slug: 'accounting-units', label: 'الوحدة الحسابية', addLabel: 'إضافة وحدة حسابية', hasParent: false, parentLabel: '' },
  { key: 'contractType', slug: 'contract-types', label: 'أنواع العقود', addLabel: 'إضافة نوع عقد', hasParent: false, parentLabel: '' },
  { key: 'unit', slug: 'units', label: 'وحدات القياس', addLabel: 'إضافة وحدة قياس', hasParent: false, parentLabel: '' },
];
```

- [ ] **Step 3: Create `SettingsLookupPage`**

Create `Frontend/src/app/features/settings/settings-lookup-page.ts`:

```typescript
import { Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { LookupsService } from '../../core/services/lookups.service';
import { ContractTypesService } from '../../core/services/contract-types.service';
import { AuthService } from '../../core/services/auth.service';
import { Lookup, MarkazLookup, SubProgramLookup, VillageLookup } from '../../core/models/project.models';
import { SettingsLookupItem, SettingsLookupParentOption, SettingsLookupSaveEvent, SettingsLookupTable } from './settings-lookup-table';
import { SETTINGS_LOOKUP_TABS, TabKey } from './settings-tabs';

@Component({
  selector: 'app-settings-lookup-page',
  imports: [SettingsLookupTable],
  templateUrl: './settings-lookup-page.html',
})
export class SettingsLookupPage {
  private readonly lookups = inject(LookupsService);
  private readonly contractTypes = inject(ContractTypesService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  readonly tab = input.required<TabKey>();

  private readonly lookupTable = viewChild.required<SettingsLookupTable>('lookupTable');

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly mainPrograms = signal<Lookup[]>([]);
  private readonly subPrograms = signal<SubProgramLookup[]>([]);
  private readonly governorates = signal<Lookup[]>([]);
  private readonly markazList = signal<MarkazLookup[]>([]);
  private readonly villages = signal<VillageLookup[]>([]);
  private readonly priorities = signal<Lookup[]>([]);
  private readonly statuses = signal<Lookup[]>([]);
  private readonly componentTypes = signal<Lookup[]>([]);
  private readonly projectLevels = signal<Lookup[]>([]);
  private readonly accountingUnits = signal<Lookup[]>([]);
  private readonly contractTypeList = signal<Lookup[]>([]);
  private readonly units = signal<Lookup[]>([]);

  protected readonly activeTabDef = computed(() => SETTINGS_LOOKUP_TABS.find((t) => t.key === this.tab())!);

  protected readonly parentOptions = computed<SettingsLookupParentOption[]>(() => {
    switch (this.tab()) {
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
    switch (this.tab()) {
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
      case 'componentType':
        return this.componentTypes().map((c) => ({ id: c.id, name: c.name }));
      case 'projectLevel':
        return this.projectLevels().map((p) => ({ id: p.id, name: p.name }));
      case 'accountingUnit':
        return this.accountingUnits().map((a) => ({ id: a.id, name: a.name }));
      case 'contractType':
        return this.contractTypeList().map((c) => ({ id: c.id, name: c.name }));
      case 'unit':
        return this.units().map((u) => ({ id: u.id, name: u.name }));
      default:
        return [];
    }
  });

  constructor() {
    effect(() => {
      this.tab();
      this.loadAll();
    });
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
      this.toPromise(this.lookups.getComponentTypes(), this.componentTypes),
      this.toPromise(this.lookups.getProjectLevels(), this.projectLevels),
      this.toPromise(this.lookups.getAccountingUnits(), this.accountingUnits),
      this.toPromise(this.contractTypes.getAll(), this.contractTypeList),
      this.toPromise(this.lookups.getUnits(), this.units),
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
    const tab = this.tab();
    const req = this.buildSaveRequest(tab, event);
    if (!req) return;
    req.subscribe({
      next: () => {
        this.lookupTable().saveSucceeded();
        this.loadAll();
      },
      error: (err) => this.lookupTable().saveFailed(err?.error?.message ?? 'تعذّر الحفظ'),
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
      case 'unit':
        return event.id
          ? this.lookups.updateUnit(event.id, { name })
          : this.lookups.createUnit({ name });
      default:
        return null;
    }
  }

  protected onDelete(id: number): void {
    const tab = this.tab();
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
      case 'componentType': return this.lookups.deleteComponentType(id);
      case 'projectLevel': return this.lookups.deleteProjectLevel(id);
      case 'accountingUnit': return this.lookups.deleteAccountingUnit(id);
      case 'contractType': return this.contractTypes.delete(id);
      case 'unit': return this.lookups.deleteUnit(id);
    }
  }
}
```

- [ ] **Step 4: Create `SettingsLookupPage`'s template**

Create `Frontend/src/app/features/settings/settings-lookup-page.html`:

```html
<app-settings-lookup-table
  #lookupTable
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
```

- [ ] **Step 5: Replace `settings.ts` (shell) in full**

```typescript
import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SETTINGS_LOOKUP_TABS } from './settings-tabs';

@Component({
  selector: 'app-settings',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './settings.html',
  styleUrl: './settings.css',
})
export class Settings {
  private readonly auth = inject(AuthService);
  protected readonly isManager = this.auth.isManager;
  protected readonly lookupTabs = SETTINGS_LOOKUP_TABS;
}
```

- [ ] **Step 6: Replace `settings.html` (shell) in full**

```html
<div class="page">
  <header class="page-head">
    <div>
      <h1>الإعدادات</h1>
      <p>إدارة القوائم الأساسية والصفحات الإدارية المستخدمة في المشروعات</p>
    </div>
  </header>

  <div class="layout">
    <nav class="tabs">
      @for (t of lookupTabs; track t.key) {
        <a class="tab" [routerLink]="t.slug" routerLinkActive="on">{{ t.label }}</a>
      }
      <a class="tab" routerLink="contractors" routerLinkActive="on">المقاولون</a>
      <a class="tab" routerLink="agencies" routerLinkActive="on">الجهات التنفيذية</a>
      @if (isManager()) {
        <a class="tab" routerLink="users" routerLinkActive="on">إدارة المستخدمين</a>
      }
      <a class="tab" routerLink="measurements" routerLinkActive="on">القياسات</a>
    </nav>

    <div class="content">
      <router-outlet />
    </div>
  </div>
</div>
```

(`settings.css` needs no changes — its existing `.tab`/`.tab.on` rules already target the class, not the element type, so they apply to `<a>` the same as the old `<button>`.)

- [ ] **Step 7: Update `app.routes.ts`**

Replace the `app` route's `children` array in `Frontend/src/app/app.routes.ts` in full:

```typescript
    children: [
      { path: '', redirectTo: 'projects', pathMatch: 'full' },
      {
        path: 'dashboard',
        canActivate: [roleGuard([Roles.PlanningManager, Roles.SuperAdmin])],
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'projects',
        loadComponent: () =>
          import('./features/projects/projects').then((m) => m.Projects),
      },
      {
        path: 'projects/:id',
        loadComponent: () =>
          import('./features/projects/details/sub-project-details').then(
            (m) => m.SubProjectDetails,
          ),
      },
      {
        path: 'plans',
        loadComponent: () =>
          import('./features/plans/plan-list').then((m) => m.PlanList),
      },
      {
        path: 'plans/:id',
        loadComponent: () =>
          import('./features/plans/plan-print').then((m) => m.PlanPrint),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/settings/settings').then((m) => m.Settings),
        children: [
          { path: '', redirectTo: 'main-programs', pathMatch: 'full' },
          {
            path: 'main-programs',
            data: { tab: 'mainProgram' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'sub-programs',
            data: { tab: 'subProgram' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'governorates',
            data: { tab: 'governorate' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'markaz',
            data: { tab: 'markaz' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'villages',
            data: { tab: 'village' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'priorities',
            data: { tab: 'priority' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'statuses',
            data: { tab: 'status' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'component-types',
            data: { tab: 'componentType' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'project-levels',
            data: { tab: 'projectLevel' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'accounting-units',
            data: { tab: 'accountingUnit' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'contract-types',
            data: { tab: 'contractType' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'units',
            data: { tab: 'unit' },
            loadComponent: () => import('./features/settings/settings-lookup-page').then((m) => m.SettingsLookupPage),
          },
          {
            path: 'users',
            canActivate: [roleGuard([Roles.PlanningManager, Roles.SuperAdmin])],
            loadComponent: () =>
              import('./features/users/users').then((m) => m.Users),
          },
          {
            path: 'contractors',
            loadComponent: () =>
              import('./features/contractors/contractors').then((m) => m.Contractors),
          },
          {
            path: 'agencies',
            loadComponent: () =>
              import('./features/agencies/agencies').then((m) => m.Agencies),
          },
          {
            path: 'measurements',
            loadComponent: () =>
              import('./features/measurements/measurements').then((m) => m.Measurements),
          },
        ],
      },
    ],
```

- [ ] **Step 8: Update `main-layout.ts`**

In `Frontend/src/app/layout/main-layout/main-layout.ts`, replace the `allNav` array:

```typescript
  private readonly allNav: NavItem[] = [
    { label: 'لوحة التحكم', route: '/app/dashboard', icon: 'M4 13h6V4H4v9Zm10 7h6v-9h-6v9ZM4 20h6v-4H4v4ZM14 4v5h6V4h-6Z', managerOnly: true },
    { label: 'المشروعات', route: '/app/projects', icon: 'M3 7h18M3 12h18M3 17h18', managerOnly: false },
    { label: 'الإعدادات', route: '/app/settings', icon: 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm7.4-3a7.4 7.4 0 0 1-.1 1.2l2.1 1.6-2 3.5-2.5-1a7.6 7.6 0 0 1-2 1.2l-.4 2.7H9.5l-.4-2.7a7.6 7.6 0 0 1-2-1.2l-2.5 1-2-3.5 2.1-1.6a7.4 7.4 0 0 1 0-2.4L2.6 8.6l2-3.5 2.5 1a7.6 7.6 0 0 1 2-1.2L9.5 2.2h5l.4 2.7a7.6 7.6 0 0 1 2 1.2l2.5-1 2 3.5-2.1 1.6c.1.4.1.8.1 1.2Z', managerOnly: false },
  ];
```

- [ ] **Step 9: Type-check and build**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
cd Frontend && npx ng build
```
Expected: no errors. Pay attention to any leftover reference to the removed `TabKey`/`TabDef` types that used to live inline in `settings.ts` — everything must now import from `settings-tabs.ts`.

- [ ] **Step 10: Manual check via browser**

With `frontend-dev` and `backend-api` running:
1. Confirm the top nav now shows only لوحة التحكم / المشروعات / الإعدادات.
2. Navigate to `/app/settings` — confirm the sidebar lists all 16 areas (12 lookup tabs + المقاولون + الجهات التنفيذية + إدارة المستخدمين (only as a manager) + القياسات).
3. Click through several tabs, confirm each loads and its create/edit/delete still works (spot-check main-programs, sub-programs with parent picker, and the new units tab).
4. Type `/app/settings/contractors` directly into the address bar — confirm it resolves (not just reachable via sidebar click).
5. Confirm `/app/contractors`, `/app/agencies`, `/app/users`, `/app/measurements` (the old top-level paths) no longer resolve (redirect to `/app/projects` via the wildcard `**` route, or 404 within the app shell).
6. Log in as a non-manager account, confirm the `إدارة المستخدمين` sidebar entry is hidden but all 15 other entries remain visible.

- [ ] **Step 11: Commit**

```bash
git add Frontend/src/app/app.config.ts Frontend/src/app/app.routes.ts Frontend/src/app/layout/main-layout/main-layout.ts Frontend/src/app/features/settings/
git commit -m "feat: consolidate Contractors/Agencies/Users/Measurements and all lookup tabs into a routed Settings shell

/app/settings becomes a parent route with 16 child routes (12 generic
lookup tabs via a new shared SettingsLookupPage + the 4 existing full
pages, unmodified beyond relocation). Top-level nav shrinks to
Dashboard/Projects/Settings."
```

---

### Task 7: Final End-to-End Verification

**Files:** none (verification only).

- [ ] **Step 1: Full regression pass in the browser**

With `frontend-dev` and `backend-api` running:
1. Login still works; role-based nav (`لوحة التحكم` manager-only) unaffected.
2. `/app/settings`: all 16 tabs reachable and functional, including the new `وحدات القياس` tab (create/edit/delete a unit; confirm deleting a unit in use by a measurement fails with a clear message).
3. `/app/settings/measurements`: create a measurement linked to 2 units and 1 sub-program via the redesigned modal; confirm the sub-program picker groups by Main Program with expand/collapse, and the table shows both new chip columns (وحدات + برامج فرعية).
4. `/app/projects`: add a sub-project under that sub-program; confirm Step 4 shows the required unit dropdown (only the 2 linked units) plus the value input; submitting a value without a unit is rejected with a clear message; record a value with a unit, save, re-open to confirm both are pre-filled.
5. Confirm the old top-level routes (`/app/contractors`, `/app/agencies`, `/app/users`, `/app/measurements`) no longer resolve, and the top nav shows only لوحة التحكم / المشروعات / الإعدادات.
6. Confirm `Users` tab is hidden from a non-manager account's Settings sidebar; all other 15 tabs remain visible for both roles.

- [ ] **Step 2: Confirm no stray console errors**

Use `read_console_messages` (`onlyErrors: true`) during the pass above.

- [ ] **Step 3: Final backend + frontend build**

```bash
cd Backend/src/SmartInvest.API && dotnet build
cd Frontend && npx ng build
```
Expected: both succeed with no errors.

- [ ] **Step 4: Final `git status`**

```bash
git status
```
Confirm only files touched by Tasks 1-6 show as modified/new.
