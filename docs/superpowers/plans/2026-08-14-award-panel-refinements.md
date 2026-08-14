# Contract Award Panel Refinements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Six targeted fixes to the step-6 (مرحلة العقد والترسية) award panel: thousands-entry money inputs, inline advance-payment proof upload, self/bank funding bounds, contract-value-based advance calculation, net-of-spend المتاح, and handover upload without a forced save-first step.

**Architecture:** Two independent backend service changes (`ProcurementService.cs`, `BankAvailabilityService.cs` — different files, zero coupling) plus one frontend component change (`procurement-workflow.ts`/`.html`). No schema/migration changes — every item is either a pure computation change or reuses existing columns and endpoints.

**Tech Stack:** .NET 10 / EF Core (InMemory provider for tests) / Angular 21 standalone components with Signals.

## Global Constraints

- Thousands-entry inputs use the existing `egpToThousands`/`thousandsToEgp` pair from `Frontend/src/app/core/utils/budget.util.ts` — do not invent a new conversion helper.
- Self/bank bound check is `<=` (equal allowed), enforced on both frontend (inline hint) and backend (hard `BusinessRuleException`).
- المتاح net-of-spend change touches only `BankAvailabilityListDto.TotalAvailable`'s computation — `RemainingAvailable` and the existing deposit cap-check in `AddAsync`/`UpdateAsync` are untouched.
- No backend endpoint changes for the handover-upload item — it's a frontend call-chaining change only (`SetSiteHandoverAsync` genuinely requires `ContractAward.SiteHandoverMode` already persisted; see spec item 6).
- Every task must build and pass its own tests standalone — this codebase's own hard-learned rule from earlier this session.

---

### Task 1: Backend — ProcurementService (hide advance-payment-proof from generic upload list, enforce self/bank bounds)

**Files:**
- Modify: `Backend/src/SmartInvest.Infrastructure/Services/ProcurementService.cs:339-360` (`SetContractAwardDetailsAsync`), `:781-810` (`BuildStageDto`)
- Test: `Backend/tests/SmartInvest.Tests/ContractAwardReworkTests.cs`

**Interfaces:**
- Consumes: existing `ContractAward` entity (`AdvancePaymentSelfAmount`, `AdvancePaymentBankAmount` — both `decimal?`), existing `SubProject.SelfFunding`/`BankFunding` (`decimal`), existing `BusinessRuleException`.
- Produces: no new public signatures — `SetContractAwardDetailsAsync`'s existing signature is unchanged, it now throws `BusinessRuleException` in one more case. `BuildStageDto`'s returned `FileSlots` list excludes `advance-payment-proof` when `stage == ProcurementStage.ContractAward` — Task 3 (frontend) relies on this to stop rendering that slot in the generic uploader.

- [ ] **Step 1: Write the failing test for the self/bank bound check**

Open `Backend/tests/SmartInvest.Tests/ContractAwardReworkTests.cs` and read its existing `SeedProjectAsync`/`SeedAwardPrereqsAsync`/`CreateContext`/`CreateService` helpers (used by every test in the file already) — reuse them verbatim, do not duplicate. Add this test immediately after the file's last existing `[Fact]`:

```csharp
    [Fact]
    public async Task Advance_self_amount_above_planned_self_funding_is_blocked()
    {
        await using var context = CreateContext();
        var subProjectId = await SeedProjectAsync(context, projectNature: "مقاولات", selfFunding: 10_000m, bankFunding: 90_000m);
        await SeedAwardPrereqsAsync(context, subProjectId);
        var service = CreateService(context);

        var dto = new SetContractAwardDetailsDto
        {
            AdvancePaymentDone = false,
            AdvancePaymentPercentage = 10m,
            AdvancePaymentSelfAmount = 10_000.01m,
            AdvancePaymentBankAmount = 0m,
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SetContractAwardDetailsAsync(subProjectId, dto));

        Assert.Contains("التمويل الذاتي", ex.Message);
    }

    [Fact]
    public async Task Advance_self_amount_equal_to_planned_self_funding_is_allowed()
    {
        await using var context = CreateContext();
        var subProjectId = await SeedProjectAsync(context, projectNature: "مقاولات", selfFunding: 10_000m, bankFunding: 90_000m);
        await SeedAwardPrereqsAsync(context, subProjectId);
        var service = CreateService(context);

        var dto = new SetContractAwardDetailsDto
        {
            AdvancePaymentDone = false,
            AdvancePaymentPercentage = 10m,
            AdvancePaymentSelfAmount = 10_000m,
            AdvancePaymentBankAmount = 0m,
        };

        await service.SetContractAwardDetailsAsync(subProjectId, dto);

        var saved = await context.ContractAwards.AsNoTracking().FirstAsync(x => x.SubProjectId == subProjectId);
        Assert.Equal(10_000m, saved.AdvancePaymentSelfAmount);
    }

    [Fact]
    public async Task Advance_bank_amount_above_planned_bank_funding_is_blocked()
    {
        await using var context = CreateContext();
        var subProjectId = await SeedProjectAsync(context, projectNature: "مقاولات", selfFunding: 10_000m, bankFunding: 90_000m);
        await SeedAwardPrereqsAsync(context, subProjectId);
        var service = CreateService(context);

        var dto = new SetContractAwardDetailsDto
        {
            AdvancePaymentDone = false,
            AdvancePaymentPercentage = 10m,
            AdvancePaymentSelfAmount = 0m,
            AdvancePaymentBankAmount = 90_000.01m,
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SetContractAwardDetailsAsync(subProjectId, dto));

        Assert.Contains("التمويل البنكي", ex.Message);
    }
```

