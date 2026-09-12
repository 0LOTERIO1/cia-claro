using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class CustomerChannelDto
{
    public ChannelType Channel { get; set; }
    public bool Connected { get; set; }
    public string? DisplayName { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

public class TelegramLinkCodeDto
{
    public string Code { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int ExpiresInMinutes { get; set; }
}

public class ActiveSessionResponse
{
    public SessionDto? Session { get; set; }
    public IReadOnlyList<MessageDto> Messages { get; set; } = Array.Empty<MessageDto>();
    public IReadOnlyList<CustomerChannelDto> Channels { get; set; } = Array.Empty<CustomerChannelDto>();
}

public class CustomerMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

public class ChannelUnlinkDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
