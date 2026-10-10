using JobHunt.Api.Models.Requests.Analytics;
using JobHunt.Api.Models.Responses.Analytics;
using JobHunt.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobHunt.Api.Controllers;

[ApiController]
[Route("api/analytics/applications")]
public class ApplicationAnalyticsController(ApplicationAnalyticsService applicationAnalyticsService) : ControllerBase
{
    [HttpGet]
    public Task<ApplicationAnalyticsResponse> GetApplicationAnalytics(
        [FromQuery] GetApplicationAnalyticsRequest request, CancellationToken cancellationToken) =>
        applicationAnalyticsService.GetApplicationAnalyticsAsync(request, cancellationToken);
}
