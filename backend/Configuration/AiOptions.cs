namespace Cia.Api.Configuration;

public class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "Local";
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; set; } = 8;
    public int MaxRetries { get; set; } = 1;
    public int ShortTermMessageCount { get; set; } = 16;
    public double HighConfidenceThreshold { get; set; } = 0.72;
    public double MediumConfidenceThreshold { get; set; } = 0.42;

    public bool HasExternalKey => !string.IsNullOrWhiteSpace(ApiKey);

    public int EffectiveTimeoutSeconds => Math.Clamp(TimeoutSeconds <= 0 ? 8 : TimeoutSeconds, 3, 45);
    public int EffectiveMaxRetries => Math.Clamp(MaxRetries, 0, 2);
    public int EffectiveShortTermMessageCount => Math.Clamp(ShortTermMessageCount <= 0 ? 16 : ShortTermMessageCount, 8, 20);
    public double HighConfidence => Math.Clamp(HighConfidenceThreshold <= 0 ? 0.72 : HighConfidenceThreshold, 0.5, 0.95);
    public double MediumConfidence => Math.Clamp(MediumConfidenceThreshold <= 0 ? 0.42 : MediumConfidenceThreshold, 0.2, HighConfidence - 0.05);
}
