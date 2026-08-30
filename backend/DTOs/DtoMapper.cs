using Cia.Api.Entities;

namespace Cia.Api.DTOs;

public static class DtoMapper
{
    public static CustomerDto ToDto(this Customer customer) => new()
    {
        Id = customer.Id,
        Name = customer.Name,
        Phone = customer.Phone,
        CreatedAt = customer.CreatedAt
    };

    public static SessionDto ToDto(this ConversationSession session, bool contextRestored = false) => new()
    {
        Id = session.Id,
        Protocol = session.Protocol,
        CustomerId = session.CustomerId,
        CustomerName = session.Customer?.Name ?? string.Empty,
        InitialChannel = session.InitialChannel,
        CurrentChannel = session.CurrentChannel,
        Status = session.Status,
        DetectedIntent = session.DetectedIntent,
        CreatedAt = session.CreatedAt,
        UpdatedAt = session.UpdatedAt,
        ContextRestored = contextRestored,
        Context = session.Context?.ToDto()
    };

    public static MessageDto ToDto(this Message message) => new()
    {
        Id = message.Id,
        SessionId = message.SessionId,
        Sender = message.Sender,
        Channel = message.Channel,
        Content = message.Content,
        CreatedAt = message.CreatedAt
    };

    public static ContextDto ToDto(this ConversationContext context) => new()
    {
        Id = context.Id,
        SessionId = context.SessionId,
        IssueType = context.IssueType,
        ModemRestarted = context.ModemRestarted,
        AdditionalData = context.AdditionalData,
        UpdatedAt = context.UpdatedAt
    };

    public static HandoffDto ToDto(this Handoff handoff) => new()
    {
        Id = handoff.Id,
        SessionId = handoff.SessionId,
        Summary = handoff.Summary,
        Status = handoff.Status,
        CreatedAt = handoff.CreatedAt
    };
}
