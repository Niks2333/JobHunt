using JobHunt.Api.Models.Responses.Interviews;

namespace JobHunt.Api.Services.Results;

public record SaveJobApplicationInterviewResult(JobApplicationInterviewResponse? Interview,
    Dictionary<string, string[]> ValidationErrors);
