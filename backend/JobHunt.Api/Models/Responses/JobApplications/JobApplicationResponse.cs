namespace JobHunt.Api.Models.Responses.JobApplications;

public record JobApplicationResponse(
    int JobApplicationId,
    string CompanyName,
    string RoleTitle,
    string? Location,
    string? Source,
    string? JobUrl,
    DateOnly ApplicationDate,
    string Status,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
