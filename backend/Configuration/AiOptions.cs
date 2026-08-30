namespace Cia.Api.Configuration;

public class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "Local";
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "gpt-4o-mini";

    public bool HasExternalKey => !string.IsNullOrWhiteSpace(ApiKey);
}
