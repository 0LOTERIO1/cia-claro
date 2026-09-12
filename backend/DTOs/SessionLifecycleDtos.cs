using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class SessionLifecycleResult
{
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ClosedSessionId { get; set; }
    public Guid? NewSessionId { get; set; }
    public SessionDto? Session { get; set; }
}

public class SessionLifecycleResponse
{
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ClosedSessionId { get; set; }
    public Guid? NewSessionId { get; set; }
    public ActiveSessionResponse Snapshot { get; set; } = new();
}
