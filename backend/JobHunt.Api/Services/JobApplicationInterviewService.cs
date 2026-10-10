using JobHunt.Api.DataAccess;
using JobHunt.Api.Entities;
using JobHunt.Api.Models.Requests.Interviews;
using JobHunt.Api.Models.Responses.Interviews;
using JobHunt.Api.Services.Results;

namespace JobHunt.Api.Services;

public class JobApplicationInterviewService(JobApplicationInterviewDataAccess interviewDataAccess)
{
    public async Task<IReadOnlyList<JobApplicationInterviewResponse>?> GetInterviewsAsync(
        int jobApplicationId, CancellationToken cancellationToken)
    {
        if (await interviewDataAccess.GetApplicationAsync(jobApplicationId, cancellationToken) is null) return null;
        var interviews = await interviewDataAccess.GetInterviewsAsync(jobApplicationId, cancellationToken);
        return interviews.Select(CreateResponse).ToList();
    }

    public async Task<UpcomingInterviewsResponse> GetUpcomingInterviewsAsync(CancellationToken cancellationToken)
    {
        var upcoming = await interviewDataAccess.GetUpcomingInterviewsAsync(DateTime.UtcNow, cancellationToken);
        return new(upcoming.Items.Select(interview => new UpcomingInterviewResponse(CreateResponse(interview),
            interview.JobApplication.CompanyName, interview.JobApplication.RoleTitle)).ToList(), upcoming.Total);
    }

    public async Task<SaveJobApplicationInterviewResult> SaveInterviewAsync(int jobApplicationId,
        int? interviewId, SaveJobApplicationInterviewRequest request, CancellationToken cancellationToken)
    {
        var application = await interviewDataAccess.GetApplicationAsync(jobApplicationId, cancellationToken);
        if (application is null) return new(null, []);
        var interview = interviewId.HasValue
            ? await interviewDataAccess.GetInterviewForUpdateAsync(jobApplicationId, interviewId.Value, cancellationToken)
            : new JobApplicationInterview { JobApplicationId = jobApplicationId, CreatedAtUtc = DateTime.UtcNow };
        if (interview is null) return new(null, []);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.RoundName)) errors[nameof(request.RoundName)] = ["Enter a round name."];
        if (!Enum.GetNames<InterviewMode>().Contains(request.Mode, StringComparer.Ordinal))
            errors[nameof(request.Mode)] = ["Choose Online, Phone, or InPerson."];
        if (request.ScheduledAt is not DateTimeOffset scheduledAt || scheduledAt == DateTimeOffset.MinValue ||
            DateOnly.FromDateTime(scheduledAt.ToOffset(TimeSpan.FromHours(5.5)).DateTime) < application.ApplicationDate)
            errors[nameof(request.ScheduledAt)] = ["Choose an interview date on or after the application date."];
        if (!string.IsNullOrWhiteSpace(request.MeetingUrl) &&
            (!Uri.TryCreate(request.MeetingUrl.Trim(), UriKind.Absolute, out var meetingUri) ||
             (meetingUri.Scheme != Uri.UriSchemeHttp && meetingUri.Scheme != Uri.UriSchemeHttps)))
            errors[nameof(request.MeetingUrl)] = ["Enter a complete HTTP or HTTPS meeting link."];
        if (errors.Count > 0) return new(null, errors);

        interview.RoundName = request.RoundName.Trim();
        interview.ScheduledAtUtc = request.ScheduledAt!.Value.UtcDateTime;
        interview.Mode = Enum.Parse<InterviewMode>(request.Mode);
        interview.MeetingUrl = Normalize(request.MeetingUrl);
        interview.Location = Normalize(request.Location);
        interview.PreparationNotes = Normalize(request.PreparationNotes);
        interview.Feedback = Normalize(request.Feedback);
        interview.IsCancelled = request.IsCancelled;
        interview.UpdatedAtUtc = DateTime.UtcNow;
        await interviewDataAccess.SaveInterviewAsync(interview, !interviewId.HasValue, cancellationToken);
        return new(CreateResponse(interview), []);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static JobApplicationInterviewResponse CreateResponse(JobApplicationInterview interview) => new(
        interview.JobApplicationInterviewId, interview.JobApplicationId, interview.RoundName,
        DateTime.SpecifyKind(interview.ScheduledAtUtc, DateTimeKind.Utc), interview.Mode.ToString(),
        interview.MeetingUrl, interview.Location, interview.PreparationNotes, interview.Feedback,
        interview.IsCancelled, DateTime.SpecifyKind(interview.CreatedAtUtc, DateTimeKind.Utc),
        DateTime.SpecifyKind(interview.UpdatedAtUtc, DateTimeKind.Utc));
}
