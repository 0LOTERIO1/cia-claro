using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class ChannelLinkCode
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public ChannelType Channel { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = null!;
}
