namespace JobHunt.Api.Models.Responses.JobApplications;

public record JobApplicationDetailsResponse(
    JobApplicationResponse Application,
    IReadOnlyList<JobApplicationStatusHistoryResponse> StatusHistory);
