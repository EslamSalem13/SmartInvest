using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class FinancialEvaluationConfiguration : IEntityTypeConfiguration<FinancialEvaluation>
{
    public void Configure(EntityTypeBuilder<FinancialEvaluation> builder)
    {
        builder.HasIndex(x => x.SubProjectId).IsUnique();

        builder.HasOne(x => x.SubProject)
               .WithOne()
               .HasForeignKey<FinancialEvaluation>(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FinancialEvaluationVersionConfiguration : IEntityTypeConfiguration<FinancialEvaluationVersion>
{
    public void Configure(EntityTypeBuilder<FinancialEvaluationVersion> builder)
    {
        builder.HasIndex(x => new { x.FinancialEvaluationId, x.VersionNumber }).IsUnique();

        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.FinancialEvaluation)
               .WithMany(d => d.Versions)
               .HasForeignKey(x => x.FinancialEvaluationId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsStoredFile(x => x.FinancialBidOpeningMinutes, "FinancialBidOpeningMinutes_");
        builder.OwnsStoredFile(x => x.FinancialEvaluationReport, "FinancialEvaluationReport_");
        builder.OwnsStoredFile(x => x.EstimatedCostSheet, "EstimatedCostSheet_");
    }
}
