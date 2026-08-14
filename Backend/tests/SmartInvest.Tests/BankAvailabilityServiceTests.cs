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
            Amount = 250_000m,
            ReceivedDate = DateTime.UtcNow.Date,
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetForFinancialYearAsync(year.FinancialYearId);

        // 250,000 (مستلم) - 30,000 (دفعة مقدمة تم صرفها فعليًا فقط، مشروع 2 لم تُصرف بعد فتُستبعد) - 20,000 (صرف تنفيذ) = 200,000
        Assert.Equal(200_000m, result.TotalAvailable);
        Assert.Equal(300_000m, result.TotalBankFunding);
        // RemainingAvailable = TotalBankFunding (300,000) - receipts (250,000) = 50,000
        Assert.Equal(50_000m, result.RemainingAvailable);
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
        context.Set<SubProjectFinancialYear>().Add(new SubProjectFinancialYear
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
        var cycle = await context.Set<SubProjectFinancialYear>()
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
