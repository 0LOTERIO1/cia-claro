using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class SendMessageResponse
{
    public Guid SessionId { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public SessionStatus Status { get; set; }
    public IntentType DetectedIntent { get; set; }
    public ChannelType CurrentChannel { get; set; }
    public DepartmentType CurrentDepartment { get; set; }
    public DepartmentType? PreviousDepartment { get; set; }
    public bool ContextRestored { get; set; }
    public bool DepartmentChanged { get; set; }
    public string? TransferNotice { get; set; }
    public ContextDto? Context { get; set; }
    public MessageDto AssistantMessage { get; set; } = null!;
    public HandoffDto? Handoff { get; set; }
    public IReadOnlyList<MessageDto> Messages { get; set; } = Array.Empty<MessageDto>();
    public IReadOnlyList<TransferDto> Transfers { get; set; } = Array.Empty<TransferDto>();
}
