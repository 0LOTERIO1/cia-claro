namespace Cia.Api.DTOs;

public class SubmitServiceRatingRequest
{
    public int Score { get; set; }
    public string? Comment { get; set; }
}

public class ServiceRatingDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmitServiceRatingResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ServiceRatingDto? Rating { get; set; }
}

public class StarCountDto
{
    public int Score { get; set; }
    public int Count { get; set; }
}
