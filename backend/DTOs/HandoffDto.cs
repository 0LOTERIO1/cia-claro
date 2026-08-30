using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class HandoffDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public HandoffStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
