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

            builder.Property(x => x.IsApproved)
           .HasDefaultValue(true);
        }
    }
}
