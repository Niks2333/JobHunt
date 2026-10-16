using System.Security.Claims;
using JobHunt.Api.Data;
using JobHunt.Api.DataAccess;
using JobHunt.Api.Entities;
using JobHunt.Api.Models.Requests.JobApplications;
using JobHunt.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobHunt.Api.Tests;

public class JobApplicationCreationTests
{
    [Fact]
    public async Task CreateJobApplicationAsync_SavesAppliedStatusAndInitialHistory()
    {
        // ARRANGE: create an isolated database and a signed-in test user.
        // Keeping the connection open keeps this in-memory database alive.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<JobHuntDbContext>()
            .UseSqlite(connection).Options;
        var currentUser = new CurrentUser(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", "test-user")], "Test"))
            }
        });
        await using var database = new JobHuntDbContext(options, currentUser);
        await database.Database.EnsureCreatedAsync();
        database.Users.Add(new ApplicationUser { Id = "test-user", UserName = "test-user" });
        await database.SaveChangesAsync();

        var service = new JobApplicationService(new JobApplicationDataAccess(database, currentUser));
        var request = new CreateJobApplicationRequest
        {
            CompanyName = "Example Company",
            RoleTitle = "Backend Developer",
            ApplicationDate = new DateOnly(2026, 1, 15)
        };

        // ACT: call the same service method used by the API.
        var response = await service.CreateJobApplicationAsync(request, CancellationToken.None);

        // ASSERT: verify the response AND read back what was actually saved.
        Assert.Equal("Applied", response.Status);
        database.ChangeTracker.Clear();
        var savedApplication = await database.JobApplications
            .Include(application => application.StatusHistory).SingleAsync();
        Assert.Equal(JobApplicationStatus.Applied, savedApplication.Status);
        var initialHistory = Assert.Single(savedApplication.StatusHistory);
        Assert.Equal(JobApplicationStatus.Applied, initialHistory.Status);
        Assert.Equal(request.ApplicationDate, initialHistory.OccurredOn);
    }
}
