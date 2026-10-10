using JobHunt.Api.DataAccess;
using JobHunt.Api.Entities;
using JobHunt.Api.Models.Requests.Analytics;
using JobHunt.Api.Models.Responses.Analytics;

namespace JobHunt.Api.Services;

public class ApplicationAnalyticsService(ApplicationAnalyticsDataAccess applicationAnalyticsDataAccess)
{
    public async Task<ApplicationAnalyticsResponse> GetApplicationAnalyticsAsync(
        GetApplicationAnalyticsRequest request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var currentMonthStartDate = new DateOnly(today.Year, today.Month, 1);
        var analytics = await applicationAnalyticsDataAccess.GetApplicationAnalyticsAsync(
            request, currentMonthStartDate, cancellationToken);
        var conversionPercentage = analytics.TotalApplications == 0 ? 0m :
            Math.Round(100m * analytics.ApplicationsReachingInterview / analytics.TotalApplications, 1);
        var statuses = Enum.GetValues<JobApplicationStatus>().Select(status =>
            new ApplicationBreakdownResponse(status.ToString(),
                analytics.StatusCounts.SingleOrDefault(group => group.Status == status)?.Count ?? 0)).ToList();
        var sources = analytics.SourceCounts.Select(group =>
            new ApplicationBreakdownResponse(group.Source ?? "Unspecified", group.Count)).ToList();
        return new(request.StartDate, request.EndDate, currentMonthStartDate,
            analytics.TotalApplications, analytics.ApplicationsThisMonth,
            analytics.ApplicationsReachingInterview, conversionPercentage, statuses, sources);
    }
}
