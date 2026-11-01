using JobHunt.Api.Data;
using JobHunt.Api.DataAccess.Results;
using Microsoft.EntityFrameworkCore;

namespace JobHunt.Api.DataAccess;

public class AssistantKnowledgeDataAccess(JobHuntDbContext database)
{
    public async Task<IReadOnlyList<AssistantKnowledgeDocument>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        // Existing global EF filters apply before text enters the retrieval corpus.
        // Only text and source metadata are loaded; no credentials or account records.
        var notes = await database.JobApplications.AsNoTracking()
            .Where(application => application.Notes != null && application.Notes != "")
            .Select(application => new { application.JobApplicationId, application.CompanyName, application.RoleTitle, application.Notes })
            .ToListAsync(cancellationToken);
        var interviews = await database.JobApplicationInterviews.AsNoTracking()
            .Where(interview => interview.PreparationNotes != null || interview.Feedback != null)
            .Select(interview => new { interview.JobApplicationId, interview.JobApplicationInterviewId,
                interview.JobApplication.CompanyName, interview.JobApplication.RoleTitle,
                interview.RoundName, interview.IsCancelled, interview.PreparationNotes, interview.Feedback })
            .ToListAsync(cancellationToken);
        var documents = notes.Select(note => new AssistantKnowledgeDocument(
            $"application:{note.JobApplicationId}:notes", note.JobApplicationId, null,
            $"{note.CompanyName} · {note.RoleTitle}", "Application notes", note.Notes!)).ToList();
        foreach (var interview in interviews)
        {
            var title = $"{interview.CompanyName} · {interview.RoleTitle} · {interview.RoundName}" +
                (interview.IsCancelled ? " (cancelled round)" : "");
            if (!string.IsNullOrWhiteSpace(interview.PreparationNotes))
                documents.Add(new($"interview:{interview.JobApplicationInterviewId}:preparation",
                    interview.JobApplicationId, interview.JobApplicationInterviewId, title,
                    "Interview preparation", interview.PreparationNotes));
            if (!string.IsNullOrWhiteSpace(interview.Feedback))
                documents.Add(new($"interview:{interview.JobApplicationInterviewId}:feedback",
                    interview.JobApplicationId, interview.JobApplicationInterviewId, title,
                    "Interview feedback", interview.Feedback));
        }
        return documents;
    }
}
