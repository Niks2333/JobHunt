namespace JobHunt.Api.DataAccess.Results;

public record AssistantKnowledgeDocument(string DocumentId, int JobApplicationId,
    int? InterviewId, string Title, string Kind, string Text);
