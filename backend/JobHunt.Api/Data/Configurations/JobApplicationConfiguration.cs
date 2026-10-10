using JobHunt.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunt.Api.Data.Configurations;

public class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> entityBuilder)
    {
        entityBuilder.HasKey(jobApplication => jobApplication.JobApplicationId);
        entityBuilder.Property(jobApplication => jobApplication.CompanyName).HasMaxLength(150).IsRequired();
        entityBuilder.Property(jobApplication => jobApplication.RoleTitle).HasMaxLength(150).IsRequired();
        entityBuilder.Property(jobApplication => jobApplication.Location).HasMaxLength(150);
        entityBuilder.Property(jobApplication => jobApplication.Source).HasMaxLength(100);
        entityBuilder.Property(jobApplication => jobApplication.JobUrl).HasMaxLength(2048);
        entityBuilder.Property(jobApplication => jobApplication.Status).HasConversion<string>().HasMaxLength(30);
        entityBuilder.HasIndex(jobApplication => jobApplication.ApplicationDate);
        entityBuilder.HasMany(jobApplication => jobApplication.StatusHistory)
            .WithOne(historyEntry => historyEntry.JobApplication)
            .HasForeignKey(historyEntry => historyEntry.JobApplicationId);
    }
}
