using JobHunt.Api.Entities;

namespace JobHunt.Api.DataAccess.Results;

public record ApplicationAnalyticsData(
    int TotalApplications,
    int ApplicationsThisMonth,
    int ApplicationsReachingInterview,
    IReadOnlyList<ApplicationStatusCountData> StatusCounts,
    IReadOnlyList<ApplicationSourceCountData> SourceCounts);

public record ApplicationStatusCountData(JobApplicationStatus Status, int Count);
public record ApplicationSourceCountData(string? Source, int Count);
