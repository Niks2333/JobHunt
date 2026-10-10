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
    public Task<List<JobApplicationResponse>> GetJobApplications(CancellationToken cancellationToken) =>
        jobApplicationService.GetJobApplicationsAsync(cancellationToken);

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
        var validationErrors = jobApplicationService.ValidateCreateRequest(request);
        foreach (var validationError in validationErrors)
            foreach (var message in validationError.Value)
                ModelState.AddModelError(validationError.Key, message);

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var jobApplication = await jobApplicationService.CreateJobApplicationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetJobApplicationDetails),
            new { jobApplicationId = jobApplication.JobApplicationId }, jobApplication);
    }
}
