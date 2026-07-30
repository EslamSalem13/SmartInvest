# Agencies & Contractors as Profiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove `ExecutiveAgency`/`Contractor` as login roles (they become plain data profiles managed by planning staff), delete the now-meaningless self-service change-request workflow, expose "which sub-projects does X hold" on both profile types, add matching frontend management pages for both, and swap the projects table's agency column for a contractor column.

**Architecture:** Backend strips the `ApplicationUser` FK/role/JWT-claim plumbing from both entities and deletes the change-request subsystem outright; two new AutoMapper-populated fields (`AssignedSubProjects` on the profile DTOs, `ContractorName` on `SubProjectListItemDto`) surface the missing data. Frontend gets two new CRUD pages (same shape as the existing Users page: table + create/edit `si-modal`, no password fields) plus a one-line table header/cell swap on the existing projects page.

**Tech Stack:** .NET 10 (EF Core, AutoMapper, ASP.NET Identity), Angular 21 standalone components + Signals.

## Global Constraints

- No unit/integration test suite exists anywhere in this repo. Each task's "test" step is build/type-check, then a manual check via the browser preview tool.
- Follow existing conventions exactly: Arabic UI strings, `si-btn`/`si-modal`/`si-overlay`/`si-grid`/`si-fld`/`si-err` shared classes from `Frontend/src/styles.css`, per-component CSS duplicating generic page/table classes (this app uses default `ViewEncapsulation.Emulated`, confirmed no global equivalents exist), Signals-based state, `[ngModel]`/`(ngModelChange)` (no Reactive Forms), `AuthService.isManager` for manager-gated actions.
- Backend: class-level `[Authorize]` with no roles OR a broad role list, method-level roles explicit and narrower — never rely on class+method role intersection accidentally locking everyone out (documented pitfall in `docs/PROJECT.md`).
- Migrations: follow `docs/PROJECT.md` §9's procedure exactly — generate, inspect the raw `Up()`/`Down()` SQL for anything requiring manual ordering (an index depending on a column being altered/dropped needs an explicit `DropIndex` first, as happened with `MainProjectCode`), apply, then run the empty-probe-migration technique to verify the snapshot matches, delete the probe files.
- Never run dev servers via Bash — use the `preview_start` tool.
- **Task ordering note:** Tasks 2 and 3 were deliberately ordered so each builds and runs standalone: Task 2 adds new data-layer pieces (a DTO, two repository methods, one mapped field) that nothing calls yet; Task 3 rewrites the services that call them. Executing them out of order will not compile.

---

### Task 1: Backend — Remove the Change-Request Workflow

**Files:**
- Delete: `Backend/src/SmartInvest.Application/Services/ChangeRequestService.cs`
- Delete: `Backend/src/SmartInvest.Application/Interfaces/IChangeRequestService.cs`
- Delete: `Backend/src/SmartInvest.Application/DTOs/ChangeRequestDtos.cs`
- Delete: `Backend/src/SmartInvest.Application/Validators/CreateChangeRequestDtoValidator.cs`
- Delete: `Backend/src/SmartInvest.Domain/Entities/ProjectAssignmentChangeRequest.cs`
- Delete: `Backend/src/SmartInvest.Domain/Enums/ChangeRequestStatus.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs:17`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs:56`
- Modify: `Backend/src/SmartInvest.API/Controllers/ProjectAssignmentsController.cs`
- Create (generated): a new EF migration dropping the `ProjectAssignmentChangeRequests` table.

**Interfaces:**
- Produces: `ProjectAssignmentsController` retains only `GetAll`, `Create`, `Update`, `Delete` (no `change-requests` sub-routes). Later tasks don't depend on anything from this task beyond the controller compiling cleanly.

- [ ] **Step 1: Delete the change-request files**

```bash
rm Backend/src/SmartInvest.Application/Services/ChangeRequestService.cs
rm Backend/src/SmartInvest.Application/Interfaces/IChangeRequestService.cs
rm Backend/src/SmartInvest.Application/DTOs/ChangeRequestDtos.cs
rm Backend/src/SmartInvest.Application/Validators/CreateChangeRequestDtoValidator.cs
rm Backend/src/SmartInvest.Domain/Entities/ProjectAssignmentChangeRequest.cs
rm Backend/src/SmartInvest.Domain/Enums/ChangeRequestStatus.cs
```

- [ ] **Step 2: Remove the `DbSet` from `AppDbContext`**

In `Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs`, delete this line:

```csharp
    public DbSet<ProjectAssignmentChangeRequest> ProjectAssignmentChangeRequests => Set<ProjectAssignmentChangeRequest>();
```

- [ ] **Step 3: Remove the DI registration**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, delete this line:

```csharp
        services.AddScoped<IChangeRequestService, ChangeRequestService>();
