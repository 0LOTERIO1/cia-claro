namespace Cia.Api.DTOs;

public class HealthResponse
{
    public string Status { get; set; } = "ok";
    public string Service { get; set; } = "CIA API";
}
