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

    /// <summary>يكمّل الاختبار أعلاه: ذاك يثبّت عزل الدفعات المقدمة عبر السنوات، هذا يثبّت العزل نفسه
    /// لصرف مراحل التنفيذ (GetExecutionBankSpendAsync) — كان مغطى إيجابيًا فقط (أن الصرف يُخصم)
    /// بلا اختبار يثبّت أن صرف سنة أخرى لا يُخصم بالخطأ.</summary>
    [Fact]
    public async Task TotalAvailable_ignores_execution_spend_from_other_financial_years()
    {
        await using var context = CreateContext();
        var year1 = await SeedYearAsync(context, "2026/2027");
        var year2 = await SeedYearAsync(context, "2027/2028");

        var project1 = await SeedProjectAsync(context, year1.FinancialYearId, bankFunding: 100_000m, selfFunding: 0m);
        await SeedExecutionSpendAsync(context, project1, year1.FinancialYearId, bankFundingSpent: 40_000m);

        context.BankAvailabilities.Add(new BankAvailability
        {
            FinancialYearId = year2.FinancialYearId,
            Amount = 50_000m,
            ReceivedDate = DateTime.UtcNow.Date,
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.GetForFinancialYearAsync(year2.FinancialYearId);

        // صرف تنفيذ project1 مرتبط بدورة year1 فقط (لا دورة له في year2)، فلا يُخصم من إتاحات year2
        Assert.Equal(50_000m, result.TotalAvailable);
    }

    /// <summary>يغطي قرار صاحب المنتج: مشروع ممتد عبر سنتين ماليتين، الدفعة المقدمة (بتاريخ ترسية معلوم عبر
    /// ProjectAssignment) تُخصم من سنة الترسية نفسها فقط — وليس من كل سنة مرتبطة بالمشروع.</summary>
    [Fact]
    public async Task TotalAvailable_multiyear_project_attributes_advance_to_award_year_only()
    {
        await using var context = CreateContext();
        var year1 = await SeedYearAsync(context, "2026/2027", new DateTime(2026, 7, 1));
        var year2 = await SeedYearAsync(context, "2027/2028", new DateTime(2027, 7, 1));

        var project = await SeedProjectAsync(context, year1.FinancialYearId, bankFunding: 100_000m, selfFunding: 0m);
        await LinkProjectToYearAsync(context, project, year2.FinancialYearId);

        // تاريخ الترسية 2026-09-01 يقع داخل مدى year1 (2026-07-01 → 2027-06-30)
        await SeedContractAwardAsync(context, project, advancePaymentDone: true, advancePaymentBankAmount: 45_000m,
            awardContractDate: new DateTime(2026, 9, 1), createAssignment: true);

        context.BankAvailabilities.Add(new BankAvailability
        {
            FinancialYearId = year1.FinancialYearId,
            Amount = 200_000m,
            ReceivedDate = DateTime.UtcNow.Date,
        });
        context.BankAvailabilities.Add(new BankAvailability
        {
            FinancialYearId = year2.FinancialYearId,
            Amount = 150_000m,
            ReceivedDate = DateTime.UtcNow.Date,
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result1 = await service.GetForFinancialYearAsync(year1.FinancialYearId);
        var result2 = await service.GetForFinancialYearAsync(year2.FinancialYearId);

        // year1: 200,000 - 45,000 (سنة الترسية) = 155,000
        Assert.Equal(155_000m, result1.TotalAvailable);
        // year2: 150,000 بلا خصم — الدفعة لا تُخصم مرتين
        Assert.Equal(150_000m, result2.TotalAvailable);
    }

    /// <summary>الحالة الاحتياطية: ترسية بلا ProjectAssignment إطلاقًا (لا تاريخ ترسية معروف) — تُخصم الدفعة
    /// من أقدم سنة مرتبطة بالمشروع (بحسب StartDate) لضمان احتسابها مرة واحدة بالضبط لا صفر مرات.</summary>
    [Fact]
    public async Task TotalAvailable_advance_without_assignment_falls_back_to_earliest_linked_year()
    {
        await using var context = CreateContext();
        var year1 = await SeedYearAsync(context, "2026/2027", new DateTime(2026, 7, 1));
        var year2 = await SeedYearAsync(context, "2027/2028", new DateTime(2027, 7, 1));

        // المشروع يُربَط بـ year2 أولًا ثم year1 عمدًا — لإثبات أن الاختيار بحسب StartDate الأقدم
        // وليس بحسب ترتيب الربط في قاعدة البيانات
        var project = await SeedProjectAsync(context, year2.FinancialYearId, bankFunding: 100_000m, selfFunding: 0m);
        await LinkProjectToYearAsync(context, project, year1.FinancialYearId);

        await SeedContractAwardAsync(context, project, advancePaymentDone: true, advancePaymentBankAmount: 60_000m);

        context.BankAvailabilities.Add(new BankAvailability
        {
            FinancialYearId = year1.FinancialYearId,
            Amount = 300_000m,
            ReceivedDate = DateTime.UtcNow.Date,
        });
        context.BankAvailabilities.Add(new BankAvailability
        {
            FinancialYearId = year2.FinancialYearId,
            Amount = 250_000m,
            ReceivedDate = DateTime.UtcNow.Date,
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result1 = await service.GetForFinancialYearAsync(year1.FinancialYearId);
        var result2 = await service.GetForFinancialYearAsync(year2.FinancialYearId);

        // year1 (الأقدم): 300,000 - 60,000 = 240,000
        Assert.Equal(240_000m, result1.TotalAvailable);
        // year2: 250,000 بلا خصم
        Assert.Equal(250_000m, result2.TotalAvailable);
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static BankAvailabilityService CreateService(AppDbContext context) => new(
        context,
        new TestCurrentUser());

    private static async Task<FinancialYear> SeedYearAsync(AppDbContext context, string name = "2026/2027", DateTime? startDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.Date;
        var year = new FinancialYear
        {
            Name = name,
            StartDate = start,
            EndDate = start.AddYears(1).AddDays(-1),
        };
        context.FinancialYears.Add(year);
        await context.SaveChangesAsync();
        return year;
    }

    private static async Task LinkProjectToYearAsync(AppDbContext context, SubProject project, int financialYearId)
    {
        context.Set<SubProjectFinancialYear>().Add(new SubProjectFinancialYear
        {
            SubProjectId = project.SubProjectId,
            FinancialYearId = financialYearId,
        });
        await context.SaveChangesAsync();
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
        AppDbContext context, SubProject project, bool advancePaymentDone, decimal advancePaymentBankAmount,
        DateTime? awardContractDate = null, DateTime? awardAssignmentDate = null, bool createAssignment = false)
    {
        int? assignmentId = null;
        if (createAssignment)
        {
            var contractType = new ContractType { ContractName = "نوع اختبار" };
            context.Add(contractType);
            await context.SaveChangesAsync();

            var assignment = new ProjectAssignment
            {
                SubProjectId = project.SubProjectId,
                ContractTypeId = contractType.ContractTypeId,
                AssignmentDate = awardAssignmentDate ?? DateTime.UtcNow.Date,
                ContractDate = awardContractDate,
                ExpectedStartDate = DateTime.UtcNow.Date,
                ExpectedEndDate = DateTime.UtcNow.Date.AddMonths(6),
            };
            context.Add(assignment);
            await context.SaveChangesAsync();
            assignmentId = assignment.AssignmentId;
        }

        context.ContractAwards.Add(new ContractAward
        {
            SubProjectId = project.SubProjectId,
            AdvancePaymentDone = advancePaymentDone,
            AdvancePaymentBankAmount = advancePaymentBankAmount,
            ProjectAssignmentId = assignmentId,
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
