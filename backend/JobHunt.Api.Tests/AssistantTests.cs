using System.Net;
using System.Security.Claims;
using System.Text.Json.Nodes;
using JobHunt.Api.Data;
using JobHunt.Api.DataAccess;
using JobHunt.Api.Entities;
using JobHunt.Api.Services;
using JobHunt.Api.Services.Assistant;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JobHunt.Api.Tests;

public class AssistantTests
{
    [Theory]
    [InlineData("{\"startDate\":null,\"endDate\":null}")]
    [InlineData("{\"startDate\":\"2026-10-21\",\"endDate\":\"2026-10-21\"}")]
    public void ToolArguments_AcceptAllTimeAndDateRanges(string arguments)
    {
        var result = JobhuntAssistantService.ParseArguments(arguments);
        Assert.Equal(result.StartDate, result.EndDate);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"startDate\":null,\"endDate\":null,\"userId\":\"other-user\"}")]
    [InlineData("{\"startDate\":\"2026-10-22\",\"endDate\":\"2026-10-21\"}")]
    [InlineData("{\"startDate\":\"2026-02-30\",\"endDate\":null}")]
    [InlineData("{\"startDate\":123,\"endDate\":null}")]
    [InlineData("{\"startDate\":\"0001-01-01\",\"endDate\":null}")]
    public void ToolArguments_RejectInvalidDatesAndExtraArguments(string arguments)
    {
        Assert.Throws<AssistantException>(() => JobhuntAssistantService.ParseArguments(arguments));
    }

    [Fact]
    public async Task Ask_ExecutesToolForSignedInUser_AndReturnsDataToModel()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var currentUser = new CurrentUser(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "demo")], "Test"))
            }
        });
        await using var database = new JobHuntDbContext(
            new DbContextOptionsBuilder<JobHuntDbContext>().UseSqlite(connection).Options, currentUser);
        await database.Database.EnsureCreatedAsync();
        database.Users.AddRange(new ApplicationUser { Id = "demo" }, new ApplicationUser { Id = "other" });
        database.JobApplications.AddRange(
            new JobApplication { UserId = "demo", CompanyName = "Visible", RoleTitle = "Developer", ApplicationDate = new(2026, 10, 21), Status = JobApplicationStatus.Applied },
            new JobApplication { UserId = "demo", CompanyName = "Outside selected range", RoleTitle = "Developer", ApplicationDate = new(2026, 10, 20), Status = JobApplicationStatus.Applied },
            new JobApplication { UserId = "other", CompanyName = "Private other account", RoleTitle = "Developer", ApplicationDate = new(2026, 10, 21), Status = JobApplicationStatus.Offer });
        await database.SaveChangesAsync();

        var handler = new RecordingHandler(
            """{"choices":[{"finish_reason":"tool_calls","message":{"role":"assistant","content":null,"reasoning":"provider-only metadata","tool_calls":[{"type":"function","id":"call_1","function":{"name":"get_application_analytics","arguments":"{\"startDate\":\"2026-10-21\",\"endDate\":\"2026-10-21\"}"}}]}}]}""",
            """{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"You applied once on 21 October 2026."}}]}""");
        var service = new JobhuntAssistantService(CreateClient(handler),
            new ApplicationAnalyticsService(new ApplicationAnalyticsDataAccess(database)), null!);

        var answer = await service.AskAsync("How many applications on 21 October 2026?", CancellationToken.None);

        Assert.NotNull(answer.Analytics);
        Assert.Equal(1, answer.Analytics.TotalApplications);
        Assert.Equal("get_application_analytics", answer.ToolUsed);
        Assert.Contains("applied once", answer.Text);
        Assert.Equal(2, handler.Requests.Count);
        var first = handler.Requests[0];
        Assert.Equal("openai/gpt-oss-20b", first["model"]!.GetValue<string>());
        Assert.Equal("get_application_analytics", first["tools"]![0]!["function"]!["name"]!.GetValue<string>());
        Assert.Equal("required", first["tool_choice"]!.GetValue<string>());
        Assert.Equal(2, first["tools"]!.AsArray().Count);
        var second = handler.Requests[1];
        Assert.Equal("none", second["tool_choice"]!.GetValue<string>());
        var toolOutput = second["messages"]!.AsArray().Last()!;
        Assert.Equal("tool", toolOutput["role"]!.GetValue<string>());
        Assert.Equal("call_1", toolOutput["tool_call_id"]!.GetValue<string>());
        var data = JsonNode.Parse(toolOutput["content"]!.GetValue<string>())!;
        Assert.Equal(1, data["totalApplications"]!.GetValue<int>());
        Assert.DoesNotContain("Private other account", second.ToJsonString());
        Assert.DoesNotContain("userId", toolOutput.ToJsonString());
        Assert.DoesNotContain("provider-only metadata", second.ToJsonString());
    }

    [Fact]
    public async Task MissingKey_DoesNotContactProvider()
    {
        var handler = new RecordingHandler();
        var client = new GroqChatClient(new HttpClient(handler), new ConfigurationBuilder().Build());
        var error = await Assert.ThrowsAsync<AssistantException>(() =>
            client.CreateAsync([], [], true, CancellationToken.None));
        Assert.Equal(503, error.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ProviderErrors_DoNotExposeProviderBody(HttpStatusCode status)
    {
        var handler = new RecordingHandler("provider-sensitive-detail") { Status = status };
        var error = await Assert.ThrowsAsync<AssistantException>(() =>
            CreateClient(handler).CreateAsync([], [], true, CancellationToken.None));
        Assert.Equal(502, error.StatusCode);
        Assert.DoesNotContain("provider-sensitive-detail", error.Message);
    }

    [Fact]
    public async Task OldOpenAiKey_DoesNotGetSentToGroq()
    {
        var handler = new RecordingHandler();
        var client = new GroqChatClient(new HttpClient(handler), new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Assistant:ApiKey"] = "old-openai-key" }).Build());
        var error = await Assert.ThrowsAsync<AssistantException>(() => client.CreateAsync([], [], true, CancellationToken.None));
        Assert.Equal(503, error.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"partial\"}}]}")]
    [InlineData("{\"choices\":[]}")]
    public async Task IncompleteResponse_IsRejected(string response)
    {
        var error = await Assert.ThrowsAsync<AssistantException>(() =>
            CreateClient(new RecordingHandler(response)).CreateAsync([], [], false, CancellationToken.None));
        Assert.Equal(502, error.StatusCode);
    }

    [Fact]
    public async Task UnsupportedTool_IsRejectedBeforeDatabaseAccess()
    {
        var handler = new RecordingHandler("""{"choices":[{"finish_reason":"tool_calls","message":{"role":"assistant","tool_calls":[{"id":"bad","type":"function","function":{"name":"run_sql","arguments":"{}"}}]}}]}""");
        var service = new JobhuntAssistantService(CreateClient(handler), null!, null!);
        var error = await Assert.ThrowsAsync<AssistantException>(() => service.AskAsync("Run arbitrary SQL", CancellationToken.None));
        Assert.Contains("supported data tool", error.Message);
        Assert.Single(handler.Requests);
    }

    private static GroqChatClient CreateClient(HttpMessageHandler handler) => new(
        new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Assistant:GroqApiKey"] = "test-only-placeholder" }).Build());

    private sealed class RecordingHandler(params string[] responses) : HttpMessageHandler
    {
        public List<JsonObject> Requests { get; } = [];
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://api.groq.com/openai/v1/chat/completions", request.RequestUri!.ToString());
            Requests.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject());
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(responses[Requests.Count - 1], System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
