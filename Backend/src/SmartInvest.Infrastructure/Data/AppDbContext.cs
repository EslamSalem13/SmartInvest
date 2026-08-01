using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Infrastructure.Identity;

namespace SmartInvest.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MainProject> MainProjects => Set<MainProject>();
    public DbSet<SubProject> SubProjects => Set<SubProject>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    //MARWA
    public DbSet<Plan> Plans { get; set; }
    public DbSet<PlanProject> PlanProjects { get; set; }
    public DbSet<MainProgram> MainPrograms { get; set; }
    public DbSet<SubProgram> SubPrograms { get; set; }
    public DbSet<FinancialYear> FinancialYears { get; set; }
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<ComponentType> ComponentTypes => Set<ComponentType>();
    public DbSet<ProjectLevel> ProjectLevels => Set<ProjectLevel>();
    public DbSet<AccountingUnit> AccountingUnits => Set<AccountingUnit>();

    // end

    // ===== Financial Management (دورة التعاقدات) =====
    public DbSet<PresentationMemo> PresentationMemos => Set<PresentationMemo>();
    public DbSet<PresentationMemoSubProject> PresentationMemoSubProjects => Set<PresentationMemoSubProject>();
    public DbSet<PresentationMemoVersion> PresentationMemoVersions => Set<PresentationMemoVersion>();
    public DbSet<TenderDocument> TenderDocuments => Set<TenderDocument>();
    public DbSet<TenderDocumentVersion> TenderDocumentVersions => Set<TenderDocumentVersion>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementVersion> AnnouncementVersions => Set<AnnouncementVersion>();
    public DbSet<OpeningEnvelopes> OpeningEnvelopes => Set<OpeningEnvelopes>();
    public DbSet<OpeningEnvelopesVersion> OpeningEnvelopesVersions => Set<OpeningEnvelopesVersion>();
    public DbSet<TechnicalEvaluation> TechnicalEvaluations => Set<TechnicalEvaluation>();
    public DbSet<TechnicalEvaluationVersion> TechnicalEvaluationVersions => Set<TechnicalEvaluationVersion>();
    public DbSet<FinancialEvaluation> FinancialEvaluations => Set<FinancialEvaluation>();
    public DbSet<FinancialEvaluationVersion> FinancialEvaluationVersions => Set<FinancialEvaluationVersion>();
    public DbSet<ContractAward> ContractAwards => Set<ContractAward>();
    public DbSet<ContractAwardVersion> ContractAwardVersions => Set<ContractAwardVersion>();
    // ===== end Financial Management =====


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Apply all IEntityTypeConfiguration<T> defined in this assembly.
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
