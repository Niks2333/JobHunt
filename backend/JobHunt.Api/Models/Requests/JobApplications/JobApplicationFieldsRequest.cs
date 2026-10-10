using System.ComponentModel.DataAnnotations;

namespace JobHunt.Api.Models.Requests.JobApplications;

public abstract class JobApplicationFieldsRequest
{
    [Required, StringLength(150)]
    public string CompanyName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string RoleTitle { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Location { get; set; }

    [StringLength(100)]
    public string? Source { get; set; }

    [StringLength(2048)]
    public string? JobUrl { get; set; }

    public DateOnly ApplicationDate { get; set; }
    public string? Notes { get; set; }
}
