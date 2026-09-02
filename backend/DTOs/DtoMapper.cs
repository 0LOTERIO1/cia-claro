using Cia.Api.Entities;
using Cia.Api.Enums;

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

    public static SessionDto ToDto(this ConversationSession session, bool contextRestored = false, bool departmentChanged = false) => new()
    {
        Id = session.Id,
        Protocol = session.Protocol,
        CustomerId = session.CustomerId,
        CustomerName = session.Customer?.Name ?? string.Empty,
        InitialChannel = session.InitialChannel,
        CurrentChannel = session.CurrentChannel,
        CurrentDepartment = session.CurrentDepartment,
        PreviousDepartment = session.PreviousDepartment,
        Status = session.Status,
        DetectedIntent = session.DetectedIntent,
        CreatedAt = session.CreatedAt,
        UpdatedAt = session.UpdatedAt,
        ContextRestored = contextRestored,
        DepartmentChanged = departmentChanged,
        Context = session.Context?.ToDto(),
        HumanRequestStatus = session.HumanAgentRequests?
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => (HumanAgentRequestStatus?)r.Status)
            .FirstOrDefault(),
        Transfers = (session.Transfers ?? Array.Empty<DepartmentTransfer>())
            .OrderBy(t => t.CreatedAt)
            .Select(t => t.ToDto())
            .ToList()
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
        InternetStillDown = context.InternetStillDown,
        OriginalProblem = context.OriginalProblem,
        TroubleshootingPerformed = context.TroubleshootingPerformed,
        CurrentRequest = context.CurrentRequest,
        ImportantFacts = context.ImportantFacts,
        ContextSummary = context.ContextSummary,
        AdditionalData = context.AdditionalData,
        UpdatedAt = context.UpdatedAt
    };

    public static TransferDto ToDto(this DepartmentTransfer transfer) => new()
    {
        Id = transfer.Id,
        SessionId = transfer.SessionId,
        FromDepartment = transfer.FromDepartment,
        ToDepartment = transfer.ToDepartment,
        Reason = transfer.Reason,
        CreatedAt = transfer.CreatedAt
    };

    public static HandoffDto ToDto(this Handoff handoff) => new()
    {
        Id = handoff.Id,
        SessionId = handoff.SessionId,
        Summary = handoff.Summary,
        Status = handoff.Status,
        CreatedAt = handoff.CreatedAt
    };

    public static UserDto ToDto(this User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        Role = user.Role,
        CustomerId = user.CustomerId
    };

    public static HumanAgentRequestDto ToDto(this HumanAgentRequest request) => new()
    {
        Id = request.Id,
        SessionId = request.SessionId,
        Status = request.Status,
        AssignedAgentId = request.AssignedAgentId,
        AssignedAgentName = request.AssignedAgent?.Name,
        CreatedAt = request.CreatedAt,
        AssignedAt = request.AssignedAt,
        FinishedAt = request.FinishedAt
    };
}
