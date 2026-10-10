using System.ComponentModel.DataAnnotations;

namespace JobHunt.Api.Models.Requests.JobApplications;

public class ChangeJobApplicationStatusRequest
{
    [Required, StringLength(30)]
    public string Status { get; set; } = string.Empty;
    public DateOnly OccurredOn { get; set; }
}
