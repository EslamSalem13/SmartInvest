using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.HasIndex(x => x.SubProjectId).IsUnique();

        builder.HasOne(x => x.SubProject)
               .WithOne()
               .HasForeignKey<Announcement>(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AnnouncementVersionConfiguration : IEntityTypeConfiguration<AnnouncementVersion>
{
    public void Configure(EntityTypeBuilder<AnnouncementVersion> builder)
    {
        builder.HasIndex(x => new { x.AnnouncementId, x.VersionNumber }).IsUnique();

        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.Announcement)
               .WithMany(d => d.Versions)
               .HasForeignKey(x => x.AnnouncementId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsStoredFile(x => x.NewspaperAdvertisement, "NewspaperAdvertisement_");
        builder.OwnsStoredFile(x => x.PortalAdvertisement, "PortalAdvertisement_");
        builder.OwnsStoredFile(x => x.CompetentAuthorityApproval, "CompetentAuthorityApproval_");
    }
}
