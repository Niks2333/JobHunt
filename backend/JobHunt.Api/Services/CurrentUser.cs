namespace JobHunt.Api.Services;

public class CurrentUser(IHttpContextAccessor accessor)
{
    public string? UserId => accessor.HttpContext?.User.FindFirst("sub")?.Value;
    public string RequiredUserId => UserId ?? throw new InvalidOperationException("A signed-in user is required.");
}
