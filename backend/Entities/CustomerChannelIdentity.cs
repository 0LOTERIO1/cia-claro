using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class CustomerChannelIdentity
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public ChannelType Channel { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string? ExternalChatId { get; set; }
    public string? DisplayName { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}
