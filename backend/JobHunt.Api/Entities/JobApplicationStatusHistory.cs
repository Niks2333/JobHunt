namespace JobHunt.Api.Entities;

public class JobApplicationStatusHistory
{
    public int JobApplicationStatusHistoryId { get; set; }
    public int JobApplicationId { get; set; }
    public JobApplication JobApplication { get; set; } = null!;
    public JobApplicationStatus Status { get; set; }
    // The initial application has a date, not a known event time. Preserve that precision.
    public DateOnly OccurredOn { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
