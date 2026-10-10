namespace JobHunt.Api.Entities;

public class JobApplicationInterview
{
    public int JobApplicationInterviewId { get; set; }
    public int JobApplicationId { get; set; }
    public JobApplication JobApplication { get; set; } = null!;
    public string RoundName { get; set; } = string.Empty;
    public DateTime ScheduledAtUtc { get; set; }
    public InterviewMode Mode { get; set; }
    public string? MeetingUrl { get; set; }
    public string? Location { get; set; }
    public string? PreparationNotes { get; set; }
    public string? Feedback { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
