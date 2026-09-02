using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class HumanAgentRequest
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public HumanAgentRequestStatus Status { get; set; } = HumanAgentRequestStatus.Waiting;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? AssignedAgentId { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public ConversationSession Session { get; set; } = null!;
    public User? AssignedAgent { get; set; }
}
