namespace JobHunt.Api.Models.Responses.Interviews;

public record JobApplicationInterviewResponse(
    int JobApplicationInterviewId, int JobApplicationId, string RoundName,
    DateTime ScheduledAtUtc, string Mode, string? MeetingUrl, string? Location,
    string? PreparationNotes, string? Feedback, bool IsCancelled,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public record UpcomingInterviewResponse(JobApplicationInterviewResponse Interview,
    string CompanyName, string RoleTitle);

public record UpcomingInterviewsResponse(IReadOnlyList<UpcomingInterviewResponse> Items,
    int TotalUpcomingInterviews);
