using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class PlanApprovalNotificationConfiguration : IEntityTypeConfiguration<PlanApprovalNotification>
{
    public void Configure(EntityTypeBuilder<PlanApprovalNotification> builder)
    {
        builder.HasIndex(x => new { x.PlanId, x.EventType }).IsUnique();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Recipients)
            .WithOne(x => x.Notification)
            .HasForeignKey(x => x.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlanApprovalNotificationRecipientConfiguration : IEntityTypeConfiguration<PlanApprovalNotificationRecipient>
{
    public void Configure(EntityTypeBuilder<PlanApprovalNotificationRecipient> builder)
    {
        builder.HasIndex(x => new { x.NotificationId, x.NormalizedEmail }).IsUnique();
    }
}
