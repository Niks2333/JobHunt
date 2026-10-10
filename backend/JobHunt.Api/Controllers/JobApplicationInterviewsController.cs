using JobHunt.Api.Models.Requests.Interviews;
using JobHunt.Api.Models.Responses.Interviews;
using JobHunt.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobHunt.Api.Controllers;

[ApiController]
[Route("api/applications/{jobApplicationId:int}/interviews")]
public class JobApplicationInterviewsController(JobApplicationInterviewService interviewService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobApplicationInterviewResponse>>> GetInterviews(
        int jobApplicationId, CancellationToken cancellationToken)
    {
        var interviews = await interviewService.GetInterviewsAsync(jobApplicationId, cancellationToken);
        return interviews is null ? NotFound() : Ok(interviews);
    }

    [HttpGet("/api/interviews/upcoming")]
    public Task<UpcomingInterviewsResponse> GetUpcomingInterviews(CancellationToken cancellationToken) =>
        interviewService.GetUpcomingInterviewsAsync(cancellationToken);

    [HttpPost]
    public Task<ActionResult<JobApplicationInterviewResponse>> CreateInterview(int jobApplicationId,
        SaveJobApplicationInterviewRequest request, CancellationToken cancellationToken) =>
        SaveInterview(jobApplicationId, null, request, cancellationToken);

    [HttpPut("{interviewId:int}")]
    public Task<ActionResult<JobApplicationInterviewResponse>> UpdateInterview(int jobApplicationId,
        int interviewId, SaveJobApplicationInterviewRequest request, CancellationToken cancellationToken) =>
        SaveInterview(jobApplicationId, interviewId, request, cancellationToken);

    private async Task<ActionResult<JobApplicationInterviewResponse>> SaveInterview(int jobApplicationId,
        int? interviewId, SaveJobApplicationInterviewRequest request, CancellationToken cancellationToken)
    {
        var result = await interviewService.SaveInterviewAsync(jobApplicationId, interviewId, request, cancellationToken);
        foreach (var error in result.ValidationErrors)
            foreach (var message in error.Value) ModelState.AddModelError(error.Key, message);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (result.Interview is null) return NotFound();
        return interviewId.HasValue ? Ok(result.Interview) :
            CreatedAtAction(nameof(GetInterviews), new { jobApplicationId }, result.Interview);
    }
}
