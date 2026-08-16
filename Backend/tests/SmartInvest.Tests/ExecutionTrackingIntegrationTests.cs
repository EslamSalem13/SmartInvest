using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;
using SmartInvest.Infrastructure.Data;
using SmartInvest.Infrastructure.Repositories;
using SmartInvest.Infrastructure.Services;

namespace SmartInvest.Tests;

public class ExecutionTrackingIntegrationTests
{
    [Fact]
    public async Task Updating_stage_keeps_existing_proof_when_no_replacement_is_uploaded()
    {
        await using var context = CreateContext();
        var seeded = await SeedExecutionAsync(context, stageCompleted: false);
        var service = CreateService(context);

        await service.UpdateAsync(seeded.ProjectId, seeded.StageId, new UpdateExecutionStageDto
        {
            FinancialYearId = seeded.YearId,
            Name = "مرحلة معدلة",
            Deadline = DateTime.UtcNow.Date.AddDays(5),
            SelfFundingSpent = 1000m,
            PhysicalProgressPercent = 100m,
        });

        var stage = await context.ExecutionStages.FindAsync(seeded.StageId);
        Assert.NotNull(stage?.SelfFundingProofFile);
        Assert.Equal("old-proof.pdf", stage.SelfFundingProofFile.FileName);
    }

