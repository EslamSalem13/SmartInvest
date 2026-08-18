using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartInvest.Infrastructure.Data.Configurations
{
    public class MainProjectConfiguration : IEntityTypeConfiguration<MainProject>
    {
        public void Configure(EntityTypeBuilder<MainProject> builder)
        {
            builder.HasIndex(x => x.MainProjectCode);

            // لا تُضِف HasDefaultValue(true) هنا: أي إنشاء يمرّر IsApproved = false صراحةً
            // (القيمة الافتراضية لنوع bool في C#) يُعامله EF Core على أنه "لم يُحدَّد من التطبيق"
            // ويترك عمود الإدراج فارغًا ليطبّق القاعدة الـ DEFAULT بدلًا منه - فيتحول كل مشروع رئيسي
            // جديد غير مُعتمد (من استيراد خطة مقترحة أو إنشاء يدوي بلا كود) إلى "معتمد" في القاعدة
            // رغم أن الكود صراحةً يضبطه false. راجع Migrations للتفاصيل.
        }
    }
}
