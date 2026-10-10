using JobHunt.Api.Data;
using JobHunt.Api.DataAccess.Results;
using JobHunt.Api.Entities;
using JobHunt.Api.Models.Requests.Analytics;
using Microsoft.EntityFrameworkCore;

namespace JobHunt.Api.DataAccess;

public class ApplicationAnalyticsDataAccess(JobHuntDbContext databaseContext)
{
    public async Task<ApplicationAnalyticsData> GetApplicationAnalyticsAsync(
        GetApplicationAnalyticsRequest request, DateOnly currentMonthStartDate,
        CancellationToken cancellationToken)
    {
        var applications = databaseContext.JobApplications.AsNoTracking();
        if (request.StartDate is DateOnly startDate)
            applications = applications.Where(application => application.ApplicationDate >= startDate);
        if (request.EndDate is DateOnly endDate)
            applications = applications.Where(application => application.ApplicationDate <= endDate);

        var nextMonthStartDate = currentMonthStartDate.AddMonths(1);
        var totalApplications = await applications.CountAsync(cancellationToken);
        var applicationsThisMonth = await applications.CountAsync(application =>
            application.ApplicationDate >= currentMonthStartDate &&
            application.ApplicationDate < nextMonthStartDate, cancellationToken);
        // Any becomes SQL EXISTS: repeated interview entries still count one application.
        var applicationsReachingInterview = await applications.CountAsync(application =>
            application.StatusHistory.Any(history => history.Status == JobApplicationStatus.Interview),
            cancellationToken);
        var statusCounts = await applications.GroupBy(application => application.Status)
            .Select(group => new ApplicationStatusCountData(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var sourceCounts = await applications
            .GroupBy(application => application.Source == null || application.Source.Trim() == ""
                ? null : application.Source.Trim())
            .OrderByDescending(group => group.Count()).ThenBy(group => group.Key)
            .Select(group => new ApplicationSourceCountData(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        return new(totalApplications, applicationsThisMonth, applicationsReachingInterview,
            statusCounts, sourceCounts);
    }
}
