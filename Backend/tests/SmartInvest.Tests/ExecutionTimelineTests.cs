using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;
using SmartInvest.Infrastructure.Data;
using SmartInvest.Infrastructure.Repositories;
using SmartInvest.Infrastructure.Services;

namespace SmartInvest.Tests;

/// <summary>يغطي ExecutionStageService.GetExecutionTimelineAsync — خط زمني حياة المشروع الكامل
/// لمخطط لوحة التحكم (نسبة التنفيذ العيني مقابل نسبة الصرف من قيمة العقد، بسقفين). القاعدة يجب أن
/// تطابق ProjectCompletionPolicy.Evaluate — انظر التعليق أعلى الدالة نفسها.</summary>
public sealed class ExecutionTimelineTests
{
    [Fact]
    public async Task Merges_stages_across_multiple_financial_year_cycles_in_chronological_order()
    {
        await using var context = CreateContext();
        var (projectId, _, cycle1Id) = await SeedProjectAsync(
            context, bankFunding: 600m, selfFunding: 400m, overrunPercentage: 0m, contractValue: 1000m);
        var year2 = new FinancialYear { Name = "2027/2028", StartDate = DateTime.UtcNow.Date.AddYears(1), EndDate = DateTime.UtcNow.Date.AddYears(2) };
        context.Add(year2);
        await context.SaveChangesAsync();
        var cycle2 = new SubProjectFinancialYear { SubProjectId = projectId, FinancialYearId = year2.FinancialYearId };
        context.Add(cycle2);
        await context.SaveChangesAsync();

        var day1 = DateTime.UtcNow.Date.AddDays(-10);
        var day2 = DateTime.UtcNow.Date.AddDays(-3); // في الدورة الثانية، لكن لاحق زمنيًا — يجب أن يأتي بعد day1
        context.ExecutionStages.AddRange(
            CompletedStage(projectId, cycle1Id, day1, progress: 40m, selfSpent: 200m, bankSpent: 0m),
            CompletedStage(projectId, cycle2.SubProjectFinancialYearId, day2, progress: 60m, selfSpent: 0m, bankSpent: 300m));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetExecutionTimelineAsync(projectId);

        Assert.True(result.HasContractValue);
        Assert.Equal(100m, result.ContractValueCeilingPercent);
        Assert.Equal(100m, result.MaxAllowedCeilingPercent); // TotalCost=ContractValue وبلا تجاوز
        Assert.Equal(2, result.Points.Count);
        Assert.Equal(40m, result.Points[0].CumulativeProgressPercent);
        Assert.Equal(20m, result.Points[0].CumulativeSpendPercent); // 200/1000
        Assert.Equal(100m, result.Points[1].CumulativeProgressPercent); // 40+60
        Assert.Equal(50m, result.Points[1].CumulativeSpendPercent); // (200+300)/1000
    }

    [Fact]
    public async Task Places_advance_payment_at_its_own_date_counted_once_not_as_progress()
    {
        await using var context = CreateContext();
        var (projectId, _, cycleId) = await SeedProjectAsync(
            context, bankFunding: 600m, selfFunding: 400m, overrunPercentage: 0m, contractValue: 1000m);
        var award = await context.ContractAwards.FirstAsync(x => x.SubProjectId == projectId);
        award.AdvancePaymentDone = true;
        award.AdvancePaymentBankAmount = 100m;
        award.AdvancePaymentSelfAmount = 0m;
        award.AdvancePaymentDate = DateTime.UtcNow.Date.AddDays(-20);
        context.ExecutionStages.Add(CompletedStage(
            projectId, cycleId, DateTime.UtcNow.Date.AddDays(-5), progress: 50m, selfSpent: 400m, bankSpent: 0m));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetExecutionTimelineAsync(projectId);

        Assert.Equal(2, result.Points.Count);
        Assert.Equal("الدفعة المقدمة", result.Points[0].Label);
        Assert.Equal(0m, result.Points[0].CumulativeProgressPercent);
        Assert.Equal(10m, result.Points[0].CumulativeSpendPercent); // 100/1000
        Assert.Equal(50m, result.Points[1].CumulativeProgressPercent);
        Assert.Equal(50m, result.Points[1].CumulativeSpendPercent); // (100+400)/1000
    }