```

- [ ] **Step 4: Remove the change-request actions from `ProjectAssignmentsController`**

In `Backend/src/SmartInvest.API/Controllers/ProjectAssignmentsController.cs`, remove the `IChangeRequestService` field/constructor param and these four actions:

```csharp
    [HttpGet("{id:int}/change-requests")]
    [Authorize(Roles = Roles.StaffAndAgency)]
    public async Task<ActionResult<IReadOnlyList<ChangeRequestDto>>> GetChangeRequests(int subProjectId, int id, CancellationToken cancellationToken)
    {
        var result = await _changeRequestService.GetHistoryAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:int}/change-requests")]
    [Authorize(Roles = Roles.AssignmentParties)]
    public async Task<ActionResult<ChangeRequestDto>> SubmitChangeRequest(int subProjectId, int id, CreateChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _changeRequestService.SubmitAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:int}/change-requests/{changeRequestId:int}/approve")]
    [Authorize(Roles = Roles.StaffAndAgency)]
    public async Task<ActionResult<ChangeRequestDto>> ApproveChangeRequest(int subProjectId, int id, int changeRequestId, ReviewChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _changeRequestService.ApproveAsync(id, changeRequestId, dto, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:int}/change-requests/{changeRequestId:int}/reject")]
    [Authorize(Roles = Roles.StaffAndAgency)]
    public async Task<ActionResult<ChangeRequestDto>> RejectChangeRequest(int subProjectId, int id, int changeRequestId, ReviewChangeRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _changeRequestService.RejectAsync(id, changeRequestId, dto, cancellationToken);
        return Ok(result);
    }
```

The constructor becomes:

```csharp
    public ProjectAssignmentsController(IProjectAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }
```

(remove the `_changeRequestService` field declaration too). Leave `GetAll`/`Create`/`Update`/`Delete` and their current `[Authorize(Roles = Roles.StaffAndAgency)]`/`Roles.PlanningManager` attributes untouched — Task 3 updates those role names once `Roles.StaffAndAgency` is redefined.

- [ ] **Step 5: Build the backend**

Run:
```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.` — if a stray `SmartInvest.API.exe` process is holding the DLL locked (this has happened repeatedly in this repo's dev loop), find and stop it first: on Windows, `Get-Process -Name SmartInvest.API | Stop-Process -Force`, then rebuild.

- [ ] **Step 6: Generate and apply the migration**

```bash
cd Backend/src/SmartInvest.API
dotnet ef migrations add RemoveProjectAssignmentChangeRequests --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```
Read the generated migration file (`Backend/src/SmartInvest.Infrastructure/Migrations/<timestamp>_RemoveProjectAssignmentChangeRequests.cs`) — expect a single `DropTable(name: "ProjectAssignmentChangeRequests")` in `Up()` and the matching `CreateTable(...)` back in `Down()`. If there's anything else (an unexpected index/FK operation elsewhere), stop and report it rather than applying blindly.

```bash
dotnet ef database update --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```
Expected: migration applies with no errors.

- [ ] **Step 7: Empty-probe verify**

```bash
dotnet ef migrations add ProbeCheck --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```
Open the generated `ProbeCheck` migration file — `Up()` and `Down()` must both be empty (just `{ }`). If they are, remove the probe:
```bash
dotnet ef migrations remove --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```
If they are NOT empty, stop and report — the snapshot doesn't match the model and needs investigation before continuing.

- [ ] **Step 8: Commit**

```bash
git add -A -- Backend/src/SmartInvest.Application/Services/ChangeRequestService.cs Backend/src/SmartInvest.Application/Interfaces/IChangeRequestService.cs Backend/src/SmartInvest.Application/DTOs/ChangeRequestDtos.cs Backend/src/SmartInvest.Application/Validators/CreateChangeRequestDtoValidator.cs Backend/src/SmartInvest.Domain/Entities/ProjectAssignmentChangeRequest.cs Backend/src/SmartInvest.Domain/Enums/ChangeRequestStatus.cs Backend/src/SmartInvest.Infrastructure/Data/AppDbContext.cs Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs Backend/src/SmartInvest.API/Controllers/ProjectAssignmentsController.cs Backend/src/SmartInvest.Infrastructure/Migrations/
git commit -m "refactor: remove the self-service change-request workflow

Only made sense when agencies/contractors could log in and act on
their own behalf; that capability is being removed in a later task."
```

---

### Task 2: Backend — "Which Sub-Projects Does X Hold" + Contractor Column Data

**Files:**
- Create: `Backend/src/SmartInvest.Application/DTOs/AssignedSubProjectDto.cs`
- Modify: `Backend/src/SmartInvest.Domain/Interfaces/ISubProjectRepository.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs`
- Modify: `Backend/src/SmartInvest.Domain/Interfaces/IProjectAssignmentRepository.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Repositories/ProjectAssignmentRepository.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/SubProjectDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Common/Mappings/SubProjectMappingProfile.cs`

**Interfaces:**
- Consumes: nothing beyond Task 1 compiling cleanly.
- Produces: `AssignedSubProjectDto`; `ISubProjectRepository.GetByExecutiveAgencyAsync(int, CancellationToken)`; `IProjectAssignmentRepository.GetByContractorAsync(int, CancellationToken)`; `SubProjectListItemDto.ContractorName: string?`. Task 3's rewritten `ExecutiveAgencyService`/`ContractorService` call the two new repository methods and populate `AssignedSubProjectDto`. Task 4 (frontend) mirrors `ContractorName` on its `SubProjectListItem` model, consumed by Task 7's table swap.

- [ ] **Step 1: Create `AssignedSubProjectDto`**

```csharp
namespace SmartInvest.Application.DTOs;

public class AssignedSubProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MainProjectName { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Add `GetByExecutiveAgencyAsync` to the sub-project repository**

In `Backend/src/SmartInvest.Domain/Interfaces/ISubProjectRepository.cs`, add:

```csharp
    Task<IReadOnlyList<SubProject>> GetByExecutiveAgencyAsync(int executiveAgencyId, CancellationToken cancellationToken = default);
```

In `Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs`, add this method (anywhere inside the class, e.g. right after `GetWithDetailsAsync`):

```csharp
    public async Task<IReadOnlyList<SubProject>> GetByExecutiveAgencyAsync(int executiveAgencyId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.MainProject)
            .Where(x => x.ExecutiveAgencyId == executiveAgencyId)
            .ToListAsync(cancellationToken);
    }
```

- [ ] **Step 3: Add `GetByContractorAsync` to the assignment repository**

In `Backend/src/SmartInvest.Domain/Interfaces/IProjectAssignmentRepository.cs`, add:

```csharp
    Task<IReadOnlyList<ProjectAssignment>> GetByContractorAsync(int contractorId, CancellationToken cancellationToken = default);
```

In `Backend/src/SmartInvest.Infrastructure/Repositories/ProjectAssignmentRepository.cs`, add:

```csharp
    public async Task<IReadOnlyList<ProjectAssignment>> GetByContractorAsync(int contractorId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.SubProject).ThenInclude(s => s.MainProject)
            .Where(x => x.ContractorId == contractorId)
            .OrderByDescending(x => x.AssignmentDate)
            .ToListAsync(cancellationToken);
    }
```

- [ ] **Step 4: Add `ContractorName` to `SubProjectListItemDto`**

In `Backend/src/SmartInvest.Application/DTOs/SubProjectDtos.cs`, find the `SubProjectListItemDto` class and add one property (placing it next to the existing agency fields):

```csharp
    public int? ExecutiveAgencyId { get; set; }
    public string? ExecutiveAgencyName { get; set; }
    public string? ContractorName { get; set; }
```

(the first two lines already exist — only `ContractorName` is new; keep everything else in the class unchanged).

- [ ] **Step 5: Map it and include the data needed**

In `Backend/src/SmartInvest.Application/Common/Mappings/SubProjectMappingProfile.cs`, in the `CreateMap<SubProject, SubProjectListItemDto>()` chain, add one more `.ForMember(...)`:

```csharp
            .ForMember(
                dest => dest.ContractorName,
                opt => opt.MapFrom(src =>
                    src.ProjectAssignments != null && src.ProjectAssignments.Any()
                        ? src.ProjectAssignments.OrderByDescending(a => a.AssignmentDate).First().Contractor.ContractorName
                        : null));
```

In `Backend/src/SmartInvest.Infrastructure/Repositories/SubProjectRepository.cs`, `SearchAsync`'s query needs the assignment+contractor data available for that projection. Find:

```csharp
        var query = DbSet
            .Include(x => x.MainProject).ThenInclude(m => m.SubProgram).ThenInclude(sp => sp.MainProgram)
            .Include(x => x.Markaz)
            .Include(x => x.Priority)
            .Include(x => x.Status)
            .Include(x => x.ExecutiveAgency)
            .AsQueryable();
```

Replace with:

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

- [ ] **Step 6: Build the backend**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 7: Manual check via Swagger**

Start `backend-api` via `preview_start`. Log in (`superadmin`/`SuperAdmin@123`) via `POST /api/auth/login`, authorize in Swagger. Confirm `GET /api/subprojects?page=1&pageSize=20` response items include a `contractorName` field — `null` for sub-projects with no assignment, and the assigned contractor's name for any that already have one via the existing (untouched) sub-project assignment flow.

- [ ] **Step 8: Commit**

```bash
git add -A -- Backend/
git commit -m "feat: add data layer for assigned-sub-projects and contractor name

New AssignedSubProjectDto, ISubProjectRepository.GetByExecutiveAgencyAsync,
IProjectAssignmentRepository.GetByContractorAsync, and
SubProjectListItemDto.ContractorName. Nothing consumes the first three
yet (Task 3 does) — this task only adds data-layer plumbing."
```

---

### Task 3: Backend — Strip Login Capability from Agencies & Contractors

**Files:**
- Modify: `Backend/src/SmartInvest.Domain/Common/Roles.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Identity/ApplicationUser.cs`
- Delete: `Backend/src/SmartInvest.Infrastructure/Data/Configurations/ApplicationUserConfiguration.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/Identity/IdentityService.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/IIdentityService.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/ICurrentUserService.cs`
- Modify: `Backend/src/SmartInvest.API/Common/CurrentUserService.cs`
- Modify: `Backend/src/SmartInvest.API/Program.cs`
- Modify: `Backend/src/SmartInvest.Domain/Entities/ExecutiveAgency.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/ExecutiveAgencyDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/ContractorDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Common/Mappings/ExecutiveAgencyMappingProfile.cs`
- Modify: `Backend/src/SmartInvest.Application/Common/Mappings/ContractorMappingProfile.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/ExecutiveAgencyService.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/ContractorService.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/IExecutiveAgencyService.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/IContractorService.cs`
- Modify: `Backend/src/SmartInvest.API/Controllers/ExecutiveAgenciesController.cs`
- Modify: `Backend/src/SmartInvest.API/Controllers/ContractorsController.cs`
- Modify: `Backend/src/SmartInvest.API/Controllers/ProjectAssignmentsController.cs` (role name update only)
- Modify: `Backend/src/SmartInvest.Application/Services/ProjectAssignmentService.cs`
- Create (generated): a new EF migration.

**Interfaces:**
- Consumes: `AssignedSubProjectDto`, `ISubProjectRepository.GetByExecutiveAgencyAsync`, `IProjectAssignmentRepository.GetByContractorAsync` (Task 2).
- Produces: `ExecutiveAgencyDto`/`ContractorDto` no longer have `UserName`; `ExecutiveAgencyDto.IsActive`/`ContractorDto.IsActive` are now plain stored columns; both DTOs' `GetById` fetch populates `AssignedSubProjects`. `CreateExecutiveAgencyDto`/`CreateContractorDto` no longer have `UserName`/`Password`. `UpdateExecutiveAgencyDto` gains `IsActive`. Task 4 (frontend) mirrors these final DTO shapes.

- [ ] **Step 1: Simplify `Roles.cs`**

Replace `Backend/src/SmartInvest.Domain/Common/Roles.cs` in full:

```csharp
namespace SmartInvest.Domain.Common;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";

    public const string PlanningEmployee = "PlanningEmployee";

    public const string PlanningManager = "PlanningManager";

    /// <summary>مدير + موظف تخطيط.</summary>
    public const string PlanningStaff = "PlanningEmployee,PlanningManager";
}
```

- [ ] **Step 2: Update every reference to the deleted grouped constants**

In `Backend/src/SmartInvest.API/Controllers/ContractorsController.cs`:
- Class-level `[Authorize(Roles = Roles.StaffAndAgency)]` → `[Authorize(Roles = Roles.PlanningStaff)]`
- `Create` action's `[Authorize(Roles = Roles.ManagerAndAgency)]` → `[Authorize(Roles = Roles.PlanningManager)]`

In `Backend/src/SmartInvest.API/Controllers/ExecutiveAgenciesController.cs`:
- Class-level `[Authorize(Roles = Roles.PlanningStaff)]` — already fine, no change (it never referenced the agency/contractor grouped constants).

In `Backend/src/SmartInvest.API/Controllers/ProjectAssignmentsController.cs`, the four remaining actions:
- `GetAll`, `Create`, `Update`: `[Authorize(Roles = Roles.StaffAndAgency)]` → `[Authorize(Roles = Roles.PlanningStaff)]`
- `Delete`: already `Roles.PlanningManager`, no change.

- [ ] **Step 3: Remove `ExecutiveAgencyId`/`ContractorId` from `ApplicationUser`**

Replace `Backend/src/SmartInvest.Infrastructure/Identity/ApplicationUser.cs` in full:

```csharp
using Microsoft.AspNetCore.Identity;

namespace SmartInvest.Infrastructure.Identity;

/// <summary>
/// Application user extending ASP.NET Core Identity.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: Delete the now-invalid `ApplicationUserConfiguration`**

```bash
rm Backend/src/SmartInvest.Infrastructure/Data/Configurations/ApplicationUserConfiguration.cs
```
(Its only content was the unique-index + FK configuration for the two columns just removed; `AppDbContext.OnModelCreating` discovers `IEntityTypeConfiguration<T>` implementations via `ApplyConfigurationsFromAssembly`, so nothing else references this file by name.)

- [ ] **Step 5: Add `ExecutiveAgency.IsActive`**

In `Backend/src/SmartInvest.Domain/Entities/ExecutiveAgency.cs`, add a field so it matches `Contractor.IsActive`:

```csharp
namespace SmartInvest.Domain.Entities
{
    public class ExecutiveAgency
    {
        [Key]
        public int ExecutiveAgencyId { get; set; }
        public string AgencyName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
```

- [ ] **Step 6: Strip `IdentityService`**

In `Backend/src/SmartInvest.Infrastructure/Identity/IdentityService.cs`, delete these methods in full: `CreateAgencyUserAsync`, `CreateContractorUserAsync`, `GetUserByExecutiveAgencyIdAsync`, `GetUserByContractorIdAsync`, `ResetPasswordForAgencyAsync`, `ResetPasswordForContractorAsync`, `DeleteUserByExecutiveAgencyIdAsync`, `DeleteUserByContractorIdAsync`.

In `GenerateJwtToken`, remove:
```csharp
        if (user.ExecutiveAgencyId.HasValue)
        {
            claims.Add(new Claim("executiveAgencyId", user.ExecutiveAgencyId.Value.ToString()));
        }

        if (user.ContractorId.HasValue)
        {
            claims.Add(new Claim("contractorId", user.ContractorId.Value.ToString()));
        }
```

- [ ] **Step 7: Update `IIdentityService`**

Replace `Backend/src/SmartInvest.Application/Interfaces/IIdentityService.cs` in full:

```csharp
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IIdentityService
{
    Task<AuthResultDto> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    Task<UserDto> CreateEmployeeAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default);

    Task SetActiveStatusAsync(string userId, bool isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 8: Strip `ICurrentUserService` / `CurrentUserService`**

Replace `Backend/src/SmartInvest.Application/Interfaces/ICurrentUserService.cs` in full:

```csharp
namespace SmartInvest.Application.Interfaces;

/// <summary>
/// يكشف هوية المستخدم الحالي (من الـ JWT) للطبقات الأعلى بدون أي اعتماد مباشر على ASP.NET Core.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }

    string? Role { get; }
}
```

Replace `Backend/src/SmartInvest.API/Common/CurrentUserService.cs` in full:

```csharp
using System.Security.Claims;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.API.Common;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Role => User?.FindFirstValue(ClaimTypes.Role);
}
```

- [ ] **Step 9: Update `ProjectAssignmentService`**

In `Backend/src/SmartInvest.Application/Services/ProjectAssignmentService.cs`, delete the `EnsureAgencyOwnership` method in full:

```csharp
    /// <summary>
    /// مدير التخطيط وموظف التخطيط لهم تجاوز كامل. الجهة التنفيذية مقصورة على مشروعاتها فقط.
    /// </summary>
    private void EnsureAgencyOwnership(SubProject subProject)
    {
        if (_currentUser.Role != Roles.ExecutiveAgency)
        {
            return;
        }

        if (subProject.ExecutiveAgencyId == null || subProject.ExecutiveAgencyId != _currentUser.ExecutiveAgencyId)
        {
            throw new ForbiddenAccessException("لا يمكنك التعامل مع تعيينات مشروع غير مسند لجهتك");
        }
    }
```

Remove its three call sites (one line each, in `GetBySubProjectAsync`, `CreateAsync`, `UpdateGeneralAsync`):
```csharp
        EnsureAgencyOwnership(subProject);
```

`ICurrentUserService _currentUser` stays injected (still used for `_currentUser.Role != Roles.PlanningManager` in the lock check) — only the ownership-check method and its call sites go.

- [ ] **Step 10: Update `Program.cs` role seed**

In `Backend/src/SmartInvest.API/Program.cs`, find:
```csharp
    string[] roles = { Roles.SuperAdmin, Roles.PlanningEmployee, Roles.PlanningManager, Roles.ExecutiveAgency, Roles.Contractor };
```
Replace with:
```csharp
    string[] roles = { Roles.SuperAdmin, Roles.PlanningEmployee, Roles.PlanningManager };
```

- [ ] **Step 11: Update the DTOs**

Replace `Backend/src/SmartInvest.Application/DTOs/ExecutiveAgencyDtos.cs` in full:

```csharp
namespace SmartInvest.Application.DTOs;

public class ExecutiveAgencyDto
{
    public int Id { get; set; }
    public string AgencyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<AssignedSubProjectDto> AssignedSubProjects { get; set; } = new();
}

public class CreateExecutiveAgencyDto
{
    public string AgencyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public class UpdateExecutiveAgencyDto
{
    public string AgencyName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
```

Replace `Backend/src/SmartInvest.Application/DTOs/ContractorDtos.cs` in full:

```csharp
namespace SmartInvest.Application.DTOs;

public class ContractorDto
{
    public int Id { get; set; }
    public string ContractorName { get; set; } = string.Empty;
    public string CompanyType { get; set; } = string.Empty;
    public string NationalIdOrCommercialRegister { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<AssignedSubProjectDto> AssignedSubProjects { get; set; } = new();
}

public class CreateContractorDto
{
    public string ContractorName { get; set; } = string.Empty;
    public string CompanyType { get; set; } = string.Empty;
    public string NationalIdOrCommercialRegister { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class UpdateContractorDto
{
    public string ContractorName { get; set; } = string.Empty;
    public string CompanyType { get; set; } = string.Empty;
    public string NationalIdOrCommercialRegister { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
```

- [ ] **Step 12: Update the mapping profiles**

Replace `Backend/src/SmartInvest.Application/Common/Mappings/ExecutiveAgencyMappingProfile.cs` in full:

```csharp
using AutoMapper;
using SmartInvest.Application.DTOs;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Application.Common.Mappings;

public class ExecutiveAgencyMappingProfile : Profile
{
    public ExecutiveAgencyMappingProfile()
    {
        CreateMap<ExecutiveAgency, ExecutiveAgencyDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ExecutiveAgencyId));
    }
}
```

Replace `Backend/src/SmartInvest.Application/Common/Mappings/ContractorMappingProfile.cs` in full:

```csharp
using AutoMapper;
using SmartInvest.Application.DTOs;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Application.Common.Mappings;

public class ContractorMappingProfile : Profile
{
    public ContractorMappingProfile()
    {
        CreateMap<Contractor, ContractorDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ContractorId));
    }
}
```

(`AssignedSubProjects` isn't mapped here — both entities have no such property; the services populate it manually after mapping, see Step 13.)

- [ ] **Step 13: Rewrite `ExecutiveAgencyService` and `ContractorService`**

Replace `Backend/src/SmartInvest.Application/Services/ExecutiveAgencyService.cs` in full:

```csharp
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
```

Replace `Backend/src/SmartInvest.Application/Services/ContractorService.cs` in full:

```csharp
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
```

- [ ] **Step 14: Update the interfaces**

Replace `Backend/src/SmartInvest.Application/Interfaces/IExecutiveAgencyService.cs` in full:

```csharp
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IExecutiveAgencyService
{
    Task<IReadOnlyList<ExecutiveAgencyDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ExecutiveAgencyDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ExecutiveAgencyDto> CreateAsync(CreateExecutiveAgencyDto dto, CancellationToken cancellationToken = default);

    Task<ExecutiveAgencyDto> UpdateAsync(int id, UpdateExecutiveAgencyDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
```

Replace `Backend/src/SmartInvest.Application/Interfaces/IContractorService.cs` in full:

```csharp
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IContractorService
{
    Task<IReadOnlyList<ContractorDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ContractorDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ContractorDto> CreateAsync(CreateContractorDto dto, CancellationToken cancellationToken = default);

    Task<ContractorDto> UpdateAsync(int id, UpdateContractorDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 15: Remove the reset-password endpoints from both controllers**

In `Backend/src/SmartInvest.API/Controllers/ExecutiveAgenciesController.cs`, delete:
```csharp
    [HttpPut("{id:int}/reset-password")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        await _agencyService.ResetPasswordAsync(id, dto.NewPassword, cancellationToken);
        return NoContent();
    }
```

In `Backend/src/SmartInvest.API/Controllers/ContractorsController.cs`, delete:
```csharp
    [HttpPut("{id:int}/reset-password")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        await _contractorService.ResetPasswordAsync(id, dto.NewPassword, cancellationToken);
        return NoContent();
    }
```

Also apply the class-level and `Create`-level role changes from Step 2 to these same two controller files while you're in them.

- [ ] **Step 16: Build the backend**

```bash
cd Backend/src/SmartInvest.API && dotnet build
```
Expected: `Build succeeded.` (this is the first task where the login-stripping and Task 2's data-layer additions meet — if it doesn't build, double check Task 2 actually landed first).

- [ ] **Step 17: Manual check via Swagger**

With `backend-api` still running (or start it via `preview_start`), log in and authorize as before. Confirm:
- `POST /api/agencies` with `{ agencyName, phone, email, address }` (no username/password) succeeds and returns `isActive: true`, `assignedSubProjects: []`.
- `POST /api/contractors` similarly — no username/password fields accepted/required.
- `GET /api/agencies/{id}` and `GET /api/contractors/{id}` on ones with existing sub-project assignments return a populated `assignedSubProjects` array.
- `PUT /api/agencies/{id}/reset-password` and `PUT /api/contractors/{id}/reset-password` both 404 (routes removed).
- Logging in as `superadmin`/`admin` (seeded `SuperAdmin`/`PlanningManager` accounts) still works — unaffected by the role-list trim.

- [ ] **Step 18: Generate and apply the migration**

Run (from `Backend/src/SmartInvest.API`):
```bash
dotnet ef migrations add RemoveAgencyContractorLogins --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```
Inspect the generated file. Expect: two `DropColumn` operations on `AspNetUsers` (`ExecutiveAgencyId`, `ContractorId`), `DropIndex` operations for their unique filtered indexes (EF should order these correctly on its own since it's dropping the whole column+index together, not altering a column in place like the `MainProjectCode` case — but read it anyway), and an `AddColumn<bool>("IsActive", table: "ExecutiveAgencies", defaultValue: true)`. If the drop-index-before-drop-column ordering is wrong or missing, fix it manually the same way the `MainProjectCode` migration was fixed (explicit `DropIndex` before the column operation it depends on).

```bash
dotnet ef database update --project ../SmartInvest.Infrastructure/SmartInvest.Infrastructure.csproj --startup-project .
```

- [ ] **Step 19: Empty-probe verify**

Same as Task 1 Step 7 — `dotnet ef migrations add ProbeCheck ...`, confirm both `Up()`/`Down()` are empty, then `dotnet ef migrations remove ...`.

- [ ] **Step 20: Commit**

```bash
git add -A -- Backend/
git commit -m "refactor: strip ExecutiveAgency/Contractor login capability

They're profiles now, not roles: no ApplicationUser link, no JWT role,
no reset-password. ExecutiveAgency gains its own IsActive column
(previously derived from the login user that no longer exists), and
both profile types now expose AssignedSubProjects on their GetById
fetch (built on Task 2's repository methods)."
```

---

### Task 4: Frontend — Models and Services

**Files:**
- Modify: `Frontend/src/app/core/models/project.models.ts`
- Create: `Frontend/src/app/core/services/contractors.service.ts`
- Create: `Frontend/src/app/core/services/agencies.service.ts`
- Modify: `Frontend/src/app/core/models/auth.models.ts`
- Modify: `Frontend/src/app/features/users/users.ts`

**Interfaces:**
- Produces: `Contractor`, `CreateContractor`, `UpdateContractor`, `ExecutiveAgencyProfile`, `CreateAgency`, `UpdateAgency`, `AssignedSubProject` types; `ContractorsService`, `AgenciesService` with `getAll/getById/create/update/delete`. Tasks 5 and 6 consume these directly.

- [ ] **Step 1: Add the models**

In `Frontend/src/app/core/models/project.models.ts`, add at the end of the file:

```ts
export interface AssignedSubProject {
  id: number;
  name: string;
  mainProjectName: string;
}

export interface Contractor {
  id: number;
  contractorName: string;
  companyType: string;
  nationalIdOrCommercialRegister: string;
  phoneNumber: string;
  email: string;
  address: string;
  category: string;
  isActive: boolean;
  assignedSubProjects: AssignedSubProject[];
}

export interface CreateContractor {
  contractorName: string;
  companyType: string;
  nationalIdOrCommercialRegister: string;
  phoneNumber: string;
  email: string;
  address: string;
  category: string;
}

export interface UpdateContractor extends CreateContractor {
  isActive: boolean;
}

export interface ExecutiveAgencyProfile {
  id: number;
  agencyName: string;
  phone: string;
  email: string;
  address: string;
  isActive: boolean;
  assignedSubProjects: AssignedSubProject[];
}

export interface CreateAgency {
  agencyName: string;
  phone: string;
  email: string;
  address: string;
}

export interface UpdateAgency extends CreateAgency {
  isActive: boolean;
}
```

(named `ExecutiveAgencyProfile`, not `ExecutiveAgency`, to avoid any confusion with `MainProjectListItem.executingAgency`/`EXECUTING_AGENCIES` — those are a separate, untouched, free-text concept per the design's out-of-scope note.)

Also add `contractorName: string | null;` to the existing `SubProjectListItem` interface (find it in the same file, add next to `executiveAgencyName`):

```ts
  executiveAgencyId: number | null;
  executiveAgencyName: string | null;
  contractorName: string | null;
```

(the first two already exist — only `contractorName` is new).

- [ ] **Step 2: Create `ContractorsService`**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Contractor, CreateContractor, UpdateContractor } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class ContractorsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/contractors`;

  getAll(): Observable<Contractor[]> {
    return this.http.get<Contractor[]>(this.base);
  }

  getById(id: number): Observable<Contractor> {
    return this.http.get<Contractor>(`${this.base}/${id}`);
  }

  create(dto: CreateContractor): Observable<Contractor> {
    return this.http.post<Contractor>(this.base, dto);
  }

  update(id: number, dto: UpdateContractor): Observable<Contractor> {
    return this.http.put<Contractor>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
```

- [ ] **Step 3: Create `AgenciesService`**

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateAgency, ExecutiveAgencyProfile, UpdateAgency } from '../models/project.models';

@Injectable({ providedIn: 'root' })
export class AgenciesService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/agencies`;

  getAll(): Observable<ExecutiveAgencyProfile[]> {
    return this.http.get<ExecutiveAgencyProfile[]>(this.base);
  }

  getById(id: number): Observable<ExecutiveAgencyProfile> {
    return this.http.get<ExecutiveAgencyProfile>(`${this.base}/${id}`);
  }

  create(dto: CreateAgency): Observable<ExecutiveAgencyProfile> {
    return this.http.post<ExecutiveAgencyProfile>(this.base, dto);
  }

  update(id: number, dto: UpdateAgency): Observable<ExecutiveAgencyProfile> {
    return this.http.put<ExecutiveAgencyProfile>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
```

- [ ] **Step 4: Remove the deleted roles from the frontend constant**

In `Frontend/src/app/core/models/auth.models.ts`, replace:

```ts
export const Roles = {
  SuperAdmin: 'SuperAdmin',
  PlanningManager: 'PlanningManager',
  PlanningEmployee: 'PlanningEmployee',
  ExecutiveAgency: 'ExecutiveAgency',
  Contractor: 'Contractor',
} as const;
```

with:

```ts
export const Roles = {
  SuperAdmin: 'SuperAdmin',
  PlanningManager: 'PlanningManager',
  PlanningEmployee: 'PlanningEmployee',
} as const;
```

- [ ] **Step 5: Remove the dead role-label branches in the Users page**

In `Frontend/src/app/features/users/users.ts`, find `roleLabel(role: string)` and remove the two `ExecutiveAgency`/`Contractor` cases:

```ts
  protected roleLabel(role: string): string {
    switch (role) {
      case Roles.SuperAdmin:
        return 'سوبر أدمن';
      case Roles.PlanningManager:
        return 'مدير التخطيط';
      default:
        return 'موظف تخطيط';
    }
  }
```

- [ ] **Step 6: Type-check the frontend**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 7: Commit**

```bash
git add Frontend/src/app/core/models/project.models.ts Frontend/src/app/core/services/contractors.service.ts Frontend/src/app/core/services/agencies.service.ts Frontend/src/app/core/models/auth.models.ts Frontend/src/app/features/users/users.ts
git commit -m "feat: add Contractor/ExecutiveAgency frontend models and services"
```

---

### Task 5: Frontend — Contractors Page

**Files:**
- Create: `Frontend/src/app/features/contractors/contractors.ts`
- Create: `Frontend/src/app/features/contractors/contractors.html`
- Create: `Frontend/src/app/features/contractors/contractors.css`
- Modify: `Frontend/src/app/app.routes.ts`
- Modify: `Frontend/src/app/layout/main-layout/main-layout.ts`

**Interfaces:**
- Consumes: `ContractorsService` (Task 4), `Contractor`/`CreateContractor`/`UpdateContractor`/`AssignedSubProject` (Task 4), `AuthService.isManager` (existing).
- Produces: route `/app/contractors`.

- [ ] **Step 1: Create `contractors.ts`**

```ts
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ContractorsService } from '../../core/services/contractors.service';
import { AuthService } from '../../core/services/auth.service';
import { Contractor, CreateContractor } from '../../core/models/project.models';

type StatusFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'app-contractors',
  imports: [FormsModule, RouterLink],
  templateUrl: './contractors.html',
  styleUrl: './contractors.css',
})
export class Contractors {
  private readonly contractorsService = inject(ContractorsService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly contractors = signal<Contractor[]>([]);
  protected readonly search = signal('');
  protected readonly statusFilter = signal<StatusFilter>('all');
  protected readonly expandedIds = signal<Set<number>>(new Set());

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const sf = this.statusFilter();
    return this.contractors().filter((c) => {
      const matchTerm =
        !term ||
        c.contractorName.toLowerCase().includes(term) ||
        c.category.toLowerCase().includes(term) ||
        c.phoneNumber.toLowerCase().includes(term);
      const matchStatus = sf === 'all' || (sf === 'active' ? c.isActive : !c.isActive);
      return matchTerm && matchStatus;
    });
  });

  protected readonly total = computed(() => this.contractors().length);
  protected readonly activeCount = computed(() => this.contractors().filter((c) => c.isActive).length);
  protected readonly inactiveCount = computed(() => this.contractors().filter((c) => !c.isActive).length);

  // ===== pagination =====
  protected readonly page = signal(1);
  protected readonly pageSize = 8;
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));
  protected readonly paged = computed(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.filtered().slice(start, start + this.pageSize);
  });
  protected readonly rangeStart = computed(() =>
    this.filtered().length === 0 ? 0 : (this.page() - 1) * this.pageSize + 1,
  );
  protected readonly rangeEnd = computed(() => Math.min(this.page() * this.pageSize, this.filtered().length));

  protected goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages()) {
      this.page.set(p);
    }
  }

  // ===== expand/collapse assigned sub-projects =====
  protected toggleExpand(c: Contractor, event: Event): void {
    event.stopPropagation();
    const next = new Set(this.expandedIds());
    if (next.has(c.id)) {
      next.delete(c.id);
      this.expandedIds.set(next);
      return;
    }
    next.add(c.id);
    this.expandedIds.set(next);
    if (c.assignedSubProjects.length === 0) {
      this.loadDetail(c.id);
    }
  }

  protected isExpanded(id: number): boolean {
    return this.expandedIds().has(id);
  }

  private loadDetail(id: number): void {
    this.contractorsService.getById(id).subscribe({
      next: (full) => {
        this.contractors.update((list) => list.map((c) => (c.id === id ? full : c)));
      },
      error: () => {},
    });
  }

  // ===== add/edit form =====
  protected readonly showForm = signal(false);
  protected readonly editing = signal<Contractor | null>(null);
  protected readonly fContractorName = signal('');
  protected readonly fCompanyType = signal('');
  protected readonly fNationalId = signal('');
  protected readonly fPhone = signal('');
  protected readonly fEmail = signal('');
  protected readonly fAddress = signal('');
  protected readonly fCategory = signal('');
  protected readonly fIsActive = signal(true);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  constructor() {
    this.load();
    effect(() => {
      this.search();
      this.statusFilter();
      this.page.set(1);
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.contractorsService.getAll().subscribe({
      next: (data) => {
        this.contractors.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل المقاولين. تأكد من تسجيل الدخول.');
        this.loading.set(false);
      },
    });
  }

  protected openAddForm(): void {
    this.editing.set(null);
    this.fContractorName.set('');
    this.fCompanyType.set('');
    this.fNationalId.set('');
    this.fPhone.set('');
    this.fEmail.set('');
    this.fAddress.set('');
    this.fCategory.set('');
    this.fIsActive.set(true);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected openEditForm(c: Contractor, event: Event): void {
    event.stopPropagation();
    this.editing.set(c);
    this.fContractorName.set(c.contractorName);
    this.fCompanyType.set(c.companyType);
    this.fNationalId.set(c.nationalIdOrCommercialRegister);
    this.fPhone.set(c.phoneNumber);
    this.fEmail.set(c.email);
    this.fAddress.set(c.address);
    this.fCategory.set(c.category);
    this.fIsActive.set(c.isActive);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
  }

  protected submitForm(): void {
    if (this.saving()) return;
    this.formError.set(null);

    if (!this.fContractorName().trim()) {
      this.formError.set('اسم المقاول مطلوب');
      return;
    }

    const base: CreateContractor = {
      contractorName: this.fContractorName().trim(),
      companyType: this.fCompanyType().trim(),
      nationalIdOrCommercialRegister: this.fNationalId().trim(),
      phoneNumber: this.fPhone().trim(),
      email: this.fEmail().trim(),
      address: this.fAddress().trim(),
      category: this.fCategory().trim(),
    };

    this.saving.set(true);
    const editing = this.editing();
    const req = editing
      ? this.contractorsService.update(editing.id, { ...base, isActive: this.fIsActive() })
      : this.contractorsService.create(base);

    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message ?? 'تعذّر حفظ بيانات المقاول');
      },
    });
  }

  protected deleteContractor(c: Contractor, event: Event): void {
    event.stopPropagation();
    if (!confirm(`تأكيد حذف المقاول «${c.contractorName}»؟`)) return;
    this.contractorsService.delete(c.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر حذف المقاول'),
    });
  }
}
```

- [ ] **Step 2: Create `contractors.html`**

```html
<div class="page">
  <header class="page-head">
    <div>
      <h1>المقاولون</h1>
      <p>ملفات المقاولين وربطهم بالمشروعات الفرعية</p>
    </div>
    @if (isManager()) {
      <button class="si-btn gold" (click)="openAddForm()">
        <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12h14" /></svg>
        إضافة مقاول جديد
      </button>
    }
  </header>

  <div class="kpis">
    <div class="kpi"><span class="lab">إجمالي المقاولين</span><b class="val tnum">{{ total() }}</b></div>
    <div class="kpi ok"><span class="lab">نشط</span><b class="val tnum">{{ activeCount() }}</b></div>
    <div class="kpi bad"><span class="lab">غير نشط</span><b class="val tnum">{{ inactiveCount() }}</b></div>
  </div>

  <div class="toolbar">
    <div class="search">
      <svg viewBox="0 0 24 24" width="17" fill="none" stroke="currentColor" stroke-width="1.9"><circle cx="11" cy="11" r="7" /><path d="m21 21-4.3-4.3" /></svg>
      <input placeholder="البحث بالاسم أو الفئة أو رقم الهاتف…" [ngModel]="search()" (ngModelChange)="search.set($event)" />
    </div>
    <div class="seg">
      <button [class.on]="statusFilter() === 'all'" (click)="statusFilter.set('all')">الكل</button>
      <button [class.on]="statusFilter() === 'active'" (click)="statusFilter.set('active')">نشط</button>
      <button [class.on]="statusFilter() === 'inactive'" (click)="statusFilter.set('inactive')">غير نشط</button>
    </div>
  </div>

  @if (loading()) {
    <div class="state"><span class="spinner"></span> جاري تحميل المقاولين…</div>
  } @else if (error()) {
    <div class="state error">{{ error() }} <button class="si-btn" (click)="load()">إعادة المحاولة</button></div>
  } @else if (filtered().length === 0) {
    <div class="state">لا يوجد مقاولون مطابقون.</div>
  } @else {
    <div class="card">
      <div class="tbl-wrap">
        <table>
          <thead>
            <tr>
              <th>اسم المقاول</th>
              <th>نوع الشركة</th>
              <th>الفئة</th>
              <th>الهاتف</th>
              <th>الحالة</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            @for (c of paged(); track c.id) {
              <tr class="main-row" tabindex="0" role="button" (click)="toggleExpand(c, $event)">
                <td>
                  <svg class="chevron" [class.open]="isExpanded(c.id)" viewBox="0 0 24 24" width="13" fill="none" stroke="currentColor" stroke-width="2"><path d="m9 6 6 6-6 6" /></svg>
                  <b>{{ c.contractorName }}</b>
                </td>
                <td>{{ c.companyType }}</td>
                <td>{{ c.category }}</td>
                <td class="tnum">{{ c.phoneNumber }}</td>
                <td><span class="pill" [class.ok]="c.isActive">{{ c.isActive ? 'نشط' : 'غير نشط' }}</span></td>
                <td>
                  @if (isManager()) {
                    <div class="acts">
                      <button class="act" title="تعديل" (click)="openEditForm(c, $event)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" /></svg></button>
                      <button class="act danger" title="حذف" (click)="deleteContractor(c, $event)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 7h16M9 7V5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2m3 0-1 13a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 7" /></svg></button>
                    </div>
                  }
                </td>
              </tr>
              @if (isExpanded(c.id)) {
                <tr class="sub-row">
                  <td colspan="6">
                    @if (c.assignedSubProjects.length === 0) {
                      <span class="empty-subs">لا توجد مشروعات فرعية مسندة إلى هذا المقاول.</span>
                    } @else {
                      <div class="assigned-list">
                        @for (sp of c.assignedSubProjects; track sp.id) {
                          <a [routerLink]="['/app/projects', sp.id]" (click)="$event.stopPropagation()">
                            {{ sp.name }} <span class="muted">— {{ sp.mainProjectName }}</span>
                          </a>
                        }
                      </div>
                    }
                  </td>
                </tr>
              }
            }
          </tbody>
        </table>
      </div>
      <div class="tblfoot">
        <span class="muted">عرض {{ rangeStart() }}–{{ rangeEnd() }} من أصل {{ filtered().length }} مقاول</span>
        <div class="pager">
          <button (click)="goToPage(page() - 1)" [disabled]="page() === 1">‹</button>
          @for (p of [].constructor(totalPages()); track $index) {
            <button [class.on]="page() === $index + 1" (click)="goToPage($index + 1)">{{ $index + 1 }}</button>
          }
          <button (click)="goToPage(page() + 1)" [disabled]="page() === totalPages()">›</button>
        </div>
      </div>
    </div>
  }

  @if (showForm()) {
    <div class="si-overlay" (click)="closeForm()">
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(560px,100%)">
        <div class="si-modal-head">
          <div class="grow"><h3>{{ editing() ? 'تعديل مقاول' : 'إضافة مقاول جديد' }}</h3><p>بيانات ملف المقاول</p></div>
          <button class="si-x" (click)="closeForm()" aria-label="إغلاق">×</button>
        </div>
        <div class="si-modal-body">
          @if (formError()) { <div class="si-err">{{ formError() }}</div> }
          <div class="si-grid">
            <div class="si-fld full"><label>اسم المقاول <span class="req">*</span></label><input [ngModel]="fContractorName()" (ngModelChange)="fContractorName.set($event)" placeholder="اسم المقاول أو الشركة" /></div>
            <div class="si-fld"><label>نوع الشركة</label><input [ngModel]="fCompanyType()" (ngModelChange)="fCompanyType.set($event)" placeholder="فردية / مساهمة …" /></div>
            <div class="si-fld"><label>الفئة</label><input [ngModel]="fCategory()" (ngModelChange)="fCategory.set($event)" placeholder="أولى / ثانية …" /></div>
            <div class="si-fld"><label>الرقم القومي / السجل التجاري</label><input [ngModel]="fNationalId()" (ngModelChange)="fNationalId.set($event)" /></div>
            <div class="si-fld"><label>الهاتف</label><input [ngModel]="fPhone()" (ngModelChange)="fPhone.set($event)" placeholder="01XXXXXXXXX" /></div>
            <div class="si-fld full"><label>البريد الإلكتروني</label><input type="email" [ngModel]="fEmail()" (ngModelChange)="fEmail.set($event)" /></div>
            <div class="si-fld full"><label>العنوان</label><input [ngModel]="fAddress()" (ngModelChange)="fAddress.set($event)" /></div>
            @if (editing()) {
              <div class="si-fld">
                <label>الحالة</label>
                <select [ngModel]="fIsActive()" (ngModelChange)="fIsActive.set($event)">
                  <option [ngValue]="true">نشط</option>
                  <option [ngValue]="false">غير نشط</option>
                </select>
              </div>
            }
          </div>
        </div>
        <div class="si-modal-foot">
          <button class="si-btn primary" [disabled]="saving()" (click)="submitForm()">
            @if (saving()) { جاري الحفظ… } @else { {{ editing() ? 'حفظ التعديلات' : 'إضافة المقاول' }} }
          </button>
          <button class="si-btn" (click)="closeForm()">إلغاء</button>
        </div>
      </div>
    </div>
  }
