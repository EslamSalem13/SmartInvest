using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class TechnicalEvaluationConfiguration : IEntityTypeConfiguration<TechnicalEvaluation>
{
    public void Configure(EntityTypeBuilder<TechnicalEvaluation> builder)
    {
        builder.HasIndex(x => x.SubProjectId).IsUnique();

        builder.HasOne(x => x.SubProject)
               .WithOne()
               .HasForeignKey<TechnicalEvaluation>(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TechnicalEvaluationVersionConfiguration : IEntityTypeConfiguration<TechnicalEvaluationVersion>
{
    public void Configure(EntityTypeBuilder<TechnicalEvaluationVersion> builder)
    {
        builder.HasIndex(x => new { x.TechnicalEvaluationId, x.VersionNumber }).IsUnique();

        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.TechnicalEvaluation)
               .WithMany(d => d.Versions)
               .HasForeignKey(x => x.TechnicalEvaluationId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsStoredFile(x => x.FirstCommitteeReport, "FirstCommitteeReport_");
        builder.OwnsStoredFile(x => x.SecondCommitteeReport, "SecondCommitteeReport_");
        builder.OwnsStoredFile(x => x.FinalTechnicalEvaluationReport, "FinalTechnicalEvaluationReport_");
    }
}
