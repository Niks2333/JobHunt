using JobHunt.Api.Data;
using JobHunt.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobHunt.Api.DataAccess;

public class JobApplicationInterviewDataAccess(JobHuntDbContext databaseContext)
{
    public Task<JobApplication?> GetApplicationAsync(int jobApplicationId, CancellationToken cancellationToken) =>
        databaseContext.JobApplications.AsNoTracking().SingleOrDefaultAsync(
            application => application.JobApplicationId == jobApplicationId, cancellationToken);

    public Task<List<JobApplicationInterview>> GetInterviewsAsync(int jobApplicationId,
        CancellationToken cancellationToken) => databaseContext.JobApplicationInterviews.AsNoTracking()
        .Where(interview => interview.JobApplicationId == jobApplicationId)
        .OrderBy(interview => interview.ScheduledAtUtc).ThenBy(interview => interview.JobApplicationInterviewId)
        .ToListAsync(cancellationToken);

    public Task<JobApplicationInterview?> GetInterviewForUpdateAsync(int jobApplicationId,
        int interviewId, CancellationToken cancellationToken) => databaseContext.JobApplicationInterviews
        .SingleOrDefaultAsync(interview => interview.JobApplicationId == jobApplicationId &&
            interview.JobApplicationInterviewId == interviewId, cancellationToken);

    public async Task<(List<JobApplicationInterview> Items, int Total)> GetUpcomingInterviewsAsync(
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var upcomingInterviews = databaseContext.JobApplicationInterviews.AsNoTracking()
            .Where(interview => !interview.IsCancelled && interview.ScheduledAtUtc >= nowUtc);
        var total = await upcomingInterviews.CountAsync(cancellationToken);
        var items = await upcomingInterviews.Include(interview => interview.JobApplication)
            .OrderBy(interview => interview.ScheduledAtUtc).ThenBy(interview => interview.JobApplicationInterviewId)
            .Take(10).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task SaveInterviewAsync(JobApplicationInterview interview, bool isNew,
        CancellationToken cancellationToken)
    {
        if (isNew) databaseContext.JobApplicationInterviews.Add(interview);
        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
