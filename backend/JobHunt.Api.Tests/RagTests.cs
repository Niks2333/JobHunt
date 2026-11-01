using System.Net;
using System.Security.Claims;
using System.Text.Json.Nodes;
using JobHunt.Api.Data;
using JobHunt.Api.DataAccess;
using JobHunt.Api.DataAccess.Results;
using JobHunt.Api.Entities;
using JobHunt.Api.Services;
using JobHunt.Api.Services.Assistant;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobHunt.Api.Tests;

public class RagTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private JobHuntDbContext database = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        // Keep the test identity stable across xUnit's async setup/test contexts.
        var user = new CurrentUser(new FixedContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "demo")], "Test"))
            }
        });
        database = new(new DbContextOptionsBuilder<JobHuntDbContext>().UseSqlite(connection).Options, user);
        await database.Database.EnsureCreatedAsync();
        database.Users.AddRange(new ApplicationUser { Id = "demo" }, new ApplicationUser { Id = "other" });
        database.JobApplications.AddRange(
            new JobApplication { UserId = "demo", CompanyName = "Cedar Labs", RoleTitle = "Backend developer", ApplicationDate = new(2026, 10, 20), Notes = "Build REST APIs with ASP.NET Core." },
            new JobApplication { UserId = "other", CompanyName = "Private Other", RoleTitle = "Developer", ApplicationDate = new(2026, 10, 20), Notes = "SQL secret-other-account" });
        await database.SaveChangesAsync();
        var visible = await database.JobApplications.SingleAsync();
        database.JobApplicationInterviews.Add(new()
        {
            JobApplicationId = visible.JobApplicationId,
            RoundName = "Technical round", ScheduledAtUtc = new(2026, 10, 21),
            PreparationNotes = "Practice Angular forms before the interview.",
            Feedback = "Revise SQL joins and indexes. Explain database transactions with examples."
        });
        var other = await database.JobApplications.IgnoreQueryFilters().SingleAsync(app => app.UserId == "other");
        database.JobApplicationInterviews.Add(new()
        {
            JobApplicationId = other.JobApplicationId, RoundName = "Private round",
            ScheduledAtUtc = new(2026, 10, 21), Feedback = "SQL secret-other-feedback"
        });
        await database.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await database.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Retrieval_UsesOnlyCurrentUsersNotesAndInterviewText()
    {
        var documents = await new AssistantKnowledgeDataAccess(database).GetDocumentsAsync(CancellationToken.None);
        Assert.Equal(3, documents.Count);
        Assert.DoesNotContain(documents, doc => doc.Text.Contains("secret-other"));
        var result = NoteRetrievalService.Rank("SQL joins", documents);
        var source = Assert.Single(result.Sources);
        Assert.Equal("Interview feedback", source.Kind);
        Assert.Contains("SQL joins", source.Passage);
        Assert.Equal("S1", source.Id);
        Assert.NotNull(source.InterviewId);
    }

    [Fact]
    public async Task Retrieval_ImmediatelyReflectsEditsAndDeletes()
    {
        var search = new NoteRetrievalService(new AssistantKnowledgeDataAccess(database));
        Assert.NotEmpty((await search.SearchAsync("SQL", CancellationToken.None)).Sources);
        var interview = await database.JobApplicationInterviews.SingleAsync();
        interview.Feedback = "Revise accessibility and keyboard navigation.";
        await database.SaveChangesAsync();
        Assert.Empty((await search.SearchAsync("SQL", CancellationToken.None)).Sources);
        Assert.NotEmpty((await search.SearchAsync("accessibility", CancellationToken.None)).Sources);
        database.JobApplications.Remove(await database.JobApplications.SingleAsync());
        await database.SaveChangesAsync();
        Assert.Empty((await search.SearchAsync("accessibility", CancellationToken.None)).Sources);
    }

    [Fact]
    public void Chunking_PreservesOverlapAndRetrievesContentNearEnd()
    {
        var text = new string('x', 1900) + " revise SQL indexes";
        var chunks = NoteRetrievalService.Chunk(text);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, 1000));
        Assert.Equal(chunks[0][^150..], chunks[1][..150]);
        var result = NoteRetrievalService.Rank("SQL indexes", [Doc("long", text)]);
        Assert.Contains(result.Sources, source => source.ChunkNumber > 1 && source.Passage.Contains("SQL indexes"));
    }

    [Fact]
    public void Ranking_PrefersRelevantPassageAndBoundsContext()
    {
        var docs = new List<AssistantKnowledgeDocument> { Doc("unrelated", "Angular components and CSS styles."), Doc("relevant", "SQL indexes SQL indexes and query execution plans.") };
        docs.AddRange(Enumerable.Range(0, 9).Select(index => Doc($"other-{index}", "Review SQL joins.")));
        var result = NoteRetrievalService.Rank("SQL indexes", docs);
        Assert.Contains("indexes", result.Sources[0].Passage);
        Assert.Equal(5, result.Sources.Count);
        Assert.Equal(5, result.Sources.Select(source => source.Id).Distinct().Count());
        Assert.DoesNotContain(result.Sources, source => source.Passage.Contains("CSS"));
    }

    [Theory]
    [InlineData("kubernetes", "Only Angular and SQL are discussed.", false)]
    [InlineData(".NET C#", "Experience building dotnet and csharp APIs.", true)]
    [InlineData("what did my feedback say", "Practice SQL.", false)]
    public void Retrieval_HandlesNoMatchesAndTechnicalTerms(string query, string text, bool expected)
    {
        Assert.Equal(expected, NoteRetrievalService.Rank(query, [Doc("doc", text)]).Sources.Count > 0);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"query\":\" \"}")]
    [InlineData("{\"query\":42}")]
    [InlineData("{\"query\":\"SQL\",\"userId\":\"other\"}")]
    [InlineData("{\"query\":\"SQL\",\"sourceType\":\"credentials\"}")]
    public void SearchArguments_RejectInvalidAndCrossUserFields(string arguments) =>
        Assert.Throws<AssistantException>(() => JobhuntAssistantService.ParseSearchArguments(arguments));

    [Theory]
    [InlineData("Practice SQL joins and indexes. [S1]", true)]
    [InlineData("Practice SQL joins. [S99]", false)]
    [InlineData("Unsupported answer without citations.", false)]
    public async Task Rag_TransmitsRetrievedPassagesAndChecksCitations(string answer, bool valid)
    {
        var handler = new ProviderHandler("SQL", answer);
        var assistant = CreateAssistant(handler);
        if (!valid)
        {
            var error = await Assert.ThrowsAsync<AssistantException>(() => assistant.AskAsync("What SQL should I revise?", CancellationToken.None));
            Assert.Equal(502, error.StatusCode);
            return;
        }
        var result = await assistant.AskAsync("What SQL should I revise?", CancellationToken.None);
        Assert.Null(result.Analytics);
        Assert.Equal("search_application_notes", result.ToolUsed);
        Assert.Single(result.Retrieval!.Sources);
        Assert.Equal(2, handler.Requests.Count);
        var messages = handler.Requests[1]["messages"]!.AsArray();
        var data = messages.Last()!;
        Assert.Equal("tool", data["role"]!.GetValue<string>());
        Assert.Contains("SQL joins", data["content"]!.GetValue<string>());
        Assert.DoesNotContain("secret-other", data.ToJsonString());
        Assert.DoesNotContain("Angular forms", data.ToJsonString());
        Assert.Equal("none", handler.Requests[1]["tool_choice"]!.GetValue<string>());
    }

    [Fact]
    public async Task NoMatches_AbstainsWithoutSecondModelRequest()
    {
        var handler = new ProviderHandler("kubernetes", "This answer must never be requested.");
        var result = await CreateAssistant(handler).AskAsync("What Kubernetes feedback do I have?", CancellationToken.None);
        Assert.Contains("couldn't find", result.Text);
        Assert.Empty(result.Retrieval!.Sources);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task FeedbackSearch_DoesNotTreatPreparationAsFeedback()
    {
        var search = new NoteRetrievalService(new AssistantKnowledgeDataAccess(database));
        Assert.NotEmpty((await search.SearchAsync("Angular", CancellationToken.None)).Sources);
        Assert.Empty((await search.SearchAsync("Angular", CancellationToken.None, "interview_feedback")).Sources);
    }

    private JobhuntAssistantService CreateAssistant(ProviderHandler handler) => new(
        new GroqChatClient(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Assistant:GroqApiKey"] = "test-placeholder" }).Build()),
        new ApplicationAnalyticsService(new ApplicationAnalyticsDataAccess(database)),
        new NoteRetrievalService(new AssistantKnowledgeDataAccess(database)));

    private static AssistantKnowledgeDocument Doc(string id, string text) => new(id, 1, null, "Example", "Interview feedback", text);

    private sealed class FixedContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class ProviderHandler(string query, string answer) : HttpMessageHandler
    {
        public List<JsonObject> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject());
            var message = Requests.Count == 1
                ? new JsonObject { ["role"] = "assistant", ["content"] = null, ["tool_calls"] = new JsonArray(new JsonObject
                    { ["id"] = "rag_call_1", ["type"] = "function", ["function"] = new JsonObject
                        { ["name"] = "search_application_notes", ["arguments"] = new JsonObject { ["query"] = query, ["sourceType"] = "interview_feedback" }.ToJsonString() } }) }
                : new JsonObject { ["role"] = "assistant", ["content"] = answer };
            var body = new JsonObject { ["choices"] = new JsonArray(new JsonObject
                { ["finish_reason"] = Requests.Count == 1 ? "tool_calls" : "stop", ["message"] = message }) };
            return new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
