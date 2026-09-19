namespace Cia.Api.DTOs;

public class CreateRegionalOutageRequest
{
    public string PostalCodePrefix { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? ExpectedResolutionAt { get; set; }
}

public class RegionalOutageDto
{
    public Guid Id { get; set; }
    public string PostalCodePrefix { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? ExpectedResolutionAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Active { get; set; }
}

public class RegionalOutageCheckResponse
{
    public string PostalCode { get; set; } = string.Empty;
    public bool HasOutage { get; set; }
    public string Message { get; set; } = string.Empty;
    public RegionalOutageDto? Outage { get; set; }
}
