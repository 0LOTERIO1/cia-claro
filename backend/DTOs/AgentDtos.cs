using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? CustomerId { get; set; }
}

public class AgentQueueItemDto
{
    public Guid RequestId { get; set; }
    public Guid SessionId { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public IReadOnlyList<string> ContextFacts { get; set; } = Array.Empty<string>();
    public string? ContextSummary { get; set; }
    public HumanAgentRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class HumanAgentRequestDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public HumanAgentRequestStatus Status { get; set; }
    public Guid? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public class AgentSessionDetailDto
{
    public AgentQueueItemDto Request { get; set; } = null!;
    public SessionDto Session { get; set; } = null!;
    public CustomerDto Customer { get; set; } = null!;
    public ContextDto? Context { get; set; }
    public IReadOnlyList<MessageDto> Messages { get; set; } = Array.Empty<MessageDto>();
    public IReadOnlyList<TransferDto> Transfers { get; set; } = Array.Empty<TransferDto>();
    public HandoffDto? Handoff { get; set; }
}

public class AgentMessageRequest
{
    public string Content { get; set; } = string.Empty;
}
