using JobHunt.Api.Data;
using JobHunt.Api.DataAccess;
using JobHunt.Api.Services;
using Microsoft.EntityFrameworkCore;

var applicationBuilder = WebApplication.CreateBuilder(args);

applicationBuilder.Services.AddControllers();
applicationBuilder.Services.AddProblemDetails();
applicationBuilder.Services.AddDbContext<JobHuntDbContext>(databaseOptions =>
    databaseOptions.UseSqlServer(applicationBuilder.Configuration.GetConnectionString("JobHuntDatabase")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:JobHuntDatabase.")));
applicationBuilder.Services.AddScoped<JobApplicationDataAccess>();
applicationBuilder.Services.AddScoped<JobApplicationService>();

var webApplication = applicationBuilder.Build();

// Return consistent error responses without exposing database details to the browser.
webApplication.UseExceptionHandler();
webApplication.UseStatusCodePages();
webApplication.MapControllers();
webApplication.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
webApplication.Run();
