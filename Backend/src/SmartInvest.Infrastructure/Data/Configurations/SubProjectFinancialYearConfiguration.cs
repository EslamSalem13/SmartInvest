using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Data.Configurations;

public class SubProjectFinancialYearConfiguration : IEntityTypeConfiguration<SubProjectFinancialYear>
{
    public void Configure(EntityTypeBuilder<SubProjectFinancialYear> builder)
    {
        // نفس المشروع الفرعي مايتربطش بنفس السنة المالية مرتين
        builder.HasIndex(x => new { x.SubProjectId, x.FinancialYearId }).IsUnique();

        builder.HasOne(x => x.SubProject)
               .WithMany(s => s.FinancialYears)
               .HasForeignKey(x => x.SubProjectId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FinancialYear)
               .WithMany(f => f.SubProjectFinancialYears)
               .HasForeignKey(x => x.FinancialYearId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
