using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class SessionDto
{
    public Guid Id { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public ChannelType InitialChannel { get; set; }
    public ChannelType CurrentChannel { get; set; }
    public SessionStatus Status { get; set; }
    public IntentType DetectedIntent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool ContextRestored { get; set; }
    public ContextDto? Context { get; set; }
}
