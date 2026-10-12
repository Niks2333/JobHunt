using JobHunt.Api.Data;
using JobHunt.Api.DataAccess;
using JobHunt.Api.Entities;
using JobHunt.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var applicationBuilder = WebApplication.CreateBuilder(args);

applicationBuilder.Services.AddControllers();
applicationBuilder.Services.AddProblemDetails();
applicationBuilder.Services.AddHttpContextAccessor();
applicationBuilder.Services.AddScoped<CurrentUser>();
applicationBuilder.Services.AddDbContext<JobHuntDbContext>(databaseOptions =>
    databaseOptions.UseSqlServer(applicationBuilder.Configuration.GetConnectionString("JobHuntDatabase")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:JobHuntDatabase.")));
applicationBuilder.Services.AddIdentityCore<ApplicationUser>(identityOptions =>
    {
        identityOptions.User.RequireUniqueEmail = true;
        identityOptions.Password.RequiredLength = 10;
    })
    .AddEntityFrameworkStores<JobHuntDbContext>();
applicationBuilder.Services.AddScoped<AuthService>();
applicationBuilder.Services.AddScoped<AuthSessionService>();
applicationBuilder.Services.AddScoped<RefreshTokenDataAccess>();
applicationBuilder.Services.AddScoped<JwtTokenService>();
var signingKey = applicationBuilder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
    throw new InvalidOperationException("Configure Jwt:SigningKey with at least 32 random bytes using user secrets or environment configuration.");
applicationBuilder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = applicationBuilder.Configuration["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = applicationBuilder.Configuration["Jwt:Audience"],
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(5)
    };
});
applicationBuilder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
applicationBuilder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
applicationBuilder.Services.AddScoped<JobApplicationDataAccess>();
applicationBuilder.Services.AddScoped<JobApplicationService>();
applicationBuilder.Services.AddScoped<ApplicationAnalyticsDataAccess>();
applicationBuilder.Services.AddScoped<ApplicationAnalyticsService>();
applicationBuilder.Services.AddScoped<JobApplicationInterviewDataAccess>();
applicationBuilder.Services.AddScoped<JobApplicationInterviewService>();

var webApplication = applicationBuilder.Build();

// Return consistent error responses without exposing database details to the browser.
webApplication.UseExceptionHandler();
webApplication.UseStatusCodePages();
if (!webApplication.Environment.IsDevelopment()) webApplication.UseHttpsRedirection();
webApplication.UseAuthentication();
webApplication.UseAuthorization();
webApplication.UseRateLimiter();
webApplication.MapControllers();
webApplication.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
webApplication.Run();
