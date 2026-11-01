namespace JobHunt.Api.Models.Responses.Assistant;

public record AssistantSourceResponse(string Id, int JobApplicationId, int? InterviewId,
    string Title, string Kind, int ChunkNumber, string Passage);

public record AssistantRetrievalResponse(string Query, int DocumentsSearched,
    int ChunksSearched, IReadOnlyList<AssistantSourceResponse> Sources);