</div>
```

- [ ] **Step 3: Create `contractors.css`**

```css
.page { padding: 24px 28px; }

.page-head { display: flex; align-items: center; gap: 14px; margin-bottom: 20px; }
.page-head h1 { font-size: 22px; }
.page-head p { margin: 2px 0 0; color: var(--muted); font-size: 13px; }
.page-head .si-btn { margin-inline-start: auto; }

.kpis { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin-bottom: 16px; }
@media (max-width: 760px) { .kpis { grid-template-columns: repeat(2, 1fr); } }
.kpi { background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius); padding: 15px; box-shadow: var(--shadow); position: relative; overflow: hidden; }
.kpi::before { content: ""; position: absolute; inset-block: 0; inset-inline-start: 0; width: 4px; background: var(--green-600); }
.kpi.ok::before { background: var(--ok); } .kpi.bad::before { background: var(--bad); }
.kpi .lab { color: var(--muted); font-size: 12px; font-weight: 700; }
.kpi .val { display: block; font-size: 23px; font-weight: 800; margin-top: 4px; }

.toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; flex-wrap: wrap; }
.search { flex: 1; min-width: 220px; display: flex; align-items: center; gap: 9px; background: var(--surface); border: 1px solid var(--line); border-radius: 11px; padding: 11px 13px; color: var(--muted); box-shadow: var(--shadow); }
.search input { border: 0; background: transparent; flex: 1; font-family: inherit; font-size: 13.5px; color: var(--ink); outline: none; }
.seg { display: flex; background: var(--surface); border: 1px solid var(--line); border-radius: 11px; padding: 4px; }
.seg button { border: 0; background: transparent; color: var(--muted); padding: 8px 16px; border-radius: 8px; font-weight: 700; font-size: 12.5px; }
.seg button.on { background: var(--green-700); color: #fff; }

.state { display: flex; align-items: center; justify-content: center; gap: 12px; background: var(--surface); border: 1px dashed var(--line-strong); border-radius: var(--radius); padding: 40px; color: var(--muted); box-shadow: var(--shadow); flex-wrap: wrap; }
.state.error { color: #b32a39; border-color: #F3C6CC; background: var(--bad-bg); }
.spinner { width: 16px; height: 16px; border: 2px solid var(--line-strong); border-top-color: var(--green-700); border-radius: 50%; animation: spin .7s linear infinite; display: inline-block; }
@keyframes spin { to { transform: rotate(360deg); } }

.card { background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius); box-shadow: var(--shadow); overflow: hidden; }
.tbl-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; min-width: 700px; }
th { font-size: 11.5px; color: var(--muted); font-weight: 700; text-align: start; padding: 12px 14px; border-bottom: 1px solid var(--line); white-space: nowrap; background: var(--surface-2); }
td { padding: 12px 14px; border-bottom: 1px solid var(--line); font-size: 13px; white-space: nowrap; }

