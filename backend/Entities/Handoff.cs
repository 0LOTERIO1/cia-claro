using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class Handoff
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public HandoffStatus Status { get; set; } = HandoffStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ConversationSession Session { get; set; } = null!;
}
