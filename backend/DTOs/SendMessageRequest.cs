using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class SendMessageRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public ChannelType Channel { get; set; }
    public string Content { get; set; } = string.Empty;
}