tr.main-row { cursor: pointer; }
tr.main-row:hover td { background: #FAFCFA; }
.chevron { transition: transform 0.2s ease; vertical-align: middle; margin-inline-end: 6px; color: var(--green-700); }
.chevron.open { transform: rotate(90deg); }

tr.sub-row td { background: var(--surface-2); white-space: normal; }
.empty-subs { color: var(--muted); font-size: 12.5px; }
.assigned-list { display: flex; flex-direction: column; gap: 6px; }
.assigned-list a { color: var(--green-700); font-weight: 700; text-decoration: none; font-size: 13px; }
.assigned-list a:hover { text-decoration: underline; }
.assigned-list .muted { color: var(--muted); font-weight: 400; }

.pill { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 999px; font-size: 12px; font-weight: 800; background: var(--bad-bg); color: #b32a39; }
.pill.ok { background: var(--ok-bg); color: #0f7a41; }
.pill::before { content: ""; width: 7px; height: 7px; border-radius: 50%; background: currentColor; }

.tblfoot { display: flex; align-items: center; gap: 12px; padding: 12px 16px; border-top: 1px solid var(--line); }
.tblfoot .muted { color: var(--muted); font-size: 12.5px; flex: 1; }
.pager { display: flex; gap: 5px; flex-wrap: wrap; }
.pager button { min-width: 34px; height: 34px; border-radius: 8px; border: 1px solid var(--line); background: var(--surface); font-weight: 700; color: var(--ink); }
.pager button.on { background: var(--green-700); color: #fff; border-color: var(--green-700); }
.pager button:disabled { opacity: .5; cursor: default; }

.acts { display: inline-flex; gap: 6px; }
.act { width: 32px; height: 32px; border-radius: 8px; border: 1px solid var(--line); background: var(--surface); display: inline-grid; place-items: center; color: var(--muted); }
.act svg { width: 15px; height: 15px; }
.act.danger { color: var(--bad); border-color: #F3C6CC; }
.act.danger:hover { background: var(--bad-bg); }
```

- [ ] **Step 4: Wire the route**

In `Frontend/src/app/app.routes.ts`, add inside the `app` route's `children` array (anywhere among the existing siblings, e.g. right after the `users` entry):

```ts
      {
        path: 'contractors',
        loadComponent: () =>
          import('./features/contractors/contractors').then((m) => m.Contractors),
      },
```

- [ ] **Step 5: Add the nav item**

In `Frontend/src/app/layout/main-layout/main-layout.ts`, in the `allNav` array, add one entry (all-staff visible per the approved design — `managerOnly: false`):

```ts
    { label: 'المقاولون', route: '/app/contractors', icon: 'M3 21h18M5 21V7l7-4 7 4v14M9 21v-6h6v6', managerOnly: false },
```

- [ ] **Step 6: Type-check**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 7: Manual check in the browser**

Start `frontend-dev` and `backend-api`. Log in as `admin`/`Admin@123`. Navigate to `/app/contractors` (via the new nav link). Confirm: list loads (empty state if none exist yet), "إضافة مقاول جديد" opens the modal with no username/password fields, creating one succeeds and appears in the list, clicking a row expands to show "لا توجد مشروعات فرعية مسندة" (or a real one if you assign it via the existing sub-project-details assignment flow first), edit/delete work, both action buttons are hidden for a non-manager (verify by reading `contractors.html`'s `@if (isManager())` guards rather than needing a second seeded account).

- [ ] **Step 8: Commit**

```bash
git add Frontend/src/app/features/contractors/ Frontend/src/app/app.routes.ts Frontend/src/app/layout/main-layout/main-layout.ts
git commit -m "feat: add Contractors management page"
```

---

### Task 6: Frontend — Executive Agencies Page

**Files:**
- Create: `Frontend/src/app/features/agencies/agencies.ts`
- Create: `Frontend/src/app/features/agencies/agencies.html`
- Create: `Frontend/src/app/features/agencies/agencies.css`
- Modify: `Frontend/src/app/app.routes.ts`
- Modify: `Frontend/src/app/layout/main-layout/main-layout.ts`

**Interfaces:**
- Consumes: `AgenciesService`, `ExecutiveAgencyProfile`/`CreateAgency`/`UpdateAgency`/`AssignedSubProject` (Task 4).
- Produces: route `/app/agencies`.

- [ ] **Step 1: Create `agencies.ts`**

```ts
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AgenciesService } from '../../core/services/agencies.service';
import { AuthService } from '../../core/services/auth.service';
import { CreateAgency, ExecutiveAgencyProfile } from '../../core/models/project.models';

type StatusFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'app-agencies',
  imports: [FormsModule, RouterLink],
  templateUrl: './agencies.html',
  styleUrl: './agencies.css',
})
export class Agencies {
  private readonly agenciesService = inject(AgenciesService);
  private readonly auth = inject(AuthService);

  protected readonly isManager = this.auth.isManager;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly agencies = signal<ExecutiveAgencyProfile[]>([]);
  protected readonly search = signal('');
  protected readonly statusFilter = signal<StatusFilter>('all');
  protected readonly expandedIds = signal<Set<number>>(new Set());

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const sf = this.statusFilter();
    return this.agencies().filter((a) => {
      const matchTerm = !term || a.agencyName.toLowerCase().includes(term) || a.phone.toLowerCase().includes(term);
      const matchStatus = sf === 'all' || (sf === 'active' ? a.isActive : !a.isActive);
      return matchTerm && matchStatus;
    });
  });

  protected readonly total = computed(() => this.agencies().length);
  protected readonly activeCount = computed(() => this.agencies().filter((a) => a.isActive).length);
  protected readonly inactiveCount = computed(() => this.agencies().filter((a) => !a.isActive).length);

  // ===== pagination =====
  protected readonly page = signal(1);
  protected readonly pageSize = 8;
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));
  protected readonly paged = computed(() => {
    const start = (this.page() - 1) * this.pageSize;
    return this.filtered().slice(start, start + this.pageSize);
  });
  protected readonly rangeStart = computed(() =>
    this.filtered().length === 0 ? 0 : (this.page() - 1) * this.pageSize + 1,
  );
  protected readonly rangeEnd = computed(() => Math.min(this.page() * this.pageSize, this.filtered().length));

  protected goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages()) {
      this.page.set(p);
    }
  }

  // ===== expand/collapse assigned sub-projects =====
  protected toggleExpand(a: ExecutiveAgencyProfile, event: Event): void {
    event.stopPropagation();
    const next = new Set(this.expandedIds());
    if (next.has(a.id)) {
      next.delete(a.id);
      this.expandedIds.set(next);
      return;
    }
    next.add(a.id);
    this.expandedIds.set(next);
    if (a.assignedSubProjects.length === 0) {
      this.loadDetail(a.id);
    }
  }

  protected isExpanded(id: number): boolean {
    return this.expandedIds().has(id);
  }

  private loadDetail(id: number): void {
    this.agenciesService.getById(id).subscribe({
      next: (full) => {
        this.agencies.update((list) => list.map((a) => (a.id === id ? full : a)));
      },
      error: () => {},
    });
  }

  // ===== add/edit form =====
  protected readonly showForm = signal(false);
  protected readonly editing = signal<ExecutiveAgencyProfile | null>(null);
  protected readonly fAgencyName = signal('');
  protected readonly fPhone = signal('');
  protected readonly fEmail = signal('');
  protected readonly fAddress = signal('');
  protected readonly fIsActive = signal(true);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  constructor() {
    this.load();
    effect(() => {
      this.search();
      this.statusFilter();
      this.page.set(1);
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.agenciesService.getAll().subscribe({
      next: (data) => {
        this.agencies.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('تعذّر تحميل الجهات التنفيذية. تأكد من تسجيل الدخول.');
        this.loading.set(false);
      },
    });
  }

  protected openAddForm(): void {
    this.editing.set(null);
    this.fAgencyName.set('');
    this.fPhone.set('');
    this.fEmail.set('');
    this.fAddress.set('');
    this.fIsActive.set(true);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected openEditForm(a: ExecutiveAgencyProfile, event: Event): void {
    event.stopPropagation();
    this.editing.set(a);
    this.fAgencyName.set(a.agencyName);
    this.fPhone.set(a.phone);
    this.fEmail.set(a.email);
    this.fAddress.set(a.address);
    this.fIsActive.set(a.isActive);
    this.formError.set(null);
    this.showForm.set(true);
  }

  protected closeForm(): void {
    this.showForm.set(false);
  }

  protected submitForm(): void {
    if (this.saving()) return;
    this.formError.set(null);

    if (!this.fAgencyName().trim()) {
      this.formError.set('اسم الجهة مطلوب');
      return;
    }

    const base: CreateAgency = {
      agencyName: this.fAgencyName().trim(),
      phone: this.fPhone().trim(),
      email: this.fEmail().trim(),
      address: this.fAddress().trim(),
    };

    this.saving.set(true);
    const editing = this.editing();
    const req = editing
      ? this.agenciesService.update(editing.id, { ...base, isActive: this.fIsActive() })
      : this.agenciesService.create(base);

    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.message ?? 'تعذّر حفظ بيانات الجهة');
      },
    });
  }

  protected deleteAgency(a: ExecutiveAgencyProfile, event: Event): void {
    event.stopPropagation();
    if (!confirm(`تأكيد حذف الجهة «${a.agencyName}»؟`)) return;
    this.agenciesService.delete(a.id).subscribe({
      next: () => this.load(),
      error: (err) => alert(err?.error?.message ?? 'تعذّر حذف الجهة'),
    });
  }
}
```

- [ ] **Step 2: Create `agencies.html`**

```html
<div class="page">
  <header class="page-head">
    <div>
      <h1>الجهات التنفيذية</h1>
      <p>ملفات الجهات التنفيذية والمشروعات الفرعية المسندة إليها</p>
    </div>
    @if (isManager()) {
      <button class="si-btn gold" (click)="openAddForm()">
        <svg viewBox="0 0 24 24" width="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12h14" /></svg>
        إضافة جهة جديدة
      </button>
    }
  </header>

  <div class="kpis">
    <div class="kpi"><span class="lab">إجمالي الجهات</span><b class="val tnum">{{ total() }}</b></div>
    <div class="kpi ok"><span class="lab">نشط</span><b class="val tnum">{{ activeCount() }}</b></div>
    <div class="kpi bad"><span class="lab">غير نشط</span><b class="val tnum">{{ inactiveCount() }}</b></div>
  </div>

  <div class="toolbar">
    <div class="search">
      <svg viewBox="0 0 24 24" width="17" fill="none" stroke="currentColor" stroke-width="1.9"><circle cx="11" cy="11" r="7" /><path d="m21 21-4.3-4.3" /></svg>
      <input placeholder="البحث بالاسم أو الهاتف…" [ngModel]="search()" (ngModelChange)="search.set($event)" />
    </div>
    <div class="seg">
      <button [class.on]="statusFilter() === 'all'" (click)="statusFilter.set('all')">الكل</button>
      <button [class.on]="statusFilter() === 'active'" (click)="statusFilter.set('active')">نشط</button>
      <button [class.on]="statusFilter() === 'inactive'" (click)="statusFilter.set('inactive')">غير نشط</button>
    </div>
  </div>

  @if (loading()) {
    <div class="state"><span class="spinner"></span> جاري تحميل الجهات التنفيذية…</div>
  } @else if (error()) {
    <div class="state error">{{ error() }} <button class="si-btn" (click)="load()">إعادة المحاولة</button></div>
  } @else if (filtered().length === 0) {
    <div class="state">لا توجد جهات تنفيذية مطابقة.</div>
  } @else {
    <div class="card">
      <div class="tbl-wrap">
        <table>
          <thead>
            <tr>
              <th>اسم الجهة</th>
              <th>الهاتف</th>
              <th>البريد الإلكتروني</th>
              <th>الحالة</th>
              <th>إجراءات</th>
            </tr>
          </thead>
          <tbody>
            @for (a of paged(); track a.id) {
              <tr class="main-row" tabindex="0" role="button" (click)="toggleExpand(a, $event)">
                <td>
                  <svg class="chevron" [class.open]="isExpanded(a.id)" viewBox="0 0 24 24" width="13" fill="none" stroke="currentColor" stroke-width="2"><path d="m9 6 6 6-6 6" /></svg>
                  <b>{{ a.agencyName }}</b>
                </td>
                <td class="tnum">{{ a.phone }}</td>
                <td>{{ a.email }}</td>
                <td><span class="pill" [class.ok]="a.isActive">{{ a.isActive ? 'نشط' : 'غير نشط' }}</span></td>
                <td>
                  @if (isManager()) {
                    <div class="acts">
                      <button class="act" title="تعديل" (click)="openEditForm(a, $event)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 20h9M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" /></svg></button>
                      <button class="act danger" title="حذف" (click)="deleteAgency(a, $event)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M4 7h16M9 7V5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2m3 0-1 13a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 7" /></svg></button>
                    </div>
                  }
                </td>
              </tr>
              @if (isExpanded(a.id)) {
                <tr class="sub-row">
                  <td colspan="5">
                    @if (a.assignedSubProjects.length === 0) {
                      <span class="empty-subs">لا توجد مشروعات فرعية مسندة إلى هذه الجهة.</span>
                    } @else {
                      <div class="assigned-list">
                        @for (sp of a.assignedSubProjects; track sp.id) {
                          <a [routerLink]="['/app/projects', sp.id]" (click)="$event.stopPropagation()">
                            {{ sp.name }} <span class="muted">— {{ sp.mainProjectName }}</span>
                          </a>
                        }
                      </div>
                    }
                  </td>
                </tr>
              }
            }
          </tbody>
        </table>
      </div>
      <div class="tblfoot">
        <span class="muted">عرض {{ rangeStart() }}–{{ rangeEnd() }} من أصل {{ filtered().length }} جهة</span>
        <div class="pager">
          <button (click)="goToPage(page() - 1)" [disabled]="page() === 1">‹</button>
          @for (p of [].constructor(totalPages()); track $index) {
            <button [class.on]="page() === $index + 1" (click)="goToPage($index + 1)">{{ $index + 1 }}</button>
          }
          <button (click)="goToPage(page() + 1)" [disabled]="page() === totalPages()">›</button>
        </div>
      </div>
    </div>
  }

  @if (showForm()) {
    <div class="si-overlay" (click)="closeForm()">
      <div class="si-modal" (click)="$event.stopPropagation()" style="width:min(560px,100%)">
        <div class="si-modal-head">
          <div class="grow"><h3>{{ editing() ? 'تعديل جهة تنفيذية' : 'إضافة جهة تنفيذية جديدة' }}</h3><p>بيانات ملف الجهة</p></div>
          <button class="si-x" (click)="closeForm()" aria-label="إغلاق">×</button>
        </div>
        <div class="si-modal-body">
          @if (formError()) { <div class="si-err">{{ formError() }}</div> }
          <div class="si-grid">
            <div class="si-fld full"><label>اسم الجهة <span class="req">*</span></label><input [ngModel]="fAgencyName()" (ngModelChange)="fAgencyName.set($event)" placeholder="اسم الجهة التنفيذية" /></div>
            <div class="si-fld"><label>الهاتف</label><input [ngModel]="fPhone()" (ngModelChange)="fPhone.set($event)" placeholder="01XXXXXXXXX" /></div>
            <div class="si-fld"><label>البريد الإلكتروني</label><input type="email" [ngModel]="fEmail()" (ngModelChange)="fEmail.set($event)" /></div>
            <div class="si-fld full"><label>العنوان</label><input [ngModel]="fAddress()" (ngModelChange)="fAddress.set($event)" /></div>
            @if (editing()) {
              <div class="si-fld">
                <label>الحالة</label>
                <select [ngModel]="fIsActive()" (ngModelChange)="fIsActive.set($event)">
                  <option [ngValue]="true">نشط</option>
                  <option [ngValue]="false">غير نشط</option>
                </select>
              </div>
            }
          </div>
        </div>
        <div class="si-modal-foot">
          <button class="si-btn primary" [disabled]="saving()" (click)="submitForm()">
            @if (saving()) { جاري الحفظ… } @else { {{ editing() ? 'حفظ التعديلات' : 'إضافة الجهة' }} }
          </button>
          <button class="si-btn" (click)="closeForm()">إلغاء</button>
        </div>
      </div>
    </div>
  }
