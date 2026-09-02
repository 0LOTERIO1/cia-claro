using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class ConversationSession
{
    public Guid Id { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public ChannelType InitialChannel { get; set; }
    public ChannelType CurrentChannel { get; set; }
    public DepartmentType CurrentDepartment { get; set; } = DepartmentType.Triage;
    public DepartmentType? PreviousDepartment { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    public IntentType DetectedIntent { get; set; } = IntentType.Unknown;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
    public ConversationContext? Context { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<Handoff> Handoffs { get; set; } = new List<Handoff>();
    public ICollection<DepartmentTransfer> Transfers { get; set; } = new List<DepartmentTransfer>();
    public ICollection<HumanAgentRequest> HumanAgentRequests { get; set; } = new List<HumanAgentRequest>();
}