    [Fact]
    public async Task Completed_stage_cannot_be_edited_before_reopen()
    {
        await using var context = CreateContext();
        var seeded = await SeedExecutionAsync(context, stageCompleted: true);
        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(
            seeded.ProjectId, seeded.StageId,
            new UpdateExecutionStageDto
            {
                FinancialYearId = seeded.YearId,
                Name = "غير مسموح",
                Deadline = DateTime.UtcNow.Date,
            }));
    }

    [Fact]
    public async Task Presentation_memo_filter_never_returns_another_year()
    {
        await using var context = CreateContext();
        var first = new FinancialYear { Name = "2026/2027", StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddYears(1) };
        var second = new FinancialYear { Name = "2027/2028", StartDate = DateTime.UtcNow.Date.AddYears(1), EndDate = DateTime.UtcNow.Date.AddYears(2) };
        context.FinancialYears.AddRange(first, second);
        await context.SaveChangesAsync();
        context.PresentationMemos.AddRange(
            new PresentationMemo { Title = "الأولى", FinancialYearId = first.FinancialYearId },
            new PresentationMemo { Title = "الثانية", FinancialYearId = second.FinancialYearId },
            new PresentationMemo { Title = "سجل قديم غامض", FinancialYearId = null });
        await context.SaveChangesAsync();

        var result = await new PresentationMemoService(context).GetAllAsync(first.FinancialYearId);

        Assert.Single(result);
        Assert.Equal(first.FinancialYearId, result[0].FinancialYearId);
    }

    [Fact]
    public async Task Follow_up_filter_aggregates_only_selected_year_stages()
    {
        await using var context = CreateContext();
        var seeded = await SeedExecutionAsync(context, stageCompleted: true);
        var secondYear = new FinancialYear { Name = "2027/2028", StartDate = DateTime.UtcNow.Date.AddYears(1), EndDate = DateTime.UtcNow.Date.AddYears(2) };
        context.FinancialYears.Add(secondYear);
        await context.SaveChangesAsync();
        var secondCycle = new SubProjectFinancialYear { SubProjectId = seeded.ProjectId, FinancialYearId = secondYear.FinancialYearId };
        context.Set<SubProjectFinancialYear>().Add(secondCycle);
        await context.SaveChangesAsync();
        context.ExecutionStages.Add(new ExecutionStage
        {
            SubProjectId = seeded.ProjectId,
            SubProjectFinancialYearId = secondCycle.SubProjectFinancialYearId,
            Name = "مرحلة سنة أخرى",
            SelfFundingSpent = 5000m,
            PhysicalProgressPercent = 70m,
            IsCompleted = true,
        });
        await context.SaveChangesAsync();

        var project = await context.SubProjects
            .Include(x => x.MainProject)
            .Include(x => x.Status)
            .FirstAsync(x => x.SubProjectId == seeded.ProjectId);
        var repository = new Mock<ISubProjectRepository>();
        repository.Setup(x => x.SearchAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<SubProject>)[project], 1));
        var service = CreateService(context, repository.Object);
        var result = await service.GetFollowUpListAsync(seeded.YearId, null, null, null, null, null);

        var row = Assert.Single(result);
        Assert.Equal(100m, row.FinancialProgressPercent);
        Assert.Equal(100m, row.PhysicalProgressPercent);
        Assert.Equal(1000m, row.CompletionEligibility.TotalSpent);
    }

    [Fact]
    public async Task Financial_progress_percent_is_based_on_contract_value()
    {
        await using var context = CreateContext();
        // المخطط 1000 (بنكي) وقيمة العقد 800 — المنصرف 1000 على المرحلة الفعلية.
        var seeded = await SeedExecutionAsync(context, stageCompleted: true);
        var assignment = await context.Set<ProjectAssignment>().FirstAsync(x => x.SubProjectId == seeded.ProjectId);
        assignment.ContractValue = 800m;
        await context.SaveChangesAsync();

        var project = await context.SubProjects
            .Include(x => x.MainProject)
            .Include(x => x.Status)
            .FirstAsync(x => x.SubProjectId == seeded.ProjectId);
        var repository = new Mock<ISubProjectRepository>();
        repository.Setup(x => x.SearchAsync(
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<SubProject>)[project], 1));
        var service = CreateService(context, repository.Object);

        var result = await service.GetFollowUpListAsync(seeded.YearId, null, null, null, null, null);

        // 1000 / 800 = 125% — وليس 100% المحسوبة على الإجمالي المخطط
        Assert.Equal(125m, Assert.Single(result).FinancialProgressPercent);
    }

    [Fact]
    public async Task View_only_role_fails_follow_up_write_policy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var viewer = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "viewer"), new Claim(ClaimTypes.Role, "ViewOnly")], "test"));
        var policy = new AuthorizationPolicyBuilder()
            .RequireRole(Roles.FollowUpStaff.Split(','))
            .Build();

        var result = await authorization.AuthorizeAsync(viewer, null, policy);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Execution_spend_ceiling_is_based_on_planned_budget_not_contract_value()
    {
        await using var context = CreateContext();
        // المخطط = 80,000 + 20,000 = 100,000، نسبة التجاوز 10% → السقف الصحيح بعد الإصلاح = 110,000.
        // قيمة العقد = 105,000 (قريبة من سقف الترسية 110,000) → السقف الخاطئ القديم (قبل الإصلاح) كان
        // 105,000 × 1.10 = 115,500، أي أكبر من السقف الصحيح.
        var seeded = await SeedExecutionWithOverrunAsync(
            context, bankFunding: 80_000m, selfFunding: 20_000m, overrunPercentage: 10m, contractValue: 105_000m);
        var service = CreateService(context);

        // محاولة صرف 112,000: تتجاوز السقف الصحيح (110,000) لكنها أقل من السقف الخاطئ القديم (115,500) —
        // يجب أن تُرفض بعد الإصلاح، بينما كانت ستُقبل خطأً قبله.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(seeded.ProjectId, new CreateExecutionStageDto
        {
            FinancialYearId = seeded.YearId,
            Name = "مرحلة تختبر سقف الصرف",
            StartDate = DateTime.UtcNow.Date,
            Deadline = DateTime.UtcNow.Date.AddDays(5),
            BankFundingSpent = 112_000m,
            BankFundingProofFile = File("bank-proof.pdf"),
            PhysicalProgressPercent = 0m,
        }));

        Assert.Contains("يتجاوز الحد المسموح", ex.Message);
    }

    [Fact]
    public async Task Stage_deadline_before_start_date_is_rejected()
    {
        await using var context = CreateContext();
        var seeded = await SeedExecutionWithOverrunAsync(
            context, bankFunding: 80_000m, selfFunding: 20_000m, overrunPercentage: 0m, contractValue: 100_000m);
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(seeded.ProjectId, new CreateExecutionStageDto
        {
            FinancialYearId = seeded.YearId,
            Name = "مرحلة بتواريخ معكوسة",
            StartDate = DateTime.UtcNow.Date.AddDays(10),
            Deadline = DateTime.UtcNow.Date.AddDays(3),
        }));

        Assert.Contains("لا يمكن أن يسبق الموعد الابتدائي", ex.Message);
    }

    [Fact]
    public async Task Stage_start_date_is_required_on_create()
    {
        await using var context = CreateContext();
        var seeded = await SeedExecutionWithOverrunAsync(
            context, bankFunding: 80_000m, selfFunding: 20_000m, overrunPercentage: 0m, contractValue: 100_000m);
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(seeded.ProjectId, new CreateExecutionStageDto
        {
            FinancialYearId = seeded.YearId,
            Name = "مرحلة بلا بداية",
            Deadline = DateTime.UtcNow.Date.AddDays(3),
        }));

        Assert.Contains("الموعد الابتدائي", ex.Message);
    }

    [Fact]
    public async Task Advance_payment_stage_is_created_from_award_and_counted_once()
    {
        await using var context = CreateContext();
        var seeded = await SeedExecutionAsync(context, stageCompleted: true);
        var paymentDate = DateTime.UtcNow.Date.AddDays(-30);
        var award = await context.ContractAwards.FirstAsync(x => x.SubProjectId == seeded.ProjectId);
        award.AdvancePaymentDone = true;
        award.AdvancePaymentPercentage = 25m;
        award.AdvancePaymentBankAmount = 200m;
        award.AdvancePaymentSelfAmount = 0m;
        award.AdvancePaymentDate = paymentDate;
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var stages = await service.GetBySubProjectAsync(seeded.ProjectId, seeded.YearId);

        var advance = Assert.Single(stages, x => x.IsAdvancePayment);
        Assert.Equal(200m, advance.BankFundingSpent);
        Assert.Equal(paymentDate, advance.StartDate);
        Assert.Equal(paymentDate, advance.Deadline);
        Assert.True(advance.IsCompleted);

        // 1000 من المرحلة الفعلية + 200 دفعة مقدمة — مرة واحدة لا مرتين
        var eligibility = await service.GetCompletionEligibilityAsync(seeded.ProjectId, seeded.YearId);
        Assert.Equal(1200m, eligibility.TotalSpent);
        Assert.Equal(200m, eligibility.AdvancePaymentTotal);
        Assert.Equal(100m, eligibility.PhysicalProgressTotal);
    }

    [Fact]
    public async Task Advance_payment_stage_starts_at_contract_date_and_ends_at_payment_date()
    {
        await using var context = CreateContext();
        var seeded = await SeedExecutionAsync(context, stageCompleted: true);
        var contractDate = DateTime.UtcNow.Date.AddDays(-60);
        var paymentDate = DateTime.UtcNow.Date.AddDays(-45);
        var assignment = await context.Set<ProjectAssignment>().FirstAsync(x => x.SubProjectId == seeded.ProjectId);
        assignment.ContractDate = contractDate;
        var award = await context.ContractAwards.FirstAsync(x => x.SubProjectId == seeded.ProjectId);
        award.AdvancePaymentDone = true;
        award.AdvancePaymentBankAmount = 200m;
        award.AdvancePaymentDate = paymentDate;
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var stages = await service.GetBySubProjectAsync(seeded.ProjectId, seeded.YearId);

        var advance = Assert.Single(stages, x => x.IsAdvancePayment);
        Assert.Equal(contractDate, advance.StartDate);
        Assert.Equal(paymentDate, advance.Deadline);
    }

    [Fact]
    public async Task Advance_payment_stage_falls_back_to_contract_date_when_payment_date_is_missing()
    {
        await using var context = CreateContext();
        var seeded = await SeedExecutionAsync(context, stageCompleted: true);
        var contractDate = DateTime.UtcNow.Date.AddDays(-60);
        var assignment = await context.Set<ProjectAssignment>().FirstAsync(x => x.SubProjectId == seeded.ProjectId);
        assignment.ContractDate = contractDate;
        var award = await context.ContractAwards.FirstAsync(x => x.SubProjectId == seeded.ProjectId);
        award.AdvancePaymentDone = true;
        award.AdvancePaymentBankAmount = 200m;
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var stages = await service.GetBySubProjectAsync(seeded.ProjectId, seeded.YearId);

        var advance = Assert.Single(stages, x => x.IsAdvancePayment);
        Assert.Equal(contractDate, advance.StartDate);
        Assert.Equal(contractDate, advance.Deadline);
    }

    [Fact]
    public async Task Advance_payment_stage_is_removed_when_award_payment_is_undone()
    {
        await using var context = CreateContext();
        var seeded = await SeedExecutionAsync(context, stageCompleted: true);
        var award = await context.ContractAwards.FirstAsync(x => x.SubProjectId == seeded.ProjectId);
        award.AdvancePaymentDone = true;
        award.AdvancePaymentBankAmount = 200m;
        award.AdvancePaymentDate = DateTime.UtcNow.Date;
        await context.SaveChangesAsync();
        var service = CreateService(context);
        await service.SyncAdvancePaymentStageAsync(seeded.ProjectId);
        Assert.True(await context.ExecutionStages.AnyAsync(x => x.IsAdvancePayment));

        award.AdvancePaymentDone = false;
        await context.SaveChangesAsync();
        await service.SyncAdvancePaymentStageAsync(seeded.ProjectId);

        Assert.False(await context.ExecutionStages.AnyAsync(x => x.IsAdvancePayment));
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ExecutionStageService CreateService(AppDbContext context, ISubProjectRepository? subProjectRepository = null) => new(
        context,
        new GenericRepository<ExecutionStage>(context),
        subProjectRepository ?? new SubProjectRepository(context),
        new UnitOfWork(context),
        new TestCurrentUser());

    private static async Task<(int ProjectId, int StageId, int YearId)> SeedExecutionAsync(
        AppDbContext context, bool stageCompleted)
    {
        var status = new ProjectStatus { StatusName = "قيد التنفيذ" };
        var project = new SubProject
        {
            SubProjectName = "مشروع اختبار",
            ProjectNature = "مقاولات",
            IsApproved = true,
            Status = status,
            BankFunding = 1000m,
            SelfFunding = 0m,
            MainProject = new MainProject { MainProjectName = "مشروع رئيسي" },
        };
        var year = new FinancialYear
        {
            Name = "2026/2027",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
        };
        context.AddRange(status, project, year);
        await context.SaveChangesAsync();
        var cycle = new SubProjectFinancialYear { SubProjectId = project.SubProjectId, FinancialYearId = year.FinancialYearId };
        var assignment = new ProjectAssignment
        {
            SubProjectId = project.SubProjectId,
            ContractValue = 1000m,
            AssignmentDate = DateTime.UtcNow.Date,
        };
        context.AddRange(cycle, assignment);
        await context.SaveChangesAsync();
        context.ContractAwards.Add(new ContractAward
        {
            SubProjectId = project.SubProjectId,
            IsCompleted = true,
            ProjectAssignmentId = assignment.AssignmentId,
            ProjectAssignment = assignment,
        });
        var stage = new ExecutionStage
        {
            SubProjectId = project.SubProjectId,
            SubProjectFinancialYearId = cycle.SubProjectFinancialYearId,
            Name = "مرحلة فعلية",
            Deadline = DateTime.UtcNow.Date.AddDays(5),
            SelfFundingSpent = 1000m,
            PhysicalProgressPercent = 100m,
            SelfFundingProofFile = new StoredFile
            {
                FileName = "old-proof.pdf",
                FileExtension = ".pdf",
                Content = [1, 2, 3],
                FileSize = 3,
            },
            PhysicalProgressProofFile = new StoredFile
            {
                FileName = "old-progress.pdf",
                FileExtension = ".pdf",
                Content = [4, 5, 6],
                FileSize = 3,
            },
            IsCompleted = stageCompleted,
        };
        context.ExecutionStages.Add(stage);
        await context.SaveChangesAsync();
        return (project.SubProjectId, stage.ExecutionStageId, year.FinancialYearId);
    }

    /// <summary>يبذر مشروعًا فرعيًا مُرسى عليه بقيمة عقد ونسبة تجاوز محددتين، بلا مرحلة تنفيذ مبدئية —
    /// يُستخدم لاختبار سقف الصرف المسموح به (GetAllowedCeilingAsync) مباشرة عبر CreateAsync.</summary>
    private static async Task<(int ProjectId, int YearId)> SeedExecutionWithOverrunAsync(
        AppDbContext context, decimal bankFunding, decimal selfFunding, decimal overrunPercentage, decimal contractValue)
    {
        var status = new ProjectStatus { StatusName = "قيد التنفيذ" };
        var project = new SubProject
        {
            SubProjectName = "مشروع اختبار سقف التجاوز",
            ProjectNature = "مقاولات",
            IsApproved = true,
            Status = status,
            BankFunding = bankFunding,
            SelfFunding = selfFunding,
            OverrunPercentage = overrunPercentage,
            MainProject = new MainProject { MainProjectName = "مشروع رئيسي" },
        };
        var year = new FinancialYear
        {
            Name = "2026/2027",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
        };
        context.AddRange(status, project, year);
        await context.SaveChangesAsync();
        var cycle = new SubProjectFinancialYear { SubProjectId = project.SubProjectId, FinancialYearId = year.FinancialYearId };
        var assignment = new ProjectAssignment
        {
            SubProjectId = project.SubProjectId,
            ContractValue = contractValue,
            AssignmentDate = DateTime.UtcNow.Date,
        };
        context.AddRange(cycle, assignment);
        await context.SaveChangesAsync();
        context.ContractAwards.Add(new ContractAward
        {
            SubProjectId = project.SubProjectId,
            IsCompleted = true,
            ProjectAssignmentId = assignment.AssignmentId,
            ProjectAssignment = assignment,
        });
        await context.SaveChangesAsync();
        return (project.SubProjectId, year.FinancialYearId);
    }

    private static FileUploadDto File(string name) => new()
    {
        FileName = name,
        FileExtension = ".pdf",
        FileSize = 1,
        Content = [1],
    };

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId => "test-user";
        public string? Role => Roles.SuperAdmin;
    }
}
