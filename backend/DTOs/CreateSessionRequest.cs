using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class CreateSessionRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public ChannelType Channel { get; set; }
}
