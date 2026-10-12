namespace JobHunt.Api.Models.Responses.Auth;

public record AuthSessionResponse(string AccessToken, DateTime ExpiresAtUtc, AuthUserResponse User);
