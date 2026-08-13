using Microsoft.EntityFrameworkCore;
using SmartInvest.Domain.Entities;
using SmartInvest.Infrastructure.Data;
using SmartInvest.Infrastructure.Services;

namespace SmartInvest.Tests;

public class DashboardServiceTests
{
    [Fact]
    public async Task Dashboard_uses_only_selected_year_stages_and_sums_physical_progress()
    {
        await using var context = CreateContext();
        var firstYear = new FinancialYear
        {
            Name = "2026/2027",
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2027, 6, 30),
        };
        var secondYear = new FinancialYear
        {
            Name = "2027/2028",
            StartDate = new DateTime(2027, 7, 1),
            EndDate = new DateTime(2028, 6, 30),
        };
        var project = CreateCompletedProject();
        context.AddRange(firstYear, secondYear, project);
        await context.SaveChangesAsync();

        var firstCycle = new SubProjectFinancialYear
        {
            SubProjectId = project.SubProjectId,
            FinancialYearId = firstYear.FinancialYearId,
        };
        var secondCycle = new SubProjectFinancialYear
        {
            SubProjectId = project.SubProjectId,
            FinancialYearId = secondYear.FinancialYearId,
        };
        context.AddRange(firstCycle, secondCycle);
        await context.SaveChangesAsync();

        context.ExecutionStages.AddRange(
            Stage(project, firstCycle, "المرحلة الأولى", 40m, 1000m, 0m, isCompleted: true),
            Stage(project, firstCycle, "المرحلة الثانية", 60m, 0m, 2000m, isCompleted: true),
            Stage(project, firstCycle, "التسليم النهائي", 0m, 0m, 0m, isCompleted: true, isFinal: true),
            Stage(project, secondCycle, "مرحلة سنة أخرى", 15m, 9000m, 0m, isCompleted: false));
        await context.SaveChangesAsync();

        var service = new DashboardService(context);
        var first = await service.GetOverviewAsync(firstYear.FinancialYearId);
        var second = await service.GetOverviewAsync(secondYear.FinancialYearId);

        Assert.Equal(100m, first.ProjectMetrics.AveragePhysicalProgress);
        Assert.Equal(3000m, first.FinancialMetrics.TotalSpent);
        Assert.Equal(1, first.ProjectMetrics.CompletedCount);
        Assert.Equal(1, first.Charts.ProgressDistribution.Single(x => x.Name == "100%").Value);
        Assert.Equal(1, first.Charts.StatusDistribution.Single(x => x.Name == "منتهي").Value);

        Assert.Equal(15m, second.ProjectMetrics.AveragePhysicalProgress);
        Assert.Equal(9000m, second.FinancialMetrics.TotalSpent);
        Assert.Equal(0, second.ProjectMetrics.CompletedCount);
        Assert.Equal(1, second.Charts.ProgressDistribution.Single(x => x.Name == "1–25%").Value);
        Assert.Equal(0, second.Charts.StatusDistribution.Single(x => x.Name == "منتهي").Value);
        Assert.Equal(1, second.Charts.StatusDistribution.Single(x => x.Name == "جاري التنفيذ").Value);
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SubProject CreateCompletedProject()
    {
        var program = new MainProgram { ProgramName = "برنامج" };
        var subProgram = new SubProgram { SubProgramName = "برنامج فرعي", MainProgram = program };
        var mainProject = new MainProject { MainProjectName = "مشروع رئيسي", SubProgram = subProgram };

        return new SubProject
        {
            SubProjectName = "مشروع متعدد السنوات",
            ProjectNature = "مقاولات",
            IsApproved = true,
            ExecutionCompletedAt = new DateTime(2027, 1, 1),
            Status = new ProjectStatus { StatusName = "منتهي" },
            MainProject = mainProject,
            Markaz = new Markaz { MarkazName = "مركز" },
            Priority = new ProjectPriority { Priority = "عالية" },
            BankFunding = 5000m,
            SelfFunding = 3000m,
        };
    }

    private static ExecutionStage Stage(
        SubProject project,
        SubProjectFinancialYear cycle,
        string name,
        decimal progress,
        decimal selfSpent,
        decimal bankSpent,
        bool isCompleted,
        bool isFinal = false) => new()
        {
            SubProjectId = project.SubProjectId,
            SubProjectFinancialYearId = cycle.SubProjectFinancialYearId,
            Name = name,
            PhysicalProgressPercent = progress,
            SelfFundingSpent = selfSpent,
            BankFundingSpent = bankSpent,
            IsCompleted = isCompleted,
            IsFinalDelivery = isFinal,
        };
}
