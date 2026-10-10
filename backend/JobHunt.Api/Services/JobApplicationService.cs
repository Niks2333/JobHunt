using JobHunt.Api.DataAccess;
using JobHunt.Api.Entities;
using JobHunt.Api.Models.Requests.JobApplications;
using JobHunt.Api.Models.Responses.JobApplications;
using JobHunt.Api.Services.Results;

namespace JobHunt.Api.Services;

public class JobApplicationService(JobApplicationDataAccess jobApplicationDataAccess)
{
    public Dictionary<string, string[]> ValidateApplicationRequest(JobApplicationFieldsRequest request)
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

    public async Task<JobApplicationPageResponse> GetJobApplicationsAsync(
        GetJobApplicationsRequest request, CancellationToken cancellationToken)
    {
        var applicationPage = await jobApplicationDataAccess.GetJobApplicationsAsync(request, cancellationToken);
        var totalPages = (int)Math.Ceiling(applicationPage.MatchingApplications / (double)request.PageSize);
        return new(applicationPage.Items.Select(CreateResponse).ToList(),
            applicationPage.TotalApplications, applicationPage.MatchingApplications,
            request.PageNumber, request.PageSize, totalPages, applicationPage.AvailableSources);
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

    public async Task<UpdateJobApplicationResult> UpdateJobApplicationAsync(
        int jobApplicationId, UpdateJobApplicationRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = ValidateApplicationRequest(request);
        if (validationErrors.Count > 0) return new(null, validationErrors);

        var jobApplication = await jobApplicationDataAccess.GetJobApplicationForUpdateAsync(
            jobApplicationId, cancellationToken);
        if (jobApplication is null) return new(null, []);

        if (request.ApplicationDate != jobApplication.ApplicationDate)
        {
            if (await jobApplicationDataAccess.HasInterviewBeforeApplicationDateAsync(
                jobApplicationId, request.ApplicationDate, cancellationToken))
                return new(null, new() { [nameof(request.ApplicationDate)] =
                    ["The application date cannot be later than a saved interview date."] });

            var initialAppliedHistory = jobApplication.StatusHistory
                .Where(historyEntry => historyEntry.Status == JobApplicationStatus.Applied)
                .OrderBy(historyEntry => historyEntry.JobApplicationStatusHistoryId)
                .FirstOrDefault();

            if (initialAppliedHistory is null)
                return new(null, new() { [nameof(request.ApplicationDate)] =
                    ["The application date cannot be corrected because its initial Applied history is missing."] });

            if (jobApplication.StatusHistory.Any(historyEntry =>
                historyEntry.JobApplicationStatusHistoryId != initialAppliedHistory.JobApplicationStatusHistoryId &&
                historyEntry.OccurredOn < request.ApplicationDate))
                return new(null, new() { [nameof(request.ApplicationDate)] =
                    ["The application date cannot be later than a recorded status change."] });

            // Correct the original event instead of adding a new status transition.
            initialAppliedHistory.OccurredOn = request.ApplicationDate;
        }

        jobApplication.CompanyName = request.CompanyName.Trim();
        jobApplication.RoleTitle = request.RoleTitle.Trim();
        jobApplication.Location = NormalizeOptionalValue(request.Location);
        jobApplication.Source = NormalizeOptionalValue(request.Source);
        jobApplication.JobUrl = NormalizeOptionalValue(request.JobUrl);
        jobApplication.ApplicationDate = request.ApplicationDate;
        jobApplication.Notes = NormalizeOptionalValue(request.Notes);
        jobApplication.UpdatedAtUtc = DateTime.UtcNow;

        await jobApplicationDataAccess.SaveJobApplicationChangesAsync(cancellationToken);
        return new(CreateResponse(jobApplication), []);
    }

    public async Task<ChangeJobApplicationStatusResult> ChangeJobApplicationStatusAsync(
        int jobApplicationId, ChangeJobApplicationStatusRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = new Dictionary<string, string[]>();
        // Accept the named stages only: Enum.TryParse alone would also accept numeric strings.
        if (!Enum.GetNames<JobApplicationStatus>().Contains(request.Status, StringComparer.Ordinal))
            validationErrors[nameof(request.Status)] = ["Choose a valid application status."];

        var todayInIndia = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5.5)).DateTime);
        if (request.OccurredOn == default || request.OccurredOn > todayInIndia)
            validationErrors[nameof(request.OccurredOn)] = ["Choose a status change date that is today or earlier."];

        if (validationErrors.Count > 0) return new(null, validationErrors);
        var jobApplication = await jobApplicationDataAccess.GetJobApplicationForUpdateAsync(
            jobApplicationId, cancellationToken);
        if (jobApplication is null) return new(null, []);

        var newStatus = Enum.Parse<JobApplicationStatus>(request.Status);
        if (newStatus == jobApplication.Status)
            validationErrors[nameof(request.Status)] = ["Choose a status different from the current status."];

        if (request.OccurredOn < jobApplication.ApplicationDate ||
            jobApplication.StatusHistory.Any(historyEntry => historyEntry.OccurredOn > request.OccurredOn))
            validationErrors[nameof(request.OccurredOn)] =
                ["The status change date cannot be earlier than the application date or latest status change."];

        if (validationErrors.Count > 0) return new(null, validationErrors);

        var recordedAtUtc = DateTime.UtcNow;
        jobApplication.Status = newStatus;
        jobApplication.UpdatedAtUtc = recordedAtUtc;
        jobApplication.StatusHistory.Add(new JobApplicationStatusHistory
        {
            Status = newStatus,
            OccurredOn = request.OccurredOn,
            RecordedAtUtc = recordedAtUtc
        });
        // The tracked status and new history entry are saved in one EF Core transaction.
        await jobApplicationDataAccess.SaveJobApplicationChangesAsync(cancellationToken);
        return new(CreateResponse(jobApplication), []);
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
