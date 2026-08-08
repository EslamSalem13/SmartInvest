using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartInvest.Application.Common.Ai;
using SmartInvest.Application.Interfaces;
using SmartInvest.Application.Services;
using SmartInvest.Application.Services.Import;
using SmartInvest.Domain.Interfaces;
using SmartInvest.Infrastructure.Data;
using SmartInvest.Infrastructure.Identity;
using SmartInvest.Infrastructure.Repositories;
using SmartInvest.Infrastructure.Services;

namespace SmartInvest.Infrastructure;

/// <summary>
/// Registers Infrastructure-layer services (EF Core, Identity, repositories) into the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.Configure<AiGatewayOptions>(configuration.GetSection(AiGatewayOptions.SectionName));
        services.AddHttpClient<IAiGatewayClient, AiGatewayClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        }).RedactLoggedHeaders(["Authorization"]);

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddMemoryCache();
        services.AddSingleton<ImportSessionStore>();
        services.AddScoped<IExcelImportParser, ExcelImportParser>();
        services.AddScoped<IMeasurementExtractionService, MeasurementExtractionService>();
        services.AddScoped<IProjectNatureClassificationService, ProjectNatureClassificationService>();
        services.AddScoped<ILookupMatchSuggestionService, LookupMatchSuggestionService>();
        services.AddScoped<SmartInvest.Application.Services.Import.SuggestedPlanImportService>();
        services.AddScoped<SmartInvest.Application.Services.Import.ApprovedPlanImportService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IMainProjectRepository, MainProjectRepository>();
        services.AddScoped<ISubProjectRepository, SubProjectRepository>();
        services.AddScoped<IProjectAssignmentRepository, ProjectAssignmentRepository>();
        services.AddScoped<IMainProjectService, MainProjectService>();
        services.AddScoped<ISubProjectService, SubProjectService>();
        services.AddScoped<IProjectSpecificationService, ProjectSpecificationService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IFinancialYearService, FinancialYearService>();
        services.AddScoped<ISubProjectFinancialYearService, SubProjectFinancialYearService>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IExecutiveAgencyService, ExecutiveAgencyService>();
        services.AddScoped<IContractorService, ContractorService>();
        services.AddScoped<IExecutionStageService, ExecutionStageService>();
        services.AddScoped<IContractTypeService, ContractTypeService>();
        services.AddScoped<IProjectAssignmentService, ProjectAssignmentService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IMeasurementRepository, MeasurementRepository>();
        services.AddScoped<IMeasurementService, MeasurementService>();
        services.AddScoped<IMeasurementResolutionService, MeasurementResolutionService>();

        // Financial Management (دورة التعاقدات)
        services.AddScoped<IProcurementService, Services.ProcurementService>();
        services.AddScoped<IPresentationMemoService, Services.PresentationMemoService>();

        return services;
    }
}
