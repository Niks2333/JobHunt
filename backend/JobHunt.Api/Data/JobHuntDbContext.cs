using JobHunt.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobHunt.Api.Data;

public class JobHuntDbContext(DbContextOptions<JobHuntDbContext> databaseOptions)
    : DbContext(databaseOptions)
{
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<JobApplicationStatusHistory> JobApplicationStatusHistory => Set<JobApplicationStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobHuntDbContext).Assembly);
    }
}
