using JobHunt.Api.Models.Requests.JobApplications;
using JobHunt.Api.Models.Responses.JobApplications;
using JobHunt.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobHunt.Api.Controllers;

[ApiController]
[Route("api/applications")]
public class JobApplicationsController(JobApplicationService jobApplicationService) : ControllerBase
{
    [HttpGet]
    public Task<JobApplicationPageResponse> GetJobApplications(
        [FromQuery] GetJobApplicationsRequest request, CancellationToken cancellationToken) =>
        jobApplicationService.GetJobApplicationsAsync(request, cancellationToken);

    [HttpGet("{jobApplicationId:int}")]
    public async Task<ActionResult<JobApplicationDetailsResponse>> GetJobApplicationDetails(
        int jobApplicationId, CancellationToken cancellationToken)
    {
        var applicationDetails = await jobApplicationService.GetJobApplicationDetailsAsync(
            jobApplicationId, cancellationToken);
        return applicationDetails is null ? NotFound() : Ok(applicationDetails);
    }

    [HttpPost]
    public async Task<ActionResult<JobApplicationResponse>> CreateJobApplication(
        CreateJobApplicationRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = jobApplicationService.ValidateApplicationRequest(request);
        foreach (var validationError in validationErrors)
            foreach (var message in validationError.Value)
                ModelState.AddModelError(validationError.Key, message);

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var jobApplication = await jobApplicationService.CreateJobApplicationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetJobApplicationDetails),
            new { jobApplicationId = jobApplication.JobApplicationId }, jobApplication);
    }
    [HttpPut("{jobApplicationId:int}")]
    public async Task<ActionResult<JobApplicationResponse>> UpdateJobApplication(
        int jobApplicationId, UpdateJobApplicationRequest request, CancellationToken cancellationToken)
    {
        var updateResult = await jobApplicationService.UpdateJobApplicationAsync(
            jobApplicationId, request, cancellationToken);
        foreach (var validationError in updateResult.ValidationErrors)
            foreach (var message in validationError.Value)
                ModelState.AddModelError(validationError.Key, message);

        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return updateResult.Application is null ? NotFound() : Ok(updateResult.Application);
    }
    [HttpPost("{jobApplicationId:int}/status-changes")]
    public async Task<ActionResult<JobApplicationResponse>> ChangeJobApplicationStatus(
        int jobApplicationId, ChangeJobApplicationStatusRequest request, CancellationToken cancellationToken)
    {
        var changeResult = await jobApplicationService.ChangeJobApplicationStatusAsync(
            jobApplicationId, request, cancellationToken);
        foreach (var validationError in changeResult.ValidationErrors)
            foreach (var message in validationError.Value)
                ModelState.AddModelError(validationError.Key, message);

        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return changeResult.Application is null ? NotFound() : Ok(changeResult.Application);
    }
}
