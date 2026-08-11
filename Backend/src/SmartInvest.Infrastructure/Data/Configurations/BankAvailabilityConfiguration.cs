using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class BankAvailabilityConfiguration : IEntityTypeConfiguration<BankAvailability>
{
    public void Configure(EntityTypeBuilder<BankAvailability> builder)
    {
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450);

        builder.HasOne(x => x.FinancialYear)
               .WithMany(y => y.BankAvailabilities)
               .HasForeignKey(x => x.FinancialYearId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BankAvailabilityDocumentConfiguration : IEntityTypeConfiguration<BankAvailabilityDocument>
{
    public void Configure(EntityTypeBuilder<BankAvailabilityDocument> builder)
    {
        builder.HasOne(x => x.BankAvailability)
               .WithMany(a => a.Documents)
               .HasForeignKey(x => x.BankAvailabilityId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsStoredFile(x => x.File, "File_", required: true);
    }
}
