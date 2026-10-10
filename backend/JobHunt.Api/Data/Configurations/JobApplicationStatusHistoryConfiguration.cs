using JobHunt.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunt.Api.Data.Configurations;

public class JobApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<JobApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<JobApplicationStatusHistory> entityBuilder)
    {
        entityBuilder.HasKey(historyEntry => historyEntry.JobApplicationStatusHistoryId);
        entityBuilder.Property(historyEntry => historyEntry.Status).HasConversion<string>().HasMaxLength(30);
    }
}
