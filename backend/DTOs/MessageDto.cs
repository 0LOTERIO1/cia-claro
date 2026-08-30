using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public MessageSender Sender { get; set; }
    public ChannelType Channel { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
