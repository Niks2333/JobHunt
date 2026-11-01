using System.ComponentModel.DataAnnotations;

namespace JobHunt.Api.Models.Requests.Assistant;

public class AskAssistantRequest
{
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Question { get; set; } = string.Empty;
}