</div>
```

- [ ] **Step 3: Create `agencies.css`**

```css
.page { padding: 24px 28px; }

.page-head { display: flex; align-items: center; gap: 14px; margin-bottom: 20px; }
.page-head h1 { font-size: 22px; }
.page-head p { margin: 2px 0 0; color: var(--muted); font-size: 13px; }
.page-head .si-btn { margin-inline-start: auto; }

.kpis { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin-bottom: 16px; }
@media (max-width: 760px) { .kpis { grid-template-columns: repeat(2, 1fr); } }
.kpi { background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius); padding: 15px; box-shadow: var(--shadow); position: relative; overflow: hidden; }
.kpi::before { content: ""; position: absolute; inset-block: 0; inset-inline-start: 0; width: 4px; background: var(--green-600); }
.kpi.ok::before { background: var(--ok); } .kpi.bad::before { background: var(--bad); }
.kpi .lab { color: var(--muted); font-size: 12px; font-weight: 700; }
.kpi .val { display: block; font-size: 23px; font-weight: 800; margin-top: 4px; }

.toolbar { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; flex-wrap: wrap; }
.search { flex: 1; min-width: 220px; display: flex; align-items: center; gap: 9px; background: var(--surface); border: 1px solid var(--line); border-radius: 11px; padding: 11px 13px; color: var(--muted); box-shadow: var(--shadow); }
.search input { border: 0; background: transparent; flex: 1; font-family: inherit; font-size: 13.5px; color: var(--ink); outline: none; }
.seg { display: flex; background: var(--surface); border: 1px solid var(--line); border-radius: 11px; padding: 4px; }
.seg button { border: 0; background: transparent; color: var(--muted); padding: 8px 16px; border-radius: 8px; font-weight: 700; font-size: 12.5px; }
.seg button.on { background: var(--green-700); color: #fff; }

.state { display: flex; align-items: center; justify-content: center; gap: 12px; background: var(--surface); border: 1px dashed var(--line-strong); border-radius: var(--radius); padding: 40px; color: var(--muted); box-shadow: var(--shadow); flex-wrap: wrap; }
.state.error { color: #b32a39; border-color: #F3C6CC; background: var(--bad-bg); }
.spinner { width: 16px; height: 16px; border: 2px solid var(--line-strong); border-top-color: var(--green-700); border-radius: 50%; animation: spin .7s linear infinite; display: inline-block; }
@keyframes spin { to { transform: rotate(360deg); } }

.card { background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius); box-shadow: var(--shadow); overflow: hidden; }
.tbl-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; min-width: 700px; }
th { font-size: 11.5px; color: var(--muted); font-weight: 700; text-align: start; padding: 12px 14px; border-bottom: 1px solid var(--line); white-space: nowrap; background: var(--surface-2); }
td { padding: 12px 14px; border-bottom: 1px solid var(--line); font-size: 13px; white-space: nowrap; }

