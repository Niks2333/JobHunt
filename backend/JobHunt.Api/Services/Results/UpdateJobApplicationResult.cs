using JobHunt.Api.Models.Responses.JobApplications;

namespace JobHunt.Api.Services.Results;

public record UpdateJobApplicationResult(
    JobApplicationResponse? Application,
    Dictionary<string, string[]> ValidationErrors);
