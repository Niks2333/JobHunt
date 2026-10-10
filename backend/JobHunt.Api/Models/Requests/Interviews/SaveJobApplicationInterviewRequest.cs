using System.ComponentModel.DataAnnotations;

namespace JobHunt.Api.Models.Requests.Interviews;

public class SaveJobApplicationInterviewRequest
{
    [Required, StringLength(150)]
    public string RoundName { get; set; } = string.Empty;
    [Required]
    public DateTimeOffset? ScheduledAt { get; set; }
    [Required, StringLength(30)]
    public string Mode { get; set; } = string.Empty;
    [StringLength(2048)]
    public string? MeetingUrl { get; set; }
    [StringLength(200)]
    public string? Location { get; set; }
    [StringLength(5000)]
    public string? PreparationNotes { get; set; }
    [StringLength(5000)]
    public string? Feedback { get; set; }
    public bool IsCancelled { get; set; }
}