tr.main-row { cursor: pointer; }
tr.main-row:hover td { background: #FAFCFA; }
.chevron { transition: transform 0.2s ease; vertical-align: middle; margin-inline-end: 6px; color: var(--green-700); }
.chevron.open { transform: rotate(90deg); }

tr.sub-row td { background: var(--surface-2); white-space: normal; }
.empty-subs { color: var(--muted); font-size: 12.5px; }
.assigned-list { display: flex; flex-direction: column; gap: 6px; }
.assigned-list a { color: var(--green-700); font-weight: 700; text-decoration: none; font-size: 13px; }
.assigned-list a:hover { text-decoration: underline; }
.assigned-list .muted { color: var(--muted); font-weight: 400; }

.pill { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 999px; font-size: 12px; font-weight: 800; background: var(--bad-bg); color: #b32a39; }
.pill.ok { background: var(--ok-bg); color: #0f7a41; }
.pill::before { content: ""; width: 7px; height: 7px; border-radius: 50%; background: currentColor; }

.tblfoot { display: flex; align-items: center; gap: 12px; padding: 12px 16px; border-top: 1px solid var(--line); }
.tblfoot .muted { color: var(--muted); font-size: 12.5px; flex: 1; }
.pager { display: flex; gap: 5px; flex-wrap: wrap; }
.pager button { min-width: 34px; height: 34px; border-radius: 8px; border: 1px solid var(--line); background: var(--surface); font-weight: 700; color: var(--ink); }
.pager button.on { background: var(--green-700); color: #fff; border-color: var(--green-700); }
.pager button:disabled { opacity: .5; cursor: default; }

.acts { display: inline-flex; gap: 6px; }
.act { width: 32px; height: 32px; border-radius: 8px; border: 1px solid var(--line); background: var(--surface); display: inline-grid; place-items: center; color: var(--muted); }
.act svg { width: 15px; height: 15px; }
.act.danger { color: var(--bad); border-color: #F3C6CC; }
.act.danger:hover { background: var(--bad-bg); }
```

- [ ] **Step 4: Wire the route**

In `Frontend/src/app/app.routes.ts`, add alongside the `contractors` route added in Task 5:

```ts
      {
        path: 'agencies',
        loadComponent: () =>
          import('./features/agencies/agencies').then((m) => m.Agencies),
      },
```

- [ ] **Step 5: Add the nav item**

In `Frontend/src/app/layout/main-layout/main-layout.ts`, in `allNav`, add (all-staff visible):

```ts
    { label: 'الجهات التنفيذية', route: '/app/agencies', icon: 'M3 21h18M6 21V10l6-4 6 4v11M10 21v-5h4v5', managerOnly: false },
```

- [ ] **Step 6: Type-check**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 7: Manual check in the browser**

Same shape of check as Task 5 Step 7, on `/app/agencies`.

- [ ] **Step 8: Commit**

```bash
git add Frontend/src/app/features/agencies/ Frontend/src/app/app.routes.ts Frontend/src/app/layout/main-layout/main-layout.ts
git commit -m "feat: add Executive Agencies management page"
```

---

### Task 7: Frontend — Projects Table Column Swap + Dead Code Cleanup

**Files:**
- Modify: `Frontend/src/app/features/projects/projects.html`
- Modify: `Frontend/src/app/features/projects/projects.ts`

**Interfaces:**
- Consumes: `SubProjectListItem.contractorName` (Task 2/4).

- [ ] **Step 1: Swap the table header and sub-row cell**

In `Frontend/src/app/features/projects/projects.html`, find the header row:

```html
              <th>كود المشروع</th><th>اسم المشروع</th><th>البرنامج</th><th>جهة التنفيذ</th>
              <th>المركز</th><th>إجراءات</th>
```

Replace with:

```html
              <th>كود المشروع</th><th>اسم المشروع</th><th>البرنامج</th><th>المقاول</th>
              <th>المركز</th><th>إجراءات</th>
```

Find the main-row's agency cell:

```html
                <td>{{ row.main.executingAgency }}</td>
```

Replace with an empty cell (contractors attach to sub-projects, not mains — same reasoning as the already-blank المركز column on main rows):

```html
                <td></td>
```

Find the sub-row's agency cell:

```html
                    <td>{{ agencyOf(s.mainProjectId) }}</td>
```

Replace with:

```html
                    <td>{{ s.contractorName ?? 'غير مسند' }}</td>
```

- [ ] **Step 2: Remove the now-dead `agencyOf`/`agencyByMain` from `projects.ts`**

In `Frontend/src/app/features/projects/projects.ts`, remove:

```ts
  // كاش جهة التنفيذ لكل مشروع رئيسي (لعرضها في صفوف الفرعي)
  private readonly agencyByMain = computed(() => {
    const map = new Map<number, string>();
    for (const m of this.mains()) {
      map.set(m.id, m.executingAgency);
    }
    return map;
  });

  agencyOf(mainId: number): string {
    return this.agencyByMain().get(mainId) ?? '';
  }
```

- [ ] **Step 3: Confirm no remaining references**

```bash
cd Frontend && grep -n "agencyOf\|executingAgency}}" src/app/features/projects/projects.ts src/app/features/projects/projects.html
```
Expected: no output. (`row.main.executingAgency` as a *filter* value — `m.executingAgency` used in `matchesMainFilters`/advanced-filter dropdown — is untouched; this grep is specifically for the deleted display usages, so if it prints anything, check it's not one of those legitimate filter-logic hits before concluding something was missed.)

- [ ] **Step 4: Type-check**

```bash
npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors.

- [ ] **Step 5: Manual check in the browser**

On `/app/projects`, confirm: header reads "المقاول" not "جهة التنفيذ"; main rows show a blank cell there; a sub-project with an assignment (e.g. the one created while testing Task 2/3) shows the contractor's name; one with none shows "غير مسند".

- [ ] **Step 6: Commit**

```bash
git add Frontend/src/app/features/projects/projects.html Frontend/src/app/features/projects/projects.ts
git commit -m "refactor: show contractor instead of agency in the projects table"
```

---

### Task 8: Final End-to-End Verification

**Files:** none (verification only).

- [ ] **Step 1: Full regression pass in the browser**

With `frontend-dev` and `backend-api` running:
1. Login as `superadmin`/`admin` still works (unaffected by the role removal — `SuperAdmin`/`PlanningManager`/`PlanningEmployee` are untouched).
2. `/app/contractors`: create a contractor, edit it, expand it (empty sub-projects), delete a throwaway one.
3. `/app/agencies`: same checks.
4. Go to an existing sub-project's details page (`/app/projects/:id`), assign an executive agency and a contractor via the existing assignment UI (untouched by this plan beyond its role attributes).
5. Back on `/app/contractors`, expand that contractor — confirm the sub-project now appears, linking correctly to `/app/projects/:id`.
6. Back on `/app/agencies`, same check for the agency.
7. `/app/projects` table: confirm that sub-project's row now shows the contractor's name in the المقاول column.
8. Confirm the nav shows "المقاولون" and "الجهات التنفيذية" for both a manager and non-manager account (both all-staff visible).

- [ ] **Step 2: Confirm no stray console errors**

Use `read_console_messages` (`onlyErrors: true`) during the pass above.

- [ ] **Step 3: Confirm the deleted API surface is actually gone**

Via a quick authenticated `fetch` in the browser console (same pattern used throughout this session):
```js
fetch('https://localhost:7250/api/subprojects/1/assignments/1/change-requests', { headers: { Authorization: 'Bearer ' + localStorage.getItem('smartinvest_token') } }).then(r => r.status)
```
Expected: `404`.

- [ ] **Step 4: Final `git status`**

```bash
git status
```
Confirm only files touched by Tasks 1-7 show as modified/new.
