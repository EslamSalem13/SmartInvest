using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class PlanProjectConfiguration : IEntityTypeConfiguration<PlanProject>
{
    public void Configure(EntityTypeBuilder<PlanProject> builder)
    {
        // نفس المشروع الفرعي مايتكررش في نفس الخطة
        builder.HasIndex(x => new { x.PlanId, x.SubProjectId }).IsUnique();

        builder.HasOne(x => x.Plan)
               .WithMany(p => p.PlanProjects)
               .HasForeignKey(x => x.PlanId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubProject)
               .WithMany(s => s.PlanProjects)
               .HasForeignKey(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
