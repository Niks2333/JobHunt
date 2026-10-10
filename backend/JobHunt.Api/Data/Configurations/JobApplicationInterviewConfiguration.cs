using JobHunt.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunt.Api.Data.Configurations;

public class JobApplicationInterviewConfiguration : IEntityTypeConfiguration<JobApplicationInterview>
{
    public void Configure(EntityTypeBuilder<JobApplicationInterview> entityBuilder)
    {
        entityBuilder.HasKey(interview => interview.JobApplicationInterviewId);
        entityBuilder.Property(interview => interview.RoundName).HasMaxLength(150).IsRequired();
        entityBuilder.Property(interview => interview.Mode).HasConversion<string>().HasMaxLength(30);
        entityBuilder.Property(interview => interview.MeetingUrl).HasMaxLength(2048);
        entityBuilder.Property(interview => interview.Location).HasMaxLength(200);
        entityBuilder.Property(interview => interview.PreparationNotes).HasMaxLength(5000);
        entityBuilder.Property(interview => interview.Feedback).HasMaxLength(5000);
        entityBuilder.HasOne(interview => interview.JobApplication)
            .WithMany(application => application.Interviews)
            .HasForeignKey(interview => interview.JobApplicationId).OnDelete(DeleteBehavior.Cascade);
        entityBuilder.HasIndex(interview => new { interview.IsCancelled, interview.ScheduledAtUtc });
    }
}