If `SeedProjectAsync` in this file doesn't currently accept `selfFunding`/`bankFunding` parameters (check its signature first — it may hardcode these), add optional parameters `decimal selfFunding = 0m, decimal bankFunding = 50_000m` to it (pick defaults matching whatever the file's other existing tests already assume, so their calls compile unchanged) and pass them onto the `SubProject` it constructs. Read the helper's current body before editing it — do not guess its shape.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd Backend && dotnet test --filter ContractAwardReworkTests`
Expected: the 3 new tests FAIL (the first two on "no exception was thrown" / wrong exception, since the check doesn't exist yet; the third compiles once `SeedProjectAsync`'s new parameters are added).

- [ ] **Step 3: Add the bound check**

In `Backend/src/SmartInvest.Infrastructure/Services/ProcurementService.cs`, `SetContractAwardDetailsAsync` (currently lines 339-360):

```csharp
    public async Task SetContractAwardDetailsAsync(int subProjectId, SetContractAwardDetailsDto dto, CancellationToken cancellationToken = default)
    {
        var doc = await GetEditableContractAwardAsync(subProjectId, cancellationToken);

        if (dto.AdvancePaymentSelfAmount is > 0m or < 0m || dto.AdvancePaymentBankAmount is > 0m or < 0m)
        {
            var funding = await _context.SubProjects.AsNoTracking()
                .Where(x => x.SubProjectId == subProjectId)
                .Select(x => new { x.SelfFunding, x.BankFunding })
                .FirstAsync(cancellationToken);

            if (dto.AdvancePaymentSelfAmount > funding.SelfFunding)
            {
                throw new BusinessRuleException(
                    $"الجزء المصروف من التمويل الذاتي ({dto.AdvancePaymentSelfAmount:N2} ج.م) يتجاوز التمويل الذاتي المخطط للمشروع ({funding.SelfFunding:N2} ج.م)");
            }
            if (dto.AdvancePaymentBankAmount > funding.BankFunding)
            {
                throw new BusinessRuleException(
                    $"الجزء المصروف من التمويل البنكي ({dto.AdvancePaymentBankAmount:N2} ج.م) يتجاوز التمويل البنكي المخطط للمشروع ({funding.BankFunding:N2} ج.م)");
            }
        }

        doc.AdvancePaymentDone = dto.AdvancePaymentDone;
        doc.AdvancePaymentPercentage = dto.AdvancePaymentPercentage;
        doc.AdvancePaymentSelfAmount = dto.AdvancePaymentSelfAmount;
        doc.AdvancePaymentBankAmount = dto.AdvancePaymentBankAmount;
        doc.ExecutionDurationMonths = dto.ExecutionDurationMonths;
        doc.ExecutionDurationDays = dto.ExecutionDurationDays;
        doc.SiteHandoverMode = dto.SiteHandoverMode is int mode ? (SiteHandoverMode)mode : null;
        doc.PenaltyAmount = dto.PenaltyAmount;

        // الإسناد نفسه يعيش في ProjectAssignment — مصدر حقيقة واحد لهوية المقاول،
        // تقرأ منه متابعة المشروعات وملف المقاول.
        if (dto.ContractorId is int contractorId)
        {
            await UpsertAssignmentAsync(doc, contractorId, dto, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
```

The `is > 0m or < 0m` guard is just "has a value and it's not exactly zero" written without triggering a nullable-warning on the `>`/`<` operators against a `decimal?` — it's cheaper to skip the funding lookup entirely when both amounts are null/zero, which is the common case before advance payment is configured at all.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd Backend && dotnet test --filter ContractAwardReworkTests`
Expected: PASS, all tests in the file including the 3 new ones.

- [ ] **Step 5: Hide advance-payment-proof from the generic file-upload list for the contract-award stage**

In the same file, `BuildStageDto<TDto>` (currently lines 781-810), change the `FileSlots` projection:

```csharp
            FileSlots = ops.Slots
                .Where(s => !(stage == ProcurementStage.ContractAward && s.Key == "advance-payment-proof"))
                .Select(s => new ProcurementFileSlotDto { Key = s.Key, Label = s.Label, Required = s.Required })
                .ToList(),
```

