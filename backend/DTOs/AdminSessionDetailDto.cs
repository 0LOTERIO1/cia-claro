namespace Cia.Api.DTOs;

public class AdminSessionDetailDto
{
    public SessionDto Session { get; set; } = null!;
    public CustomerDto Customer { get; set; } = null!;
    public ContextDto? Context { get; set; }
    public IReadOnlyList<MessageDto> Messages { get; set; } = Array.Empty<MessageDto>();
    public HandoffDto? Handoff { get; set; }
    public IReadOnlyList<TransferDto> Transfers { get; set; } = Array.Empty<TransferDto>();
}
