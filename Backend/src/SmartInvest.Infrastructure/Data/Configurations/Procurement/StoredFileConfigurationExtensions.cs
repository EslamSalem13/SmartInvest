using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInvest.Domain.Common;
using System.Linq.Expressions;

namespace SmartInvest.Infrastructure.Data.Configurations;

/// <summary>
/// إعداد موحّد للملفات المخزنة داخل قاعدة البيانات (Owned Type)
/// حتى لا تتكرر نفس الأسطر في كل إصدار مستند.
/// </summary>
public static class StoredFileConfigurationExtensions
{
    public static void OwnsStoredFile<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, StoredFile?>> navigation,
        string columnPrefix,
        bool required = false)
        where TEntity : class
    {
        builder.OwnsOne(navigation, file =>
        {
            file.Property(f => f.FileName)
                .HasMaxLength(255)
                .HasColumnName($"{columnPrefix}FileName");

            file.Property(f => f.FileExtension)
                .HasMaxLength(20)
                .HasColumnName($"{columnPrefix}FileExtension");

            file.Property(f => f.FileSize)
                .HasColumnName($"{columnPrefix}FileSize");

            file.Property(f => f.Content)
                .HasColumnType("varbinary(max)")
                .HasColumnName($"{columnPrefix}Content");
        });

        if (required)
        {
            builder.Navigation(navigation!).IsRequired();
        }
    }
}
