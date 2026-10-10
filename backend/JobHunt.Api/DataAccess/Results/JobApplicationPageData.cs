using JobHunt.Api.Entities;

namespace JobHunt.Api.DataAccess.Results;

public record JobApplicationPageData(
    List<JobApplication> Items,
    int TotalApplications,
    int MatchingApplications,
    List<string> AvailableSources);
