using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace JobHunt.Api.Services.Assistant;

public class GroqChatClient(HttpClient httpClient, IConfiguration configuration)
{
    public async Task<JsonObject> CreateAsync(JsonArray messages,
        JsonArray tools, bool requestTool, CancellationToken cancellationToken)
    {
        var apiKey = configuration["Assistant:GroqApiKey"] ?? configuration["GROQ_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AssistantException(503,
                "The AI assistant needs a Groq API key. Run scripts/Set-AssistantApiKey.ps1 locally, then restart the API.");

        var body = new JsonObject
        {
            ["model"] = configuration["Assistant:GroqModel"] ?? "openai/gpt-oss-20b",
            ["messages"] = messages.DeepClone(),
            ["tools"] = tools.DeepClone(),
            ["tool_choice"] = requestTool ? "required" : "none",
            ["parallel_tool_calls"] = false,
            ["max_completion_tokens"] = 2048,
            ["temperature"] = 0.2
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(body);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Never pass provider response bodies or authorization details to the browser.
                var message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "Groq rejected the backend API key. Check the Groq key and its model access.",
                    HttpStatusCode.TooManyRequests =>
                        "Groq's usage limit was reached. Wait for your free-plan quota to reset and try again.",
                    HttpStatusCode.BadRequest =>
                        "Groq could not complete the tool request. Try rephrasing your question or check the configured model's tool support.",
                    _ => "The AI provider could not complete this request. Please try again later."
                };
                throw new AssistantException(502, message);
            }
            var result = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
            var choices = result?["choices"] as JsonArray;
            if (choices is null || choices.Count != 1 || choices[0] is not JsonObject choice ||
                choice["finish_reason"]?.GetValue<string>() != (requestTool ? "tool_calls" : "stop") ||
                choice["message"] is not JsonObject)
                throw new AssistantException(502, "The AI provider returned an incomplete answer. Please try again.");
            return (JsonObject)choice["message"]!;
        }
        catch (HttpRequestException)
        {
            throw new AssistantException(502, "The backend could not connect to Groq. Check its internet connection.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AssistantException(504, "Groq took too long to respond. Please try again.");
        }
        catch (System.Text.Json.JsonException)
        {
            throw new AssistantException(502, "The AI provider returned an unreadable response. Please try again.");
        }
    }
}
