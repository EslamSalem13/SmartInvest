using Microsoft.EntityFrameworkCore;
using Moq;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Application.Services.Import;
using SmartInvest.Domain.Entities;
using SmartInvest.Infrastructure.Data;
using SmartInvest.Infrastructure.Repositories;

namespace SmartInvest.Tests;

/// <summary>يغطي CommitAsync لملف "خطة مقترحة" — تحديدًا سبب ظهور "0 مشروعات رئيسية و0 مشروعات فرعية"
/// في نافذة النتيجة رغم نجاح الاستيراد: MainProjectsCreated/SubProjectsCreated يُحتسبان فقط لما
/// يُنشأ حديثًا، لا لما كان موجودًا بالفعل وأُعيد استخدامه. راجع
/// docs/superpowers/specs (بحث "0 مشروعات") — الإصلاح يضيف عدّاد MainProjectsAlreadyExisted
/// المقابل لـ SubProjectsAlreadyLinked الموجود بالفعل، ثم يعرضهما الفرونت في وضع "مقترحة".</summary>
public sealed class SuggestedPlanImportCommitTests
{
    [Fact]
    public async Task Reused_existing_main_project_counts_as_already_existed_not_created()
    {
        await using var context = CreateContext();
        var seed = await SeedLookupsAsync(context);

        // المشروع الرئيسي موجود بالفعل بنفس الاسم تحت نفس البرنامج الفرعي — يجب أن يُعاد استخدامه.
        var existingMainProject = new MainProject
        {
            MainProjectName = "مشروع رئيسي قائم",
            ExecutingAgency = string.Empty,
            SubProgramId = seed.SubProgramId,
            IsApproved = false,
        };
        context.MainProjects.Add(existingMainProject);
        await context.SaveChangesAsync();

        var file = new ParsedImportFile
        {
            Mode = ImportMode.Suggested,
            Rows =
            [
                new ParsedImportRow
                {
                    RowIndex = 1,
                    MainProgramName = seed.MainProgramName,
                    SubProgramName = seed.SubProgramName,
                    MainProjectCode = "",
                    MainProjectName = "مشروع رئيسي قائم", // نفس اسم existingMainProject بالضبط
                    ProjectLevelName = seed.ProjectLevelName,
                    ExecutiveAgencyName = seed.AgencyName,
                    MarkazName = seed.MarkazName,
                    SubProjectCode = "",
                    SubProjectName = "مشروع فرعي جديد كليًا",
                    ComponentTypeName = seed.ComponentTypeName,
                    BankFunding = 100_000m,
                    SelfFunding = 50_000m,
                    AccountingUnitName = seed.AccountingUnitName,
                    ProjectNature = "مقاولات",
                },
            ],
        };
        var dto = new ImportCommitDto { ImportId = "test-import", FinancialYearId = seed.FinancialYearId };

        var service = CreateService(context);
        var result = await service.CommitAsync(file, dto, CancellationToken.None);

        Assert.Empty(result.Failed);
        // الأصل المُعاد إصلاحه: MainProjectsCreated=0 كان يظهر بلا أي تفسير. الآن معه عدّاد يوضّح السبب.
        Assert.Equal(0, result.MainProjectsCreated);
        Assert.Equal(1, result.MainProjectsAlreadyExisted);
        // المشروع الفرعي جديد كليًا — يثبت أن الإصلاح لا يكسر مسار الإنشاء العادي.
        Assert.Equal(1, result.SubProjectsCreated);
        Assert.Equal(0, result.SubProjectsAlreadyLinked);

        Assert.Single(context.MainProjects); // لم يُنشأ نسخة مكرّرة من "مشروع رئيسي قائم"
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SuggestedPlanImportService CreateService(AppDbContext context) => new(
        new GenericRepository<Markaz>(context),
        new GenericRepository<Governorate>(context),
        new GenericRepository<MainProgram>(context),
        new GenericRepository<SubProgram>(context),
        new GenericRepository<ExecutiveAgency>(context),
        new GenericRepository<ProjectLevel>(context),
        new GenericRepository<ComponentType>(context),
        new GenericRepository<AccountingUnit>(context),
        new GenericRepository<ProjectPriority>(context),
        new GenericRepository<ProjectStatus>(context),
        new GenericRepository<FinancialYear>(context),
        new GenericRepository<PlanProject>(context),
        new GenericRepository<SubProjectFinancialYear>(context),
        Mock.Of<ILookupService>(),
        Mock.Of<IExecutiveAgencyService>(),
        new MainProjectRepository(context),
        new SubProjectRepository(context),
        new PlanRepo(context),
        new UnitOfWork(context),
        Mock.Of<IMeasurementResolutionService>(),
        Mock.Of<ILookupMatchSuggestionService>());

    private sealed record SeedResult(
        int FinancialYearId, string MainProgramName, int SubProgramId, string SubProgramName,
        string ProjectLevelName, string AgencyName, string MarkazName, string ComponentTypeName, string AccountingUnitName);

    /// <summary>يزرع كل المطلوب حتى يحلّ ResolveNamedLookupAsync (ولا يشترط أي resolution صريح — هو
    /// نفسه يقرأ كل صفوف المستودع أولًا) كل اسم في الصف أدناه دون أي تعارض أو حاجة لإنشاء جديد.</summary>
    private static async Task<SeedResult> SeedLookupsAsync(AppDbContext context)
    {
        var governorate = new Governorate { GovernorateName = "المنوفية" };
        var markaz = new Markaz { MarkazName = "مركز اختبار", Governorate = governorate };
        var mainProgram = new MainProgram { ProgramName = "برنامج رئيسي اختبار" };
        var subProgram = new SubProgram { SubProgramName = "برنامج فرعي اختبار", MainProgram = mainProgram };
        var agency = new ExecutiveAgency { AgencyName = "جهة تنفيذ اختبار" };
        var projectLevel = new ProjectLevel { Name = "مشترك" };
        var componentType = new ComponentType { Name = "تشييدات" };
        var accountingUnit = new AccountingUnit { Name = "وحدة اختبار" };
        var priority = new ProjectPriority { Priority = "منخفضة" };
        var status = new ProjectStatus { StatusName = "جديد" };
        var year = new FinancialYear
        {
            Name = "2026/2027",
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2027, 6, 30),
        };

        context.AddRange(governorate, markaz, mainProgram, subProgram, agency, projectLevel, componentType, accountingUnit, priority, status, year);
        await context.SaveChangesAsync();

        return new SeedResult(
            year.FinancialYearId, mainProgram.ProgramName, subProgram.SubProgramId, subProgram.SubProgramName,
            projectLevel.Name, agency.AgencyName, markaz.MarkazName, componentType.Name, accountingUnit.Name);
    }
}
