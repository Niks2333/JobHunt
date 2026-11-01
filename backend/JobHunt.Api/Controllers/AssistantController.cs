using JobHunt.Api.Models.Requests.Assistant;
using JobHunt.Api.Models.Responses.Assistant;
using JobHunt.Api.Services.Assistant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobHunt.Api.Controllers;

[ApiController]
[Route("api/assistant")]
[EnableRateLimiting("assistant")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AssistantController(JobhuntAssistantService assistant) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<ActionResult<AssistantAnswerResponse>> Ask(AskAssistantRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await assistant.AskAsync(request.Question, cancellationToken)); }
        catch (AssistantException exception) { return Problem(statusCode: exception.StatusCode, detail: exception.Message); }
    }
}
