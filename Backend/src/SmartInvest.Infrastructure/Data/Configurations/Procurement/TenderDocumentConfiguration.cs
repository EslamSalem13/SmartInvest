using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class TenderDocumentConfiguration : IEntityTypeConfiguration<TenderDocument>
{
    public void Configure(EntityTypeBuilder<TenderDocument> builder)
    {
        // 1:1 — كراسة شروط واحدة لكل مشروع فرعي
        builder.HasIndex(x => x.SubProjectId).IsUnique();

        builder.HasOne(x => x.SubProject)
               .WithOne()
               .HasForeignKey<TenderDocument>(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TenderDocumentVersionConfiguration : IEntityTypeConfiguration<TenderDocumentVersion>
{
    public void Configure(EntityTypeBuilder<TenderDocumentVersion> builder)
    {
        builder.HasIndex(x => new { x.TenderDocumentId, x.VersionNumber }).IsUnique();

        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.TenderDocument)
               .WithMany(d => d.Versions)
               .HasForeignKey(x => x.TenderDocumentId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsStoredFile(x => x.File, "File_", required: true);
    }
}
