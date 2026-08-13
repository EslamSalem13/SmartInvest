using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmartInvest.API.Controllers;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Enums;
using SmartInvest.Infrastructure.Data;
using SmartInvest.Infrastructure.Services;

namespace SmartInvest.Tests;

public sealed class ProcurementDurationAndAuthorizationTests
{
    [Theory]
    [InlineData(nameof(ProcurementController.Complete))]
    [InlineData(nameof(ProcurementController.Fail))]
    public async Task Planning_and_financial_employees_are_forbidden_from_completing_or_failing_stage(string action)
    {
        Assert.False(await IsAuthorizedAsync(action, Roles.PlanningEmployee));
        Assert.False(await IsAuthorizedAsync(action, Roles.FinancialEmployee));
        Assert.True(await IsAuthorizedAsync(action, Roles.FinancialManager));
        Assert.True(await IsAuthorizedAsync(action, Roles.SuperAdmin));
    }

    [Theory]
    [InlineData(Roles.PlanningEmployee, false)]
    [InlineData(Roles.FinancialEmployee, false)]
    [InlineData(Roles.FinancialManager, false)]
    [InlineData(Roles.PlanningManager, true)]
    [InlineData(Roles.SuperAdmin, true)]
    public async Task Only_planning_manager_and_super_admin_can_change_duration(string role, bool expected)
    {
        Assert.Equal(expected, await IsAuthorizedAsync(nameof(ProcurementController.SetDuration), role));
    }

