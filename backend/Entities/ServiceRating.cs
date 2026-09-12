using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class ServiceRating
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public int Score { get; set; }
    public string? Comment { get; set; }
    public Guid? AgentId { get; set; }
    public ChannelType? ChannelAtCompletion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ConversationSession Session { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
