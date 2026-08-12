using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class PresentationMemoConfiguration : IEntityTypeConfiguration<PresentationMemo>
{
    public void Configure(EntityTypeBuilder<PresentationMemo> builder)
    {
        builder.Property(x => x.Title)
               .HasMaxLength(300)
               .IsRequired();

        builder.HasOne(x => x.FinancialYear)
               .WithMany(x => x.PresentationMemos)
               .HasForeignKey(x => x.FinancialYearId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PresentationMemoSubProjectConfiguration : IEntityTypeConfiguration<PresentationMemoSubProject>
{
    public void Configure(EntityTypeBuilder<PresentationMemoSubProject> builder)
    {
        // نفس المشروع الفرعي لا يتكرر في نفس المذكرة
        builder.HasIndex(x => new { x.PresentationMemoId, x.SubProjectId }).IsUnique();

        builder.HasOne(x => x.PresentationMemo)
               .WithMany(m => m.MemoSubProjects)
               .HasForeignKey(x => x.PresentationMemoId)
               .OnDelete(DeleteBehavior.Restrict);

        // بدون خاصية تنقّل عكسية على SubProject — حفاظًا على كيانات إدارة التخطيط بدون تعديل
        builder.HasOne(x => x.SubProject)
               .WithMany()
               .HasForeignKey(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PresentationMemoVersionConfiguration : IEntityTypeConfiguration<PresentationMemoVersion>
{
    public void Configure(EntityTypeBuilder<PresentationMemoVersion> builder)
    {
        builder.HasIndex(x => new { x.PresentationMemoId, x.VersionNumber }).IsUnique();

        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.PresentationMemo)
               .WithMany(m => m.Versions)
               .HasForeignKey(x => x.PresentationMemoId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsStoredFile(x => x.File, "File_", required: true);
        builder.OwnsStoredFile(x => x.LegalAffairsCommitteeDecision, "LegalAffairsDecision_");
    }
}