    [Fact]
    public async Task Completing_announcement_activates_next_normal_stage_with_stable_seven_day_duration()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context);
        context.TenderDocuments.Add(new TenderDocument
        {
            SubProjectId = project.SubProjectId,
            IsCompleted = true,
        });
        var announcement = new Announcement
        {
            SubProjectId = project.SubProjectId,
            CurrentVersionNumber = 1,
            AnnouncementDate = DateTime.UtcNow.AddDays(-16),
            DurationDays = 15,
        };
        context.Announcements.Add(announcement);
        await context.SaveChangesAsync();
        context.AnnouncementVersions.Add(new AnnouncementVersion
        {
            AnnouncementId = announcement.Id,
            VersionNumber = 1,
            NewspaperAdvertisement = File("newspaper.pdf"),
            PortalAdvertisement = File("portal.pdf"),
            CompetentAuthorityApproval = File("approval.pdf"),
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        await service.SetCompletionAsync(project.SubProjectId, ProcurementStage.Announcement, true);

        var activated = await context.OpeningEnvelopes.AsNoTracking().SingleAsync();
        Assert.Equal(7, activated.DurationDays);
        Assert.NotNull(activated.DurationSetAt);

        var firstRead = await service.GetStageAsync(project.SubProjectId, ProcurementStage.OpeningEnvelopes);
        var secondRead = await service.GetStageAsync(project.SubProjectId, ProcurementStage.OpeningEnvelopes);
        Assert.Equal(firstRead.Deadline, secondRead.Deadline);
        Assert.Equal(activated.DurationSetAt!.Value.AddDays(7), firstRead.Deadline);
    }

    [Fact]
    public async Task Linking_project_to_presentation_memo_activates_first_stage_once()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context);
        var year = new FinancialYear
        {
            Name = "2026/2027",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
        };
        context.FinancialYears.Add(year);
        await context.SaveChangesAsync();
        context.Set<SubProjectFinancialYear>().Add(new SubProjectFinancialYear
        {
            SubProjectId = project.SubProjectId,
            FinancialYearId = year.FinancialYearId,
        });
        await context.SaveChangesAsync();

        await new PresentationMemoService(context).CreateAsync(new CreatePresentationMemoDto
        {
            FinancialYearId = year.FinancialYearId,
            Title = "مذكرة اختبار",
            ContractingMethod = (int)ContractingMethod.PublicTender,
            SubProjectIds = [project.SubProjectId],
        });

        var firstStage = await context.TenderDocuments.AsNoTracking().SingleAsync();
        Assert.Equal(7, firstStage.DurationDays);
        Assert.NotNull(firstStage.DurationSetAt);
    }

    [Fact]
    public async Task Announcement_always_exposes_fixed_fifteen_days_from_announcement_date()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context);
        var date = DateTime.UtcNow.Date.AddDays(-3);
        context.Announcements.Add(new Announcement
        {
            SubProjectId = project.SubProjectId,
            AnnouncementDate = date,
            DurationDays = 99,
            DurationSetAt = DateTime.UtcNow.AddYears(-1),
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetStageAsync(project.SubProjectId, ProcurementStage.Announcement);

        Assert.Equal(15, result.DurationDays);
        Assert.Equal(date.AddDays(15), result.Deadline);
    }

    [Theory]
    [InlineData(ProcurementStage.Announcement)]
    [InlineData(ProcurementStage.ContractAward)]
    public async Task Fixed_or_excluded_stage_rejects_general_duration(ProcurementStage stage)
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(context).SetStageDurationAsync(project.SubProjectId, stage, 20));
    }

    [Fact]
    public async Task Ambiguous_legacy_duration_has_no_deadline_and_get_does_not_invent_one()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context);
        context.TenderDocuments.Add(new TenderDocument
        {
            SubProjectId = project.SubProjectId,
            DurationDays = 7,
            DurationSetAt = null,
            CreatedAt = DateTime.UtcNow.AddYears(-2),
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var first = await service.GetStageAsync(project.SubProjectId, ProcurementStage.TenderDocument);
        var second = await service.GetStageAsync(project.SubProjectId, ProcurementStage.TenderDocument);

        Assert.Null(first.Deadline);
        Assert.Null(second.Deadline);
        Assert.False(first.CanFail);
        Assert.Null((await context.TenderDocuments.AsNoTracking().SingleAsync()).DurationSetAt);
    }

    [Fact]
    public async Task Duration_change_is_audited_and_null_resets_to_default_seven_days()
    {
        await using var context = CreateContext();
        var project = await SeedProjectAsync(context);
        var service = CreateService(context);

        await service.SetStageDurationAsync(project.SubProjectId, ProcurementStage.TenderDocument, null);

        var document = await context.TenderDocuments.AsNoTracking().SingleAsync();
        var audit = await context.AuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(7, document.DurationDays);
        Assert.NotNull(document.DurationSetAt);
        Assert.Equal(project.SubProjectId, audit.EntityId);
        Assert.Equal("tender-document", audit.FieldName);
        Assert.Equal("7", audit.NewValue);
        Assert.Equal("duration-test-user", audit.ChangedByUserId);
    }

    [Fact]
    public void Contract_execution_duration_still_calculates_delivery_from_site_handover()
    {
        var award = new ContractAward
        {
            SiteHandoverDate = new DateTime(2026, 8, 10),
            ExecutionDurationMonths = 2,
            ExecutionDurationDays = 5,
        };

        Assert.Equal(new DateTime(2026, 10, 15), award.ContractualDeliveryDate);
    }

    private static async Task<bool> IsAuthorizedAsync(string actionName, string role)
    {
        var action = typeof(ProcurementController).GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public)!;
        var attribute = action.GetCustomAttribute<AuthorizeAttribute>()!;
        var policy = new AuthorizationPolicyBuilder()
            .RequireRole(attribute.Roles!.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "test"), new Claim(ClaimTypes.Role, role)],
            "test"));

        return (await authorization.AuthorizeAsync(principal, null, policy)).Succeeded;
    }

    private static ProcurementService CreateService(AppDbContext context) => new(
        context,
        Mock.Of<IExecutionStageService>(),
        new TestCurrentUser());

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<SubProject> SeedProjectAsync(AppDbContext context)
    {
        var project = new SubProject { SubProjectName = "اختبار مدد الطرح", IsApproved = true };
        context.SubProjects.Add(project);
        await context.SaveChangesAsync();
        return project;
    }

    private static StoredFile File(string name) => new()
    {
        FileName = name,
        FileExtension = ".pdf",
        FileSize = 1,
        Content = [1],
    };

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId => "duration-test-user";
        public string? Role => Roles.SuperAdmin;
    }
}
