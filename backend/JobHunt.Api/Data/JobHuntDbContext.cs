using JobHunt.Api.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using JobHunt.Api.Services;

namespace JobHunt.Api.Data;

public class JobHuntDbContext(DbContextOptions<JobHuntDbContext> databaseOptions, CurrentUser currentUser)
    : IdentityDbContext<ApplicationUser>(databaseOptions)
{
    private string? CurrentUserId => currentUser.UserId;
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<JobApplicationStatusHistory> JobApplicationStatusHistory => Set<JobApplicationStatusHistory>();
    public DbSet<JobApplicationInterview> JobApplicationInterviews => Set<JobApplicationInterview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Identity's account tables before applying our tracker configurations.
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobHuntDbContext).Assembly);
        // Null owners preserve legacy records without exposing them to any account.
        modelBuilder.Entity<JobApplication>().HasQueryFilter(application =>
            CurrentUserId != null && application.UserId == CurrentUserId);
        modelBuilder.Entity<JobApplicationInterview>().HasQueryFilter(interview =>
            CurrentUserId != null && interview.JobApplication.UserId == CurrentUserId);
        modelBuilder.Entity<JobApplicationStatusHistory>().HasQueryFilter(history =>
            CurrentUserId != null && history.JobApplication.UserId == CurrentUserId);
    }
}
