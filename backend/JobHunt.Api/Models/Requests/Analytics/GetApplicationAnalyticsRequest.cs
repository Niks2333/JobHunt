using System.ComponentModel.DataAnnotations;

namespace JobHunt.Api.Models.Requests.Analytics;

public class GetApplicationAnalyticsRequest : IValidatableObject
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate == DateOnly.MinValue || EndDate == DateOnly.MinValue)
            yield return new ValidationResult("Choose a valid application date.", [nameof(StartDate), nameof(EndDate)]);
        if (StartDate.HasValue && EndDate.HasValue && StartDate > EndDate)
            yield return new ValidationResult("Start date must be on or before end date.", [nameof(EndDate)]);
    }
}
