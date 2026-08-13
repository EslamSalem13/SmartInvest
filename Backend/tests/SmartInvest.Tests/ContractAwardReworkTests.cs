using Microsoft.EntityFrameworkCore;
using Moq;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Enums;
using SmartInvest.Infrastructure.Data;
using SmartInvest.Infrastructure.Services;

namespace SmartInvest.Tests;

public sealed class ContractAwardReworkTests
{
    [Fact]
    public async Task Cannot_start_procurement_stage_when_memo_is_attached_but_not_completed()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 100_000m, selfFunding: 0m);
        var memo = new PresentationMemo
        {
            Title = "مذكرة اختبار",
            ContractingMethod = ContractingMethod.PublicTender,
            IsCompleted = false,
        };
        context.Set<PresentationMemo>().Add(memo);
        await context.SaveChangesAsync();
        context.Set<PresentationMemoSubProject>().Add(new PresentationMemoSubProject
        {
            PresentationMemoId = memo.Id,
            SubProjectId = project.SubProjectId,
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UploadVersionAsync(project.SubProjectId, ProcurementStage.TenderDocument, new UploadProcurementVersionDto
            {
                Files = new Dictionary<string, FileUploadDto> { ["file"] = File("f.pdf") },
            }));
    }

    [Fact]
    public async Task Can_start_procurement_stage_when_memo_is_completed()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 100_000m, selfFunding: 0m);
        var memo = new PresentationMemo
        {
            Title = "مذكرة اختبار",
            ContractingMethod = ContractingMethod.PublicTender,
            IsCompleted = true,
        };
        context.Set<PresentationMemo>().Add(memo);
        await context.SaveChangesAsync();
        context.Set<PresentationMemoSubProject>().Add(new PresentationMemoSubProject
        {
            PresentationMemoId = memo.Id,
            SubProjectId = project.SubProjectId,
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await service.UploadVersionAsync(project.SubProjectId, ProcurementStage.TenderDocument, new UploadProcurementVersionDto
        {
            Files = new Dictionary<string, FileUploadDto> { ["file"] = File("f.pdf") },
        });

        Assert.Equal(1, await context.TenderDocuments.CountAsync());
    }

    [Fact]
    public async Task Contract_value_above_planned_plus_overrun_blocks_completion()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 100_000m, selfFunding: 0m, projectNature: "توريدات", overrunPercentage: 10m);
        var (memo, contractor, contractType) = await SeedAwardPrereqsAsync(context, project.SubProjectId);
        var service = CreateService(context);

        await service.SetContractAwardDetailsAsync(project.SubProjectId, new SetContractAwardDetailsDto
        {
            ContractorId = contractor.ContractorId,
            ContractDate = DateTime.UtcNow.Date,
            ContractValue = 115_000m, // planned 100,000 + 10% = 110,000 ceiling — this exceeds it
            ExecutionDurationMonths = 1,
        });

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SetCompletionAsync(project.SubProjectId, ProcurementStage.ContractAward, true));
        Assert.Contains("تتجاوز", ex.Message);
    }

    [Fact]
    public async Task Contract_value_within_overrun_ceiling_allows_completion_and_computes_savings()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 100_000m, selfFunding: 0m, projectNature: "توريدات", overrunPercentage: 10m);
        var (memo, contractor, contractType) = await SeedAwardPrereqsAsync(context, project.SubProjectId);
        var service = CreateService(context);

        await service.SetContractAwardDetailsAsync(project.SubProjectId, new SetContractAwardDetailsDto
        {
            ContractorId = contractor.ContractorId,
            ContractDate = DateTime.UtcNow.Date,
            ContractValue = 90_000m, // below planned 100,000 → 10,000 savings, no supply project needs handover fields
            ExecutionDurationMonths = 1,
        });

        await service.SetCompletionAsync(project.SubProjectId, ProcurementStage.ContractAward, true);

        var details = (await service.GetOverviewAsync(project.SubProjectId))
            .Stages.Single(s => s.Stage == "contract-award").ContractAward!;
        Assert.Equal(10_000m, details.Savings);
    }

    [Fact]
    public async Task Supply_project_completes_without_handover_fields_and_auto_sets_handover_date()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 50_000m, selfFunding: 0m, projectNature: "توريدات");
        var (memo, contractor, contractType) = await SeedAwardPrereqsAsync(context, project.SubProjectId);
        var service = CreateService(context);

        await service.SetContractAwardDetailsAsync(project.SubProjectId, new SetContractAwardDetailsDto
        {
            ContractorId = contractor.ContractorId,
            ContractDate = DateTime.UtcNow.Date,
            ContractValue = 40_000m,
            ExecutionDurationMonths = 1,
            // deliberately no SiteHandoverMode/Date/Proof — must not be required for توريدات
        });

        await service.SetCompletionAsync(project.SubProjectId, ProcurementStage.ContractAward, true);

        var award = await context.ContractAwards.AsNoTracking().SingleAsync(x => x.SubProjectId == project.SubProjectId);
        Assert.NotNull(award.SiteHandoverDate);
        Assert.Null(award.SiteHandoverMode);
    }

    [Fact]
    public async Task Contract_type_is_derived_from_memo_contracting_method_not_chosen_independently()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context, bankFunding: 50_000m, selfFunding: 0m, projectNature: "توريدات");
        var (memo, contractor, _) = await SeedAwardPrereqsAsync(context, project.SubProjectId);
        var service = CreateService(context);

        await service.SetContractAwardDetailsAsync(project.SubProjectId, new SetContractAwardDetailsDto
        {
            ContractorId = contractor.ContractorId,
            ContractDate = DateTime.UtcNow.Date,
            ContractValue = 10_000m,
            ExecutionDurationMonths = 1,
        });

        var assignment = await context.Set<ProjectAssignment>().AsNoTracking().SingleAsync(a => a.SubProjectId == project.SubProjectId);
        var createdType = await context.Set<ContractType>().AsNoTracking().SingleAsync(t => t.ContractTypeId == assignment.ContractTypeId);
        Assert.Equal(ContractingMethodLabels.ToLabel(memo.ContractingMethod), createdType.ContractName);

        // رقم العقد مُولَّد من AssignmentId، لا من أي إدخال
        Assert.Equal(assignment.AssignmentId.ToString(), assignment.ContractNumber);
    }

    private static async Task<(PresentationMemo memo, Contractor contractor, ContractType? contractType)> SeedAwardPrereqsAsync(
        AppDbContext context, int subProjectId)
    {
        var memo = new PresentationMemo
        {
            Title = "مذكرة اختبار",
            ContractingMethod = ContractingMethod.PublicTender,
            IsCompleted = true,
        };
        context.Set<PresentationMemo>().Add(memo);
        var contractor = new Contractor { ContractorName = "مقاول اختبار", IsActive = true };
        context.Set<Contractor>().Add(contractor);
        await context.SaveChangesAsync();
        context.Set<PresentationMemoSubProject>().Add(new PresentationMemoSubProject
        {
            PresentationMemoId = memo.Id,
            SubProjectId = subProjectId,
        });
        var award = new ContractAward
        {
            SubProjectId = subProjectId,
            CurrentVersionNumber = 1,
        };
        context.Set<ContractAward>().Add(award);
        // المرحلة السابقة (التقييم المالي) يجب أن تكون مكتملة حتى تُفتح مرحلة الترسية للتعديل —
        // EnsurePreviousStageCompletedAsync تتحقق من هذا في كل من SetContractAwardDetailsAsync وSetCompletionAsync.
        context.Set<FinancialEvaluation>().Add(new FinancialEvaluation
        {
            SubProjectId = subProjectId,
            IsCompleted = true,
        });
        await context.SaveChangesAsync();

        // نسخة أولى بالملفين الإلزاميين (أمر الإسناد + العقد) — SetCompletionAsync يتحقق من وجودهما
        // في أحدث نسخة قبل استدعاء ValidateContractAwardForCompletionAsync، بنفس نمط AnnouncementVersion
        // في ProcurementDurationAndAuthorizationTests.
        context.Set<ContractAwardVersion>().Add(new ContractAwardVersion
        {
            ContractAwardId = award.Id,
            VersionNumber = 1,
            AwardOrder = StoredTestFile("award-order.pdf"),
            Contract = StoredTestFile("contract.pdf"),
        });
        await context.SaveChangesAsync();

        return (memo, contractor, null);
    }

    private static ProcurementService CreateService(AppDbContext context) => new(
        context,
        Mock.Of<IExecutionStageService>(),
        new TestCurrentUser());

    private static AppDbContext CreateContext() => new(
        new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // UploadAsync يفتح معاملة DB حقيقية (تعمل ضد SQL Server في الإنتاج) — مزوّد InMemory
            // يرفض دعم المعاملات عمدًا ويرمي استثناءً بدل تجاهلها بصمت، فنتجاهل هذا التحذير هنا فقط.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task<SubProject> SeedProjectAsync(
        AppDbContext context, decimal bankFunding, decimal selfFunding,
        string projectNature = "توريدات", decimal? overrunPercentage = null)
    {
        var project = new SubProject
        {
            SubProjectName = "اختبار الترسية",
            IsApproved = true,
            BankFunding = bankFunding,
            SelfFunding = selfFunding,
            ProjectNature = projectNature,
            OverrunPercentage = overrunPercentage,
        };
        context.SubProjects.Add(project);
        await context.SaveChangesAsync();
        return project;
    }

    private static FileUploadDto File(string name) => new()
    {
        FileName = name,
        FileExtension = ".pdf",
        FileSize = 1,
        Content = [1],
    };

    private static StoredFile StoredTestFile(string name) => new()
    {
        FileName = name,
        FileExtension = ".pdf",
        FileSize = 1,
        Content = [1],
    };

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId => "award-rework-test-user";
        public string? Role => Roles.SuperAdmin;
    }
}
