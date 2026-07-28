using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class SubProjectConfiguration : IEntityTypeConfiguration<SubProject>
{
    public void Configure(EntityTypeBuilder<SubProject> builder)
    {
        // كود المشروع الفرعي: مسموح تكراره — فهرس عادي (غير فريد) للبحث فقط
        builder.HasIndex(x => x.SubProjectCode);

        // اسم المشروع الفرعي: فريد على مستوى قاعدة البيانات
        builder.HasIndex(x => x.SubProjectName)
               .IsUnique();

        // منع الـ cascade delete اللي ممكن يمسح بيانات التخطيط بالغلط (مطلب في الـ Master Prompt)
        builder.HasOne(x => x.MainProject)
               .WithMany(m => m.SubProjects)
               .HasForeignKey(x => x.MainProjectId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Status)
       .WithMany(s => s.SubProjects)
       .HasForeignKey(x => x.StatusId)
       .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Priority)
               .WithMany(p => p.SubProjects)
               .HasForeignKey(x => x.PriorityId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Markaz)
               .WithMany(m => m.SubProjects)
               .HasForeignKey(x => x.MarkazId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ExecutiveAgency)
               .WithMany()
               .HasForeignKey(x => x.ExecutiveAgencyId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}