using JobHunt.Api.DataAccess;
using JobHunt.Api.Entities;
using JobHunt.Api.Models.Requests.JobApplications;
using JobHunt.Api.Models.Responses.JobApplications;

namespace JobHunt.Api.Services;

public class JobApplicationService(JobApplicationDataAccess jobApplicationDataAccess)
{
    public Dictionary<string, string[]> ValidateCreateRequest(CreateJobApplicationRequest request)
    {
        var validationErrors = new Dictionary<string, string[]>();
        // JobHunt's first version uses the user's India calendar date for date validation.
        var todayInIndia = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5.5)).DateTime);
        if (request.ApplicationDate == default || request.ApplicationDate > todayInIndia)
            validationErrors[nameof(request.ApplicationDate)] = ["Choose an application date that is today or earlier."];

        if (!string.IsNullOrWhiteSpace(request.JobUrl) &&
            (!Uri.TryCreate(request.JobUrl.Trim(), UriKind.Absolute, out var jobUri) ||
             (jobUri.Scheme != Uri.UriSchemeHttp && jobUri.Scheme != Uri.UriSchemeHttps)))
            validationErrors[nameof(request.JobUrl)] = ["Enter a complete HTTP or HTTPS job link."];

        return validationErrors;
    }

    public async Task<List<JobApplicationResponse>> GetJobApplicationsAsync(CancellationToken cancellationToken)
    {
        var jobApplications = await jobApplicationDataAccess.GetJobApplicationsAsync(cancellationToken);
        return jobApplications.Select(CreateResponse).ToList();
    }

    public async Task<JobApplicationDetailsResponse?> GetJobApplicationDetailsAsync(
        int jobApplicationId, CancellationToken cancellationToken)
    {
        var jobApplication = await jobApplicationDataAccess.GetJobApplicationWithStatusHistoryAsync(
            jobApplicationId, cancellationToken);
        if (jobApplication is null) return null;

        var statusHistory = jobApplication.StatusHistory.Select(historyEntry =>
            new JobApplicationStatusHistoryResponse(
                historyEntry.JobApplicationStatusHistoryId,
                historyEntry.Status.ToString(),
                historyEntry.OccurredOn,
                DateTime.SpecifyKind(historyEntry.RecordedAtUtc, DateTimeKind.Utc))).ToList();

        return new JobApplicationDetailsResponse(CreateResponse(jobApplication), statusHistory);
    }

    public async Task<JobApplicationResponse> CreateJobApplicationAsync(
        CreateJobApplicationRequest request, CancellationToken cancellationToken)
    {
        var recordedAtUtc = DateTime.UtcNow;
        var jobApplication = new JobApplication
        {
            CompanyName = request.CompanyName.Trim(),
            RoleTitle = request.RoleTitle.Trim(),
            Location = NormalizeOptionalValue(request.Location),
            Source = NormalizeOptionalValue(request.Source),
            JobUrl = NormalizeOptionalValue(request.JobUrl),
            ApplicationDate = request.ApplicationDate,
            Status = JobApplicationStatus.Applied,
            Notes = NormalizeOptionalValue(request.Notes),
            CreatedAtUtc = recordedAtUtc,
            UpdatedAtUtc = recordedAtUtc,
            StatusHistory = [new JobApplicationStatusHistory
            {
                Status = JobApplicationStatus.Applied,
                OccurredOn = request.ApplicationDate,
                RecordedAtUtc = recordedAtUtc
            }]
        };

        await jobApplicationDataAccess.CreateJobApplicationAsync(jobApplication, cancellationToken);
        return CreateResponse(jobApplication);
    }

    private static string? NormalizeOptionalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static JobApplicationResponse CreateResponse(JobApplication jobApplication) => new(
        jobApplication.JobApplicationId, jobApplication.CompanyName, jobApplication.RoleTitle,
        jobApplication.Location, jobApplication.Source, jobApplication.JobUrl,
        jobApplication.ApplicationDate, jobApplication.Status.ToString(), jobApplication.Notes,
        // SQL Server datetime2 does not preserve DateTime.Kind. Mark known UTC values
        // explicitly so JSON responses consistently include the UTC suffix.
        DateTime.SpecifyKind(jobApplication.CreatedAtUtc, DateTimeKind.Utc),
        DateTime.SpecifyKind(jobApplication.UpdatedAtUtc, DateTimeKind.Utc));
}
