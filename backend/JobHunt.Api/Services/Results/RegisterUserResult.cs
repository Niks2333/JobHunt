using JobHunt.Api.Models.Responses.Auth;

namespace JobHunt.Api.Services.Results;

public record RegisterUserResult(AuthUserResponse? User, Dictionary<string, string[]> ValidationErrors);
