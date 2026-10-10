namespace JobHunt.Api.Models.Responses.Analytics;

public record ApplicationAnalyticsResponse(
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateOnly CurrentMonthStartDate,
    int TotalApplications,
    int ApplicationsThisMonth,
    int ApplicationsReachingInterview,
    decimal InterviewConversionPercentage,
    IReadOnlyList<ApplicationBreakdownResponse> ApplicationsByStatus,
    IReadOnlyList<ApplicationBreakdownResponse> ApplicationsBySource);

public record ApplicationBreakdownResponse(string Label, int Count);
