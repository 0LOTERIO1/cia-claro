namespace Cia.Api.Entities;

public class Customer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? TelegramUserId { get; set; }
    public long? TelegramChatId { get; set; }

    public ICollection<ConversationSession> Sessions { get; set; } = new List<ConversationSession>();
    public ICollection<CustomerChannelIdentity> ChannelIdentities { get; set; } = new List<CustomerChannelIdentity>();
    public ICollection<ChannelLinkCode> LinkCodes { get; set; } = new List<ChannelLinkCode>();
    public ICollection<ServiceRating> ServiceRatings { get; set; } = new List<ServiceRating>();
}
