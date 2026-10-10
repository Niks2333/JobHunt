using JobHunt.Api.Models.Responses.JobApplications;

namespace JobHunt.Api.Services.Results;

public record ChangeJobApplicationStatusResult(
    JobApplicationResponse? Application,
    Dictionary<string, string[]> ValidationErrors);
