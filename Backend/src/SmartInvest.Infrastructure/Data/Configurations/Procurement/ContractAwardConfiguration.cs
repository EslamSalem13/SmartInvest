using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class ContractAwardConfiguration : IEntityTypeConfiguration<ContractAward>
{
    public void Configure(EntityTypeBuilder<ContractAward> builder)
    {
        builder.HasIndex(x => x.SubProjectId).IsUnique();

        builder.HasOne(x => x.SubProject)
               .WithOne()
               .HasForeignKey<ContractAward>(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ContractAwardVersionConfiguration : IEntityTypeConfiguration<ContractAwardVersion>
{
    public void Configure(EntityTypeBuilder<ContractAwardVersion> builder)
    {
        builder.HasIndex(x => new { x.ContractAwardId, x.VersionNumber }).IsUnique();

        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.ContractAward)
               .WithMany(d => d.Versions)
               .HasForeignKey(x => x.ContractAwardId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsStoredFile(x => x.AwardOrder, "AwardOrder_");
        builder.OwnsStoredFile(x => x.Contract, "Contract_");
    }
}