    [Fact]
    public async Task Appends_today_point_from_the_open_stages_persisted_values()
    {
        await using var context = CreateContext();
        var (projectId, _, cycleId) = await SeedProjectAsync(
            context, bankFunding: 600m, selfFunding: 400m, overrunPercentage: 0m, contractValue: 1000m);
        context.ExecutionStages.AddRange(
            CompletedStage(projectId, cycleId, DateTime.UtcNow.Date.AddDays(-5), progress: 30m, selfSpent: 300m, bankSpent: 0m),
            OpenStage(projectId, cycleId, progress: 20m, selfSpent: 100m, bankSpent: 0m));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetExecutionTimelineAsync(projectId);

        Assert.Equal(2, result.Points.Count);
        var today = result.Points[^1];
        Assert.Equal("اليوم", today.Label);
        Assert.Equal(50m, today.CumulativeProgressPercent); // 30+20
        Assert.Equal(40m, today.CumulativeSpendPercent); // (300+100)/1000
        Assert.True(DateTime.UtcNow - today.Date < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task No_trailing_point_when_every_stage_is_already_completed()
    {
        await using var context = CreateContext();
        var (projectId, _, cycleId) = await SeedProjectAsync(
            context, bankFunding: 600m, selfFunding: 400m, overrunPercentage: 0m, contractValue: 1000m);
        context.ExecutionStages.Add(
            CompletedStage(projectId, cycleId, DateTime.UtcNow.Date.AddDays(-5), progress: 100m, selfSpent: 1000m, bankSpent: 0m));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetExecutionTimelineAsync(projectId);

        Assert.Single(result.Points);
        Assert.NotEqual("اليوم", result.Points[0].Label);
    }

    [Fact]
    public async Task Ceilings_express_total_cost_and_overrun_as_percent_of_contract_value()
    {
        await using var context = CreateContext();
        // المخطط = 100,000، تجاوز 10% → السقف الأقصى بالمبلغ = 110,000. قيمة العقد = 200,000
        // (مقصودة أكبر من المخطط لإعطاء نسبة نظيفة) → 110,000 / 200,000 × 100 = 55%.
        var (projectId, _, _) = await SeedProjectAsync(
            context, bankFunding: 60_000m, selfFunding: 40_000m, overrunPercentage: 10m, contractValue: 200_000m);

        var result = await CreateService(context).GetExecutionTimelineAsync(projectId);

        Assert.True(result.HasContractValue);
        Assert.Equal(100m, result.ContractValueCeilingPercent);
        Assert.Equal(55m, result.MaxAllowedCeilingPercent);
        Assert.Empty(result.Points);
    }

    [Fact]
    public async Task No_completed_award_returns_empty_timeline_without_throwing()
    {
        await using var context = CreateContext();
        var (projectId, _, cycleId) = await SeedProjectAsync(
            context, bankFunding: 600m, selfFunding: 400m, overrunPercentage: 0m, contractValue: null);
        context.ExecutionStages.Add(
            CompletedStage(projectId, cycleId, DateTime.UtcNow.Date, progress: 50m, selfSpent: 100m, bankSpent: 0m));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetExecutionTimelineAsync(projectId);

        Assert.False(result.HasContractValue);
        Assert.Null(result.ContractValue);
        Assert.Null(result.ContractValueCeilingPercent);
        Assert.Null(result.MaxAllowedCeilingPercent);
        Assert.Empty(result.Points);
        Assert.Equal(1000m, result.TotalCost); // لا يزال يُحسب — لا علاقة له بوجود عقد
    }

    [Fact]
    public async Task Unknown_subproject_throws_not_found()
    {
        await using var context = CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(context).GetExecutionTimelineAsync(999_999));
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ExecutionStageService CreateService(AppDbContext context) => new(
        context,
        new GenericRepository<ExecutionStage>(context),
        new SubProjectRepository(context),
        new UnitOfWork(context),
        new TestCurrentUser());

    private static async Task<(int ProjectId, int YearId, int CycleId)> SeedProjectAsync(
        AppDbContext context, decimal bankFunding, decimal selfFunding, decimal overrunPercentage, decimal? contractValue)
    {
        var status = new ProjectStatus { StatusName = "قيد التنفيذ" };
        var project = new SubProject
        {
            SubProjectName = "مشروع اختبار الخط الزمني",
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
        context.Add(cycle);
        await context.SaveChangesAsync();

        if (contractValue is decimal value)
        {
            var assignment = new ProjectAssignment
            {
                SubProjectId = project.SubProjectId,
                ContractValue = value,
                AssignmentDate = DateTime.UtcNow.Date,
            };
            context.Add(assignment);
            await context.SaveChangesAsync();
            context.ContractAwards.Add(new ContractAward
            {
                SubProjectId = project.SubProjectId,
                IsCompleted = true,
                ProjectAssignmentId = assignment.AssignmentId,
                ProjectAssignment = assignment,
            });
            await context.SaveChangesAsync();
        }

        return (project.SubProjectId, year.FinancialYearId, cycle.SubProjectFinancialYearId);
    }

    private static ExecutionStage CompletedStage(
        int projectId, int cycleId, DateTime completedAt, decimal progress, decimal selfSpent, decimal bankSpent) => new()
        {
            SubProjectId = projectId,
            SubProjectFinancialYearId = cycleId,
            Name = "مرحلة",
            PhysicalProgressPercent = progress,
            SelfFundingSpent = selfSpent,
            BankFundingSpent = bankSpent,
            IsCompleted = true,
            CompletedAt = completedAt,
        };

    private static ExecutionStage OpenStage(
        int projectId, int cycleId, decimal progress, decimal selfSpent, decimal bankSpent) => new()
        {
            SubProjectId = projectId,
            SubProjectFinancialYearId = cycleId,
            Name = "مرحلة جارية",
            PhysicalProgressPercent = progress,
            SelfFundingSpent = selfSpent,
            BankFundingSpent = bankSpent,
            IsCompleted = false,
        };

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId => "timeline-test-user";
        public string? Role => Roles.SuperAdmin;
    }
}
