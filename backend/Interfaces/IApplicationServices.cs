using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;

namespace Cia.Api.Interfaces;

public interface IConversationService
{
    Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default);
    Task<SessionDto> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken = default);
    Task<SessionDto> GetSessionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionDto>> GetSessionsByCustomerAsync(string customerId, CancellationToken cancellationToken = default);
    Task<SessionDto> ChangeChannelAsync(Guid sessionId, ChannelType channel, CancellationToken cancellationToken = default);
    Task<SessionDto> ChangeDepartmentAsync(Guid sessionId, DepartmentType department, string? reason, CancellationToken cancellationToken = default);
}

public interface IContextService
{
    Task<ConversationContext> GetOrCreateAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ConversationContext> UpdateFromIntentAsync(ConversationContext context, IntentType intent, string message, CancellationToken cancellationToken = default);
}

public interface IIntentService
{
    IntentType Detect(string message);
}

public interface IAiService
{
    Task<string> GenerateResponseAsync(
        string message,
        IntentType intent,
        ConversationContext context,
        Customer customer,
        ConversationSession session,
        CancellationToken cancellationToken = default);

    Task<string> GenerateHandoffSummaryAsync(
        Customer customer,
        ConversationSession session,
        ConversationContext context,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default);
}

public interface IAiProvider
{
    Task<IntentType> AnalyzeIntentAsync(string message, CancellationToken cancellationToken = default);

    Task<string> GenerateResponseAsync(
        string message,
        IntentType intent,
        ConversationContext context,
        Customer customer,
        ConversationSession session,
        CancellationToken cancellationToken = default);

    Task<string> GenerateHandoffSummaryAsync(
        Customer customer,
        ConversationSession session,
        ConversationContext context,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default);
}

public interface IHandoffService
{
    Task<HandoffDto> CreateHandoffAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> GetMeAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IHumanAgentService
{
    Task<IReadOnlyList<AgentQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentQueueItemDto>> GetAssignedAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<AgentSessionDetailDto> GetDetailAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<AgentSessionDetailDto> GetDetailBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<AgentSessionDetailDto> AssumeAsync(Guid requestId, Guid agentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageDto>> SendMessageAsync(Guid sessionId, Guid agentId, string content, CancellationToken cancellationToken = default);
    Task<AgentSessionDetailDto> FinishAsync(Guid requestId, Guid agentId, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<AdminSessionDetailDto> GetSessionDetailAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IProtocolService
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}

public class RoutingDecision
{
    public DepartmentType Current { get; set; }
    public DepartmentType? Previous { get; set; }
    public bool Transferred { get; set; }
    public string? Reason { get; set; }
    public DepartmentTransfer? Transfer { get; set; }
}

public interface IOrchestrationService
{
    Task<RoutingDecision> RouteAsync(
        ConversationSession session,
        IntentType intent,
        ConversationContext context,
        CancellationToken cancellationToken = default);

    Task<RoutingDecision> ChangeDepartmentAsync(
        ConversationSession session,
        DepartmentType target,
        string reason,
        CancellationToken cancellationToken = default);
}
