using JobHunt.Api.Models.Responses.Analytics;

namespace JobHunt.Api.Models.Responses.Assistant;

public record AssistantAnswerResponse(string Text, string ToolUsed,
    DateTime RetrievedAtUtc, ApplicationAnalyticsResponse? Analytics,
    AssistantRetrievalResponse? Retrieval = null);
