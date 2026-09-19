namespace Cia.Api.Entities;

public class RegionalOutage
{
    public Guid Id { get; set; }
    public string PostalCodePrefix { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? ExpectedResolutionAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
}
