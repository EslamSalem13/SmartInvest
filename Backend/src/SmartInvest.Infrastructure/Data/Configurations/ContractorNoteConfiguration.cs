using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class ContractorNoteConfiguration : IEntityTypeConfiguration<ContractorNote>
{
    public void Configure(EntityTypeBuilder<ContractorNote> builder)
    {
        builder.HasKey(x => x.ContractorNoteId);
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();

        builder.HasOne(x => x.Contractor)
               .WithMany(c => c.Notes)
               .HasForeignKey(x => x.ContractorId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SubProject)
               .WithMany()
               .HasForeignKey(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
