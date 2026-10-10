namespace JobHunt.Api.Entities;

public class JobApplication
{
    public int JobApplicationId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string RoleTitle { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Source { get; set; }
    public string? JobUrl { get; set; }
    public DateOnly ApplicationDate { get; set; }
    public JobApplicationStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<JobApplicationStatusHistory> StatusHistory { get; set; } = [];
}
