using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class ExecutionStageConfiguration : IEntityTypeConfiguration<ExecutionStage>
{
    public void Configure(EntityTypeBuilder<ExecutionStage> builder)
    {
        builder.HasKey(x => x.ExecutionStageId);

        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.SelfFundingSpent).HasColumnType("decimal(18,2)");
        builder.Property(x => x.BankFundingSpent).HasColumnType("decimal(18,2)");
        builder.Property(x => x.PhysicalProgressPercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.PenaltyAmount).HasColumnType("decimal(18,2)");

        builder.HasOne(x => x.SubProject)
               .WithMany()
               .HasForeignKey(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsStoredFile(x => x.SelfFundingProofFile, "SelfFundingProof_");
        builder.OwnsStoredFile(x => x.BankFundingProofFile, "BankFundingProof_");
        builder.OwnsStoredFile(x => x.PhysicalProgressProofFile, "PhysicalProgressProof_");
    }
}
