using System.ComponentModel.DataAnnotations;
using JobHunt.Api.Entities;

namespace JobHunt.Api.Models.Requests.JobApplications;

public class GetJobApplicationsRequest : IValidatableObject
{
    [StringLength(150)]
    public string? Search { get; set; }
    [StringLength(30)]
    public string? Status { get; set; }
    [StringLength(100)]
    public string? Source { get; set; }
    [Required]
    [RegularExpression("^(newest|oldest|company)$", ErrorMessage = "Choose a valid sort order: newest, oldest, or company.")]
    public string SortBy { get; set; } = "newest";
    [Range(1, 1_000_000)]
    public int PageNumber { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Status) &&
            !Enum.GetNames<JobApplicationStatus>().Contains(Status.Trim(), StringComparer.Ordinal))
            yield return new ValidationResult("Choose a valid application status.", [nameof(Status)]);
    }
}
