using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class ConversationContext
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public IssueType IssueType { get; set; } = IssueType.None;
    public bool ModemRestarted { get; set; }
    public string? AdditionalData { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ConversationSession Session { get; set; } = null!;
}
