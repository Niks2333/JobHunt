namespace JobHunt.Api.Models.Responses.JobApplications;

public record JobApplicationPageResponse(
    IReadOnlyList<JobApplicationResponse> Items,
    int TotalApplications,
    int MatchingApplications,
    int PageNumber,
    int PageSize,
    int TotalPages,
    IReadOnlyList<string> AvailableSources);
