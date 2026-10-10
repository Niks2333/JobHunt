namespace JobHunt.Api.Models.Responses.JobApplications;

public record JobApplicationStatusHistoryResponse(
    int JobApplicationStatusHistoryId,
    string Status,
    DateOnly OccurredOn,
    DateTime RecordedAtUtc);
