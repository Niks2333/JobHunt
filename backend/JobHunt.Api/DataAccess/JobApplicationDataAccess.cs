using JobHunt.Api.Data;
using JobHunt.Api.Entities;
using Microsoft.EntityFrameworkCore;
using JobHunt.Api.Models.Requests.JobApplications;
using JobHunt.Api.DataAccess.Results;

namespace JobHunt.Api.DataAccess;

public class JobApplicationDataAccess(JobHuntDbContext databaseContext)
{
    public async Task<JobApplicationPageData> GetJobApplicationsAsync(
        GetJobApplicationsRequest request, CancellationToken cancellationToken)
    {
        var allApplications = databaseContext.JobApplications.AsNoTracking();
        var filteredApplications = allApplications;
        var search = request.Search?.Trim();
        var source = request.Source?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            filteredApplications = filteredApplications.Where(jobApplication =>
                jobApplication.CompanyName.Contains(search) || jobApplication.RoleTitle.Contains(search));
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = Enum.Parse<JobApplicationStatus>(request.Status.Trim());
            filteredApplications = filteredApplications.Where(jobApplication => jobApplication.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(source))
            filteredApplications = filteredApplications.Where(jobApplication => jobApplication.Source == source);

        // Counts are taken before pagination. Filters and Skip/Take execute in SQL.
        var totalApplications = await allApplications.CountAsync(cancellationToken);
        var matchingApplications = await filteredApplications.CountAsync(cancellationToken);
        var items = await filteredApplications
            .OrderByDescending(jobApplication => jobApplication.ApplicationDate)
            .ThenByDescending(jobApplication => jobApplication.JobApplicationId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        // Use all applications so selecting a filter does not hide other source choices.
        var availableSources = await allApplications
            .Where(jobApplication => jobApplication.Source != null && jobApplication.Source != "")
            .Select(jobApplication => jobApplication.Source!)
            .Distinct().OrderBy(sourceName => sourceName).ToListAsync(cancellationToken);
        return new(items, totalApplications, matchingApplications, availableSources);
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

    public Task<JobApplication?> GetJobApplicationForUpdateAsync(
        int jobApplicationId, CancellationToken cancellationToken)
    {
        // Tracking lets EF Core detect changes to both the application and its history.
        return databaseContext.JobApplications
            .Include(jobApplication => jobApplication.StatusHistory)
            .SingleOrDefaultAsync(jobApplication => jobApplication.JobApplicationId == jobApplicationId,
                cancellationToken);
    }

    public Task SaveJobApplicationChangesAsync(CancellationToken cancellationToken) =>
        databaseContext.SaveChangesAsync(cancellationToken);

    public Task<bool> HasInterviewBeforeApplicationDateAsync(int jobApplicationId,
        DateOnly applicationDate, CancellationToken cancellationToken)
    {
        var applicationDayStartUtc = applicationDate.ToDateTime(TimeOnly.MinValue).AddHours(-5.5);
        return databaseContext.JobApplicationInterviews.AnyAsync(interview =>
            interview.JobApplicationId == jobApplicationId && interview.ScheduledAtUtc < applicationDayStartUtc,
            cancellationToken);
    }

    public async Task CreateJobApplicationAsync(JobApplication jobApplication, CancellationToken cancellationToken)
    {
        databaseContext.JobApplications.Add(jobApplication);
        // EF Core saves the application and its related history in one transaction.
        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
