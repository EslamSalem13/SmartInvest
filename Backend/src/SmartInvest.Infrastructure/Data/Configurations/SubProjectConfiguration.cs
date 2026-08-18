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

        // اسم المشروع الفرعي: لم يعُد فريدًا حتى ضمن نفس المشروع الرئيسي — نفس الاسم قد يتكرر شرعًا
        // تحت نفس المشروع الرئيسي عبر سنوات مالية مختلفة (كل سنة مالية تُنشئ نسخة/سجل مستقل من
        // المشروع الفرعي بمعرّف SubProjectId خاص بها كي تبقى مستنداته - كمذكرة العرض وغيرها - معزولة
        // عن السنوات الأخرى؛ راجع SuggestedPlanImportService.CommitAsync). الفهرس هنا للبحث فقط،
        // والتحقق من عدم التكرار الفعلي (لإنشاء يدوي داخل نفس السياق) يتم في SubProjectService عبر
        // NameExistsAsync على مستوى التطبيق.
        builder.HasIndex(x => new { x.MainProjectId, x.SubProjectName });

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

        builder.HasOne(x => x.ProjectLevel)
               .WithMany()
               .HasForeignKey(x => x.ProjectLevelId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ComponentType)
               .WithMany()
               .HasForeignKey(x => x.ComponentTypeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AccountingUnit)
               .WithMany()
               .HasForeignKey(x => x.AccountingUnitId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}