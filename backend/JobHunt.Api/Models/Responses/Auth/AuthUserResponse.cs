namespace JobHunt.Api.Models.Responses.Auth;

public record AuthUserResponse(string Id, string Email, bool EmailConfirmed);
