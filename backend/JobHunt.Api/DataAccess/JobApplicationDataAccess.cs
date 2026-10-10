using JobHunt.Api.Data;
using JobHunt.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobHunt.Api.DataAccess;

public class JobApplicationDataAccess(JobHuntDbContext databaseContext)
{
    public Task<List<JobApplication>> GetJobApplicationsAsync(CancellationToken cancellationToken)
    {
        // These LINQ operations become ORDER BY clauses in SQL, not in-memory sorting.
        return databaseContext.JobApplications.AsNoTracking()
            .OrderByDescending(jobApplication => jobApplication.ApplicationDate)
            .ThenByDescending(jobApplication => jobApplication.JobApplicationId)
            .ToListAsync(cancellationToken);
    }

    public Task<JobApplication?> GetJobApplicationWithStatusHistoryAsync(
        int jobApplicationId, CancellationToken cancellationToken)
    {
        return databaseContext.JobApplications.AsNoTracking()
            .Include(jobApplication => jobApplication.StatusHistory
                .OrderBy(historyEntry => historyEntry.OccurredOn)
                .ThenBy(historyEntry => historyEntry.JobApplicationStatusHistoryId))
            .SingleOrDefaultAsync(jobApplication => jobApplication.JobApplicationId == jobApplicationId,
                cancellationToken);
    }

    public async Task CreateJobApplicationAsync(JobApplication jobApplication, CancellationToken cancellationToken)
    {
        databaseContext.JobApplications.Add(jobApplication);
        // EF Core saves the application and its related history in one transaction.
        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
