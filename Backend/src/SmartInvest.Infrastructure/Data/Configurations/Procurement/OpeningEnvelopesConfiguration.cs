using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class OpeningEnvelopesConfiguration : IEntityTypeConfiguration<OpeningEnvelopes>
{
    public void Configure(EntityTypeBuilder<OpeningEnvelopes> builder)
    {
        builder.HasIndex(x => x.SubProjectId).IsUnique();

        builder.HasOne(x => x.SubProject)
               .WithOne()
               .HasForeignKey<OpeningEnvelopes>(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OpeningEnvelopesVersionConfiguration : IEntityTypeConfiguration<OpeningEnvelopesVersion>
{
    public void Configure(EntityTypeBuilder<OpeningEnvelopesVersion> builder)
    {
        builder.HasIndex(x => new { x.OpeningEnvelopesId, x.VersionNumber }).IsUnique();

        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.OpeningEnvelopes)
               .WithMany(d => d.Versions)
               .HasForeignKey(x => x.OpeningEnvelopesId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsStoredFile(x => x.File, "File_", required: true);
    }
}
