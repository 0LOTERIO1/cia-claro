using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class ContextDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public IssueType IssueType { get; set; }
    public bool ModemRestarted { get; set; }
    public bool InternetStillDown { get; set; }
    public string? OriginalProblem { get; set; }
    public string? TroubleshootingPerformed { get; set; }
    public string? CurrentRequest { get; set; }
    public string? ImportantFacts { get; set; }
    public string? ContextSummary { get; set; }
    public string? AdditionalData { get; set; }
    public DateTime UpdatedAt { get; set; }
}