This only changes what the generic "رفع إصدار جديد" uploader (driven by `ProcurementStageDto.FileSlots`) shows — the `FileSlot<ContractAwardVersion>` registration itself (`ProcurementService.cs:867`) is untouched, so uploading a file under the `advance-payment-proof` key through `uploadStageVersion` (used directly by Task 3's new inline uploader) still works and still satisfies the existing stage-completion check at `ProcurementService.cs:1027-1030`.

- [ ] **Step 6: Add a test proving the slot is hidden**

Add to `ContractAwardReworkTests.cs`:

```csharp
    [Fact]
    public async Task Advance_payment_proof_slot_is_hidden_from_contract_award_file_slots()
    {
        await using var context = CreateContext();
        var subProjectId = await SeedProjectAsync(context, projectNature: "مقاولات");
        await SeedAwardPrereqsAsync(context, subProjectId);
        var service = CreateService(context);

        var overview = await service.GetOverviewAsync(subProjectId);
        var awardStage = overview.Stages.Single(s => s.Stage == "contract-award");

        Assert.DoesNotContain(awardStage.FileSlots, s => s.Key == "advance-payment-proof");
        Assert.Contains(awardStage.FileSlots, s => s.Key == "award-order");
    }
```

Read `GetOverviewAsync`'s actual signature in this file first to confirm the call shape above matches (parameter list, return type) — adjust only if it genuinely differs, the assertions themselves are what matters.

- [ ] **Step 7: Run full backend suite**

Run: `cd Backend && dotnet test`
Expected: all tests pass, no regressions in unrelated files.

- [ ] **Step 8: Commit**

```bash
git add Backend/src/SmartInvest.Infrastructure/Services/ProcurementService.cs Backend/tests/SmartInvest.Tests/ContractAwardReworkTests.cs
git commit -m "feat(procurement): enforce advance self/bank funding bounds, hide advance-payment-proof from generic upload list"
```

---

### Task 2: Backend — BankAvailabilityService (المتاح net of spend)

**Files:**
- Modify: `Backend/src/SmartInvest.Infrastructure/Services/BankAvailabilityService.cs:36-75`
- Test: Create `Backend/tests/SmartInvest.Tests/BankAvailabilityServiceTests.cs`

**Interfaces:**
- Consumes: existing `BankAvailability`, `SubProject`, `ContractAward`, `ExecutionStage`, `SubProjectFinancialYear` entities — no changes to any of them.
- Produces: `GetForFinancialYearAsync`'s existing return type (`BankAvailabilityListDto`) is unchanged in shape — only `TotalAvailable`'s computed value changes. No frontend changes needed for this task; `projects.html:278` already reads `availabilityData()?.totalAvailable`.

- [ ] **Step 1: Write the failing test**

Create `Backend/tests/SmartInvest.Tests/BankAvailabilityServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Infrastructure.Data;
using SmartInvest.Infrastructure.Services;

namespace SmartInvest.Tests;

public class BankAvailabilityServiceTests
{
    [Fact]
    public async Task TotalAvailable_subtracts_done_advance_payments_and_execution_spend()
    {
        await using var context = CreateContext();
        var year = await SeedYearAsync(context);

        // مشروع 1: تمويل بنكي مخطط 200,000، دفعة مقدمة بنكية 30,000 (تم صرفها فعليًا)، وصرف تنفيذ 20,000
        var project1 = await SeedProjectAsync(context, year.FinancialYearId, bankFunding: 200_000m, selfFunding: 0m);
        await SeedContractAwardAsync(context, project1, advancePaymentDone: true, advancePaymentBankAmount: 30_000m);
        await SeedExecutionSpendAsync(context, project1, year.FinancialYearId, bankFundingSpent: 20_000m);

        // مشروع 2: تمويل بنكي مخطط 100,000، دفعة مقدمة بنكية 15,000 لكن لم تُصرف فعليًا بعد (AdvancePaymentDone = false)
        var project2 = await SeedProjectAsync(context, year.FinancialYearId, bankFunding: 100_000m, selfFunding: 0m);
        await SeedContractAwardAsync(context, project2, advancePaymentDone: false, advancePaymentBankAmount: 15_000m);

        context.BankAvailabilities.Add(new BankAvailability
        {
            FinancialYearId = year.FinancialYearId,
            Amount: 250_000m,
            ReceivedDate = DateTime.UtcNow.Date,
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetForFinancialYearAsync(year.FinancialYearId);

        // 250,000 (مستلم) - 30,000 (دفعة مقدمة تم صرفها فعليًا فقط، مشروع 2 لم تُصرف بعد فتُستبعد) - 20,000 (صرف تنفيذ) = 200,000
        Assert.Equal(200_000m, result.TotalAvailable);
        Assert.Equal(300_000m, result.TotalBankFunding);
        Assert.Equal(100_000m, result.RemainingAvailable);
    }

    [Fact]
    public async Task TotalAvailable_ignores_spend_from_other_financial_years()
    {
        await using var context = CreateContext();
        var year1 = await SeedYearAsync(context, "2026/2027");
        var year2 = await SeedYearAsync(context, "2027/2028");

        var project1 = await SeedProjectAsync(context, year1.FinancialYearId, bankFunding: 100_000m, selfFunding: 0m);
        await SeedContractAwardAsync(context, project1, advancePaymentDone: true, advancePaymentBankAmount: 40_000m);

        context.BankAvailabilities.Add(new BankAvailability
        {
            FinancialYearId = year2.FinancialYearId,
            Amount = 50_000m,
            ReceivedDate = DateTime.UtcNow.Date,
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetForFinancialYearAsync(year2.FinancialYearId);

        // إتاحات year2 لا تخصم منها دفعة project1 (project1 مرتبط بـ year1 فقط)
        Assert.Equal(50_000m, result.TotalAvailable);
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static BankAvailabilityService CreateService(AppDbContext context) => new(
        context,
        new TestCurrentUser());

    private static async Task<FinancialYear> SeedYearAsync(AppDbContext context, string name = "2026/2027")
    {
        var year = new FinancialYear
        {
            Name = name,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
        };
        context.FinancialYears.Add(year);
        await context.SaveChangesAsync();
        return year;
    }

    private static async Task<SubProject> SeedProjectAsync(
        AppDbContext context, int financialYearId, decimal bankFunding, decimal selfFunding)
    {
        var status = new ProjectStatus { StatusName = "قيد التنفيذ" };
        var project = new SubProject
        {
            SubProjectName = "مشروع اختبار المتاح",
            ProjectNature = "مقاولات",
            IsApproved = true,
            Status = status,
            BankFunding = bankFunding,
            SelfFunding = selfFunding,
            MainProject = new MainProject { MainProjectName = "مشروع رئيسي" },
        };
        context.AddRange(status, project);
        await context.SaveChangesAsync();
        context.SubProjectFinancialYears.Add(new SubProjectFinancialYear
        {
            SubProjectId = project.SubProjectId,
            FinancialYearId = financialYearId,
        });
        await context.SaveChangesAsync();
        return project;
    }

    private static async Task SeedContractAwardAsync(
        AppDbContext context, SubProject project, bool advancePaymentDone, decimal advancePaymentBankAmount)
    {
        context.ContractAwards.Add(new ContractAward
        {
            SubProjectId = project.SubProjectId,
            AdvancePaymentDone = advancePaymentDone,
            AdvancePaymentBankAmount = advancePaymentBankAmount,
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedExecutionSpendAsync(
        AppDbContext context, SubProject project, int financialYearId, decimal bankFundingSpent)
    {
        var cycle = await context.SubProjectFinancialYears
            .FirstAsync(x => x.SubProjectId == project.SubProjectId && x.FinancialYearId == financialYearId);
        context.ExecutionStages.Add(new ExecutionStage
        {
            SubProjectId = project.SubProjectId,
            SubProjectFinancialYearId = cycle.SubProjectFinancialYearId,
            Name = "مرحلة اختبار",
            BankFundingSpent = bankFundingSpent,
        });
        await context.SaveChangesAsync();
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId => "test-user";
        public string? Role => Roles.SuperAdmin;
    }
}
```

Before trusting this verbatim: read `BankAvailability`'s entity definition and `BankAvailabilityService`'s constructor signature to confirm field names (`Amount`, `ReceivedDate`, `FinancialYearId`) and the exact `ICurrentUserService` shape (copy `TestCurrentUser` from `ExecutionTrackingIntegrationTests.cs` if its shape differs from what's shown above — that file already has a working one, prefer copying it exactly over retyping from memory).

- [ ] **Step 2: Run test to verify it fails**

Run: `cd Backend && dotnet test --filter BankAvailabilityServiceTests`
Expected: FAIL — `TotalAvailable` currently returns `250,000`/`300,000` (raw sums, no subtraction) instead of the expected net-of-spend values.

- [ ] **Step 3: Implement net-of-spend TotalAvailable**

In `Backend/src/SmartInvest.Infrastructure/Services/BankAvailabilityService.cs`, `GetForFinancialYearAsync` (currently lines 36-75):

```csharp
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
        var advancesSpent = await GetAdvancePaymentsSpentAsync(financialYearId, cancellationToken);
        var executionSpent = await GetExecutionBankSpendAsync(financialYearId, cancellationToken);
        var totalAvailable = receipts - advancesSpent - executionSpent;

        return new BankAvailabilityListDto
        {
            TotalAvailable = totalAvailable,
            TotalBankFunding = totalBankFunding,
            RemainingAvailable = totalBankFunding - receipts,
            Items = items,
        };
    }

    /// <summary>مجموع الدفعات المقدمة البنكية المصروفة فعليًا (AdvancePaymentDone) عبر كل المشروعات
    /// الفرعية المرتبطة بهذه السنة المالية — يُخصم من المتاح لأنه صرف حقيقي من الرصيد المستلم.</summary>
    private async Task<decimal> GetAdvancePaymentsSpentAsync(int financialYearId, CancellationToken cancellationToken)
    {
        return await _context.ContractAwards.AsNoTracking()
            .Where(a => a.AdvancePaymentDone
                && a.SubProject.FinancialYears.Any(fy => fy.FinancialYearId == financialYearId))
            .SumAsync(a => a.AdvancePaymentBankAmount ?? 0m, cancellationToken);
    }

    /// <summary>مجموع الصرف الفعلي من التمويل البنكي عبر مراحل التنفيذ لكل المشروعات الفرعية
    /// المرتبطة بهذه السنة المالية تحديدًا (وليس أي سنة أخرى للمشروع نفسه).</summary>
    private async Task<decimal> GetExecutionBankSpendAsync(int financialYearId, CancellationToken cancellationToken)
    {
        return await _context.ExecutionStages.AsNoTracking()
            .Where(e => e.SubProjectFinancialYear!.FinancialYearId == financialYearId)
            .SumAsync(e => e.BankFundingSpent, cancellationToken);
    }
```

Note `RemainingAvailable` now uses `receipts` (the raw sum, kept as a local variable) rather than the now-net `totalAvailable` — this preserves its existing meaning exactly ("how much more can still be deposited toward the plan"), unaffected by this change, per the spec's explicit instruction not to touch it. Check `ExecutionStage.SubProjectFinancialYear`'s nullability in the entity (`SubProjectFinancialYearId` is `int?` per earlier reads in this codebase) — if the navigation property is nullable, the `!` above is safe only if every row in practice has a value; if EF's InMemory provider complains or a null case is plausible, use `e.SubProjectFinancialYear != null && e.SubProjectFinancialYear.FinancialYearId == financialYearId` instead — verify against the actual entity before finalizing.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd Backend && dotnet test --filter BankAvailabilityServiceTests`
Expected: PASS, both tests.

- [ ] **Step 5: Run full backend suite**

Run: `cd Backend && dotnet test`
Expected: all tests pass — this change doesn't touch `AddAsync`/`UpdateAsync`'s deposit cap-check, so no existing bank-availability-adjacent behavior should regress.

- [ ] **Step 6: Commit**

```bash
git add Backend/src/SmartInvest.Infrastructure/Services/BankAvailabilityService.cs Backend/tests/SmartInvest.Tests/BankAvailabilityServiceTests.cs
git commit -m "feat(procurement): المتاح نet of advance payments and execution spend"
```

---

### Task 3: Frontend — award panel (thousands inputs, inline proof upload, bound hint, contract-value advance base, chained handover save)

**Files:**
- Modify: `Frontend/src/app/features/financial/procurement-workflow.ts`
- Modify: `Frontend/src/app/features/financial/procurement-workflow.html`

**Interfaces:**
- Consumes: `Task 1`'s backend validation (surfaced generically via `err?.error?.message`, same pattern as every other error in this component — no new frontend error-parsing needed) and `Task 1`'s filtered `FileSlots` (contract-award stage no longer includes `advance-payment-proof`, so the generic uploader stops rendering it automatically — no frontend code needed to "remove" it, it's just absent from the array the `@for` loop already iterates). Consumes existing `egpToThousands`/`thousandsToEgp` from `Frontend/src/app/core/utils/budget.util.ts` and existing `financial.uploadStageVersion()`/`financial.setSiteHandover()` service methods — no new service methods needed.
- Produces: no new public surface — this is a leaf feature component.

No automated frontend test coverage exists for this component today (consistent with the rest of the app) — this task is implementation + a manual verification checklist at the end, not TDD.

- [ ] **Step 1: Convert قيمة العقد / الدفعة المقدمة self+bank to thousands entry**

In `Frontend/src/app/features/financial/procurement-workflow.ts`, the three signals stay named the same (`aContractValue`, `aAdvanceSelf`, `aAdvanceBank`) but now hold thousands values, matching the existing `newAvailabilityThousands` idiom in `projects.ts`. Add raw-EGP computed signals right after them (find the existing block at lines 70-99):

```typescript
  protected readonly aContractDate = signal<string>('');
  protected readonly aContractValue = signal<number | null>(null);
  protected readonly aContractValueRaw = computed(() => thousandsToEgp(this.aContractValue()));
  protected readonly aAdvanceDone = signal(false);
  protected readonly aAdvancePercentage = signal<number | null>(null);
  protected readonly aAdvanceSelf = signal<number | null>(null);
  protected readonly aAdvanceSelfRaw = computed(() => thousandsToEgp(this.aAdvanceSelf()));
  protected readonly aAdvanceBank = signal<number | null>(null);
  protected readonly aAdvanceBankRaw = computed(() => thousandsToEgp(this.aAdvanceBank()));
```

Add the import at the top of the file (next to any existing imports from the same module — if `budget.util` isn't imported yet in this file, add a new import line):

```typescript
import { egpToThousands, thousandsToEgp } from '../../core/utils/budget.util';
```

Update `advanceAmount()` to use `aContractValueRaw()` instead of `award()?.totalCost` — this also folds in item 4 (see Step 4 below), so leave this specific edit for that step to avoid touching the same lines twice.

Update `advanceRemaining()` (currently line 97-99) to use the raw computeds:

```typescript
  protected readonly advanceRemaining = computed(
    () => Math.round((this.advanceAmount() - this.aAdvanceSelfRaw() - this.aAdvanceBankRaw()) * 100) / 100,
  );
```

Update `syncAwardForm()` (currently lines 141-158) to convert server values to thousands on load:

```typescript
    this.aContractValue.set(egpToThousands(details.contractValue));
    ...
    this.aAdvanceSelf.set(egpToThousands(details.advancePaymentSelfAmount));
    this.aAdvanceBank.set(egpToThousands(details.advancePaymentBankAmount));
```

(keep every other line in `syncAwardForm()` exactly as it is — only these three assignments change, replacing the direct `details.X` pass-through with `egpToThousands(details.X)`).

Update the save payload in `saveAward()` (currently lines 166-179) to convert back to raw EGP:

```typescript
      .setContractAwardDetails(this.subProjectId, {
        advancePaymentDone: this.aAdvanceDone(),
        advancePaymentPercentage: this.aAdvancePercentage(),
        advancePaymentSelfAmount: this.aAdvanceSelfRaw(),
        advancePaymentBankAmount: this.aAdvanceBankRaw(),
        executionDurationMonths: this.aDurationMonths(),
        executionDurationDays: this.aDurationDays(),
        siteHandoverMode: this.aHandoverMode(),
        penaltyAmount: this.aPenaltyAmount(),
        contractorId: this.aContractorId(),
        contractDate: this.aContractDate() || null,
        contractValue: this.aContractValueRaw(),
      })
```

In `Frontend/src/app/features/financial/procurement-workflow.html`, no changes are needed to the three `<input type="number">` bindings themselves (`procurement-workflow.html:318,332,336`) — they already bind directly to the signals (`aContractValue`, `aAdvanceSelf`, `aAdvanceBank`), which now simply mean "thousands" instead of "raw EGP." Add a hint under each so the unit is visible to the user — for قيمة العقد (line 317-319):

```html
                      <div class="si-fld">
                        <label>قيمة العقد</label>
                        <input type="number" [ngModel]="aContractValue()" (ngModelChange)="aContractValue.set($event)" [disabled]="!awardEditable()" />
                        <span class="hint">بالألف جنيه</span>
                      </div>
```

Apply the identical `<span class="hint">بالألف جنيه</span>` pattern to the "من التمويل الذاتي" and "من التمويل البنكي" fields (currently lines 330-337).

- [ ] **Step 2: Self/bank bound — inline hint**

In `procurement-workflow.ts`, add two computed signals near `advanceRemaining()`:

```typescript
  protected readonly aAdvanceSelfExceeds = computed(
    () => this.aAdvanceSelfRaw() > (this.award()?.selfFunding ?? 0),
  );
  protected readonly aAdvanceBankExceeds = computed(
    () => this.aAdvanceBankRaw() > (this.award()?.bankFunding ?? 0),
  );
```

In `procurement-workflow.html`, add a hint under each of the two inputs (currently lines 330-337):

```html
                        <div class="si-fld">
                          <label>من التمويل الذاتي</label>
                          <input type="number" [ngModel]="aAdvanceSelf()" (ngModelChange)="aAdvanceSelf.set($event)" [disabled]="!awardEditable()" />
                          <span class="hint">بالألف جنيه</span>
                          @if (aAdvanceSelfExceeds()) {
                            <span class="si-err">يتجاوز التمويل الذاتي المخطط ({{ thousandsLabel(aw.selfFunding) }})</span>
                          }
                        </div>
                        <div class="si-fld">
                          <label>من التمويل البنكي</label>
                          <input type="number" [ngModel]="aAdvanceBank()" (ngModelChange)="aAdvanceBank.set($event)" [disabled]="!awardEditable()" />
                          <span class="hint">بالألف جنيه</span>
                          @if (aAdvanceBankExceeds()) {
                            <span class="si-err">يتجاوز التمويل البنكي المخطط ({{ thousandsLabel(aw.bankFunding) }})</span>
                          }
                        </div>
```

- [ ] **Step 3: Manually verify item 1 and item 3's frontend half**

```bash
cd Frontend && npm run build
```

Expected: 0 errors. Then in a running instance: open step 6 for a مقاولات project, type `5` in قيمة العقد, confirm it round-trips correctly after save+reload (shows `5` again, not `5000`), and confirm typing an advance self/bank amount above the project's planned funding shows the red hint immediately.

- [ ] **Step 4: Advance-amount base — قيمة العقد instead of الإجمالي المخطط**

In `procurement-workflow.ts`, `advanceAmount()` (currently lines 87-94):

```typescript
  /** قيمة الدفعة المقدمة بالجنيه — تُحسب من قيمة العقد لا الإجمالي المخطط، تظهر تلقائيًا بمجرد كتابة النسبة أو قيمة العقد */
  protected readonly advanceAmount = computed(() => {
    const pct = this.aAdvancePercentage();
    const base = this.aContractValueRaw();
    if (pct == null || pct <= 0 || base <= 0) {
      return 0;
    }
    return Math.round(base * pct) / 100;
  });
```

- [ ] **Step 5: Manually verify item 4**

In the running app: open step 6, enter a قيمة العقد, then enter an advance percentage — confirm the "= …" hint next to النسبة (`procurement-workflow.html:328`) reflects `contractValue × percentage / 100`, and updates live as either field changes.

- [ ] **Step 6: Move advance-payment-proof upload inline under the checkbox**

In `procurement-workflow.ts`, add a dedicated small signal + method near `onHandoverFileChange`/`saveHandover` (currently around lines 193-223):

```typescript
  protected aAdvanceProofFile: File | null = null;
  protected readonly aAdvanceProofUploading = signal(false);
  protected readonly aAdvanceProofError = signal<string | null>(null);

  protected onAdvanceProofFileChange(event: Event): void {
    this.aAdvanceProofFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  protected uploadAdvanceProof(): void {
    if (this.aAdvanceProofUploading() || !this.aAdvanceProofFile) {
      return;
    }
    this.aAdvanceProofUploading.set(true);
    this.aAdvanceProofError.set(null);
    this.financial
      .uploadStageVersion(this.subProjectId, 'contract-award', { 'advance-payment-proof': this.aAdvanceProofFile }, '')
      .subscribe({
        next: () => {
          this.aAdvanceProofUploading.set(false);
          this.aAdvanceProofFile = null;
          this.toast.success('تم رفع إثبات صرف الدفعة المقدمة');
          this.reload();
        },
        error: (err) => {
          this.aAdvanceProofUploading.set(false);
          this.aAdvanceProofError.set(err?.error?.message ?? 'تعذر رفع الملف');
        },
      });
  }
```

Read `uploadStageVersion`'s exact signature in `Frontend/src/app/core/services/financial.service.ts` (or wherever `FinancialService`/whatever class `this.financial` is lives — grep for `uploadStageVersion` to find it) before using it verbatim above — confirm the stage-key parameter accepts the literal string `'contract-award'` (matching the `stage` value already used elsewhere in this same component, e.g. wherever `detail.stage` is passed to this same method in `submitUpload()`) and that the files parameter accepts a plain `Record<string, File>` / `{[key: string]: File}` shape.

In `procurement-workflow.html`, replace the checkbox block (currently lines 343-349):

```html
                        <div class="si-fld full">
                          <label class="inline-check">
                            <input type="checkbox" [checked]="aAdvanceDone()" (change)="aAdvanceDone.set($any($event.target).checked)" [disabled]="!awardEditable()" />
                            <span>تم صرف الدفعة المقدمة فعليًا</span>
                          </label>
                          @if (aAdvanceDone()) {
                            <div class="si-fld full">
                              <label>إثبات صرف الدفعة المقدمة <span class="req">*</span></label>
                              <input type="file" accept=".pdf,.png,.jpg,.jpeg" (change)="onAdvanceProofFileChange($event)" />
                              <button type="button" class="si-btn sm" [disabled]="aAdvanceProofUploading()" (click)="uploadAdvanceProof()">
                                @if (aAdvanceProofUploading()) { جاري الرفع… } @else { رفع الإثبات }
                              </button>
                              @if (aAdvanceProofError()) {
                                <div class="si-err">{{ aAdvanceProofError() }}</div>
                              }
                            </div>
                          }
                        </div>
```

`ContractAwardDetailsDto` doesn't expose an uploaded-advance-proof filename today (only `SiteHandoverProofFileName`, a different file), so there's no "already uploaded" indicator for this slot — the plain upload control renders every time the checkbox is checked. Out of scope to add that field here.

- [ ] **Step 7: Confirm the generic uploader no longer shows this slot**

This should already be true automatically once Task 1's backend change is live (the `FileSlots` array the `@for` loop at `procurement-workflow.html:453` iterates simply won't contain `advance-payment-proof` for the contract-award stage anymore) — no frontend code change needed for the removal itself. Manually verify: open step 6, click "رفع إصدار جديد" (if that button/action exists for this stage), confirm only "أمر الإسناد" and "العقد" appear as file slots, not "إثبات صرف الدفعة المقدمة".

- [ ] **Step 8: Manually verify item 2 end-to-end**

Build and run:

```bash
cd Frontend && npm run build
```

In the running app: check "تم صرف الدفعة المقدمة فعليًا" — confirm the file upload input appears immediately under it; uncheck it — confirm the input disappears; check it again, pick a file, click "رفع الإثبات" — confirm success toast and the file is attached (verify via the stage-completion check later requiring it, or by re-opening the generic version list to see the new version was created with this file).

- [ ] **Step 9: Chain award-save and handover-save into one action**

In `procurement-workflow.ts`, replace `saveAward()` (currently lines 160-191):

```typescript
  protected saveAward(): void {
    if (this.awardSaving()) {
      return;
    }
    this.awardSaving.set(true);
    this.awardError.set(null);
    this.financial
      .setContractAwardDetails(this.subProjectId, {
        advancePaymentDone: this.aAdvanceDone(),
        advancePaymentPercentage: this.aAdvancePercentage(),
        advancePaymentSelfAmount: this.aAdvanceSelfRaw(),
        advancePaymentBankAmount: this.aAdvanceBankRaw(),
        executionDurationMonths: this.aDurationMonths(),
        executionDurationDays: this.aDurationDays(),
        siteHandoverMode: this.aHandoverMode(),
        penaltyAmount: this.aPenaltyAmount(),
        contractorId: this.aContractorId(),
        contractDate: this.aContractDate() || null,
        contractValue: this.aContractValueRaw(),
      })
      .subscribe({
        next: () => this.saveAwardThenHandoverIfStaged(),
        error: (err) => {
          this.awardSaving.set(false);
          this.awardError.set(err?.error?.message ?? 'تعذر حفظ بيانات الترسية');
        },
      });
  }

  /** بعد نجاح حفظ بيانات الترسية: لو المستخدم اختار "مُسلَّمة للمقاول" وجهّز تاريخًا وملفًا، يُسجَّل التسليم في نفس الحفظة — لا حاجة لخطوة حفظ منفصلة. */
  private saveAwardThenHandoverIfStaged(): void {
    if (this.aHandoverMode() !== 1 || !this.aHandoverDate() || !this.aHandoverFile) {
      this.awardSaving.set(false);
      this.toast.success('تم حفظ بيانات الترسية');
      this.reload();
      return;
    }

    this.financial.setSiteHandover(this.subProjectId, this.aHandoverDate(), this.aHandoverFile).subscribe({
      next: () => {
        this.awardSaving.set(false);
        this.aHandoverFile = null;
        this.toast.success('تم حفظ بيانات الترسية وتسجيل تسليم الأرضية');
        this.reload();
      },
      error: (err) => {
        this.awardSaving.set(false);
        this.toast.error(err?.error?.message ?? 'تم حفظ بيانات الترسية، لكن تعذر تسجيل تسليم الأرضية');
        this.reload();
      },
    });
  }
```

`saveHandover()`, `onHandoverFileChange()`, and `downloadHandoverProof()` (currently lines 193-230) stay exactly as they are — `saveHandover()` remains available for editing the handover date/file later on an already-completed award (e.g. correcting a mistake after the fact), it's just no longer the *only* way to set it the first time.

- [ ] **Step 10: Remove the forced-save-first gate in the template**

In `procurement-workflow.html`, replace the gated block (currently lines 397-419):

```html
                        @if (aHandoverMode() === 1 && awardEditable()) {
                          <div class="si-fld full">
                            <label>تسجيل تسليم الأرضية <span class="req">*</span></label>
                            <div class="si-grid">
                              <div class="si-fld">
                                <label>تاريخ التسليم</label>
                                <input type="date" [ngModel]="aHandoverDate()" (ngModelChange)="aHandoverDate.set($event)" />
                              </div>
                              <div class="si-fld">
                                <label>إثبات التسليم (PDF أو صورة)</label>
                                <input type="file" accept=".pdf,.png,.jpg,.jpeg" (change)="onHandoverFileChange($event)" />
                              </div>
                            </div>
                            @if (aw.siteHandoverMode === 1) {
                              <button class="si-btn sm" [disabled]="aHandoverSaving()" (click)="saveHandover()">
                                @if (aHandoverSaving()) { جاري الحفظ… } @else { تحديث تسليم الأرضية }
                              </button>
                              <span class="hint">لتصحيح التاريخ أو الملف بعد الحفظ الأول</span>
                            } @else {
                              <span class="hint">سيُسجَّل تسليم الأرضية تلقائيًا مع حفظ بيانات الترسية أدناه</span>
                            }
                          </div>
                        }
```

This removes the `احفظ بيانات الترسية أولًا…` error block entirely — the date+file inputs now render as soon as "مُسلَّمة للمقاول" is selected, regardless of whether the award has been saved before. The `@if (aw.siteHandoverMode === 1)` branch keeps `saveHandover()`'s standalone button available for edits *after* the first combined save (when `doc.SiteHandoverMode` is already persisted server-side) — matching the "both create and edit" scope decided for this item, since editing later still benefits from being able to update just the handover fields without re-submitting the whole award form.

- [ ] **Step 11: Manually verify item 6 end-to-end, both create and edit**

```bash
cd Frontend && npm run build
```

**Create path:** open step 6 for a brand-new مقاولات award (never saved before). Select "مُسلَّمة للمقاول" — confirm date+file inputs appear immediately, no error message. Fill in the whole award form including a handover date+file, click "حفظ بيانات الترسية" once — confirm both the award fields AND `SiteHandoverDate`/proof file are persisted after one click (reload the page and confirm `aw.siteHandoverDate`/`aw.siteHandoverProofFileName` are populated).

**Edit path:** on that same now-saved award, change the handover date and re-upload a different file using the now-visible `تحديث تسليم الأرضية` button — confirm it updates independently without needing to touch the rest of the award form.

- [ ] **Step 12: Full frontend build**

```bash
cd Frontend && npm run build
```

Expected: 0 errors.

- [ ] **Step 13: Commit**

```bash
git add Frontend/src/app/features/financial/procurement-workflow.ts Frontend/src/app/features/financial/procurement-workflow.html
git commit -m "feat(procurement): thousands-entry award inputs, inline advance-proof upload, funding bounds hint, contract-value advance base, one-step handover save"
```

---

## Final verification

- [ ] `cd Backend && dotnet test` — full suite passes.
- [ ] `cd Frontend && npm run build` — 0 errors.
- [ ] Manual walkthrough of all 6 items together on one مقاولات project and one توريدات project (توريدات has no advance payment or handover section at all — confirm items 1/2/3/4/6 correctly don't apply/render for it, per the existing `aw.requiresAdvancePayment`/`aw.projectNature === 'مقاولات'` gates, both untouched by this plan).
- [ ] Confirm إجمالي المتاح on the Projects page's بنك availability modal reflects the net-of-spend value after marking an advance payment done or recording execution spend on any sub-project in that financial year.
