namespace JobHunt.Api.Services.Assistant;

public class AssistantException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
