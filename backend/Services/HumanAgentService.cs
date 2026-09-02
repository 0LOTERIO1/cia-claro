using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class HumanAgentService : IHumanAgentService
{
    private readonly IHumanAgentRequestRepository _requests;
    private readonly ISessionRepository _sessions;
    private readonly IMessageRepository _messages;
    private readonly IHandoffRepository _handoffs;
    private readonly IUserRepository _users;
    private readonly ILogger<HumanAgentService> _logger;

    public HumanAgentService(
        IHumanAgentRequestRepository requests,
        ISessionRepository sessions,
        IMessageRepository messages,
        IHandoffRepository handoffs,
        IUserRepository users,
        ILogger<HumanAgentService> logger)
    {
        _requests = requests;
        _sessions = sessions;
        _messages = messages;
        _handoffs = handoffs;
        _users = users;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AgentQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var waiting = await _requests.GetByStatusAsync(HumanAgentRequestStatus.Waiting, cancellationToken);
        return waiting.Select(ToQueueItem).ToList();
    }

    public async Task<IReadOnlyList<AgentQueueItemDto>> GetAssignedAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var assigned = await _requests.GetAssignedToAgentAsync(agentId, cancellationToken);
        return assigned.Select(ToQueueItem).ToList();
    }

    public async Task<AgentSessionDetailDto> GetDetailAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requests.GetByIdAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Solicitação de atendimento humano não encontrada.");

        return await BuildDetailAsync(request, cancellationToken);
    }

    public async Task<AgentSessionDetailDto> GetDetailBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var request = await _requests.GetLatestBySessionIdAsync(sessionId, cancellationToken)
            ?? throw new NotFoundException("Esta sessão ainda não possui atendimento humano.");

        return await BuildDetailAsync(request, cancellationToken);
    }

    public async Task<AgentSessionDetailDto> AssumeAsync(Guid requestId, Guid agentId, CancellationToken cancellationToken = default)
    {
        var request = await _requests.GetByIdAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Solicitação de atendimento humano não encontrada.");

        if (request.Status == HumanAgentRequestStatus.Finished)
        {
            throw new ConflictException("Este atendimento já foi encerrado.");
        }

        if (request.Status == HumanAgentRequestStatus.Assigned && request.AssignedAgentId != agentId)
        {
            throw new ConflictException("Este atendimento já foi assumido por outro funcionário.");
        }

        var agent = await _users.GetByIdAsync(agentId, cancellationToken)
            ?? throw new NotFoundException("Funcionário não encontrado.");

        if (request.Status == HumanAgentRequestStatus.Waiting)
        {
            request.Status = HumanAgentRequestStatus.Assigned;
            request.AssignedAgentId = agent.Id;
            request.AssignedAt = DateTime.UtcNow;

            var session = request.Session;
            session.Status = SessionStatus.Transferred;
            session.UpdatedAt = DateTime.UtcNow;

            var welcome = BuildWelcomeMessage(session.Customer.Name, session.Context);
            await _messages.AddAsync(new Message
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Sender = MessageSender.HumanAgent,
                Channel = session.CurrentChannel,
                Content = welcome,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _sessions.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Human agent assumed request. Protocol={Protocol} Agent={Agent}",
                session.Protocol, agent.Name);
        }

        return await GetDetailAsync(request.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<MessageDto>> SendMessageAsync(
        Guid sessionId,
        Guid agentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationAppException("A mensagem não pode ser vazia.");
        }

        if (content.Trim().Length > 2000)
        {
            throw new ValidationAppException("A mensagem excede o tamanho máximo de 2000 caracteres.");
        }

        var request = await _requests.GetOpenBySessionIdAsync(sessionId, cancellationToken)
            ?? throw new NotFoundException("Não há atendimento humano aberto para esta sessão.");

        if (request.Status != HumanAgentRequestStatus.Assigned)
        {
            throw new ConflictException("Assuma o atendimento antes de enviar mensagens.");
        }

        if (request.AssignedAgentId != agentId)
        {
            throw new ConflictException("Somente o funcionário responsável pode enviar mensagens neste protocolo.");
        }

        var session = request.Session;
        await _messages.AddAsync(new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Sender = MessageSender.HumanAgent,
            Channel = session.CurrentChannel,
            Content = content.Trim(),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.SaveChangesAsync(cancellationToken);

        var history = await _messages.GetBySessionIdAsync(session.Id, cancellationToken);
        return history.Select(m => m.ToDto()).ToList();
    }

    public async Task<AgentSessionDetailDto> FinishAsync(Guid requestId, Guid agentId, CancellationToken cancellationToken = default)
    {
        var request = await _requests.GetByIdAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Solicitação de atendimento humano não encontrada.");

        if (request.AssignedAgentId != agentId)
        {
            throw new ConflictException("Somente o funcionário responsável pode encerrar este atendimento.");
        }

        request.Status = HumanAgentRequestStatus.Finished;
        request.FinishedAt = DateTime.UtcNow;
        request.Session.Status = SessionStatus.Resolved;
        request.Session.UpdatedAt = DateTime.UtcNow;
        await _sessions.SaveChangesAsync(cancellationToken);

        return await GetDetailAsync(request.Id, cancellationToken);
    }

    private async Task<AgentSessionDetailDto> BuildDetailAsync(HumanAgentRequest request, CancellationToken cancellationToken)
    {
        var messages = await _messages.GetBySessionIdAsync(request.SessionId, cancellationToken);
        var handoff = await _handoffs.GetLatestBySessionIdAsync(request.SessionId, cancellationToken);

        return new AgentSessionDetailDto
        {
            Request = ToQueueItem(request),
            Session = request.Session.ToDto(),
            Customer = request.Session.Customer.ToDto(),
            Context = request.Session.Context?.ToDto(),
            Messages = messages.Select(m => m.ToDto()).ToList(),
            Transfers = (request.Session.Transfers ?? Array.Empty<DepartmentTransfer>())
                .OrderBy(t => t.CreatedAt)
                .Select(t => t.ToDto())
                .ToList(),
            Handoff = handoff?.ToDto()
        };
    }

    private static AgentQueueItemDto ToQueueItem(HumanAgentRequest request)
    {
        var context = request.Session.Context;
        return new AgentQueueItemDto
        {
            RequestId = request.Id,
            SessionId = request.SessionId,
            Protocol = request.Session.Protocol,
            CustomerName = request.Session.Customer?.Name ?? string.Empty,
            CustomerId = request.Session.CustomerId,
            Problem = context?.OriginalProblem
                ?? (context?.IssueType == IssueType.InternetConnection
                    ? "Internet residencial sem conexão"
                    : "Atendimento em andamento"),
            ContextFacts = BuildFacts(context),
            ContextSummary = context?.ContextSummary,
            Status = request.Status,
            CreatedAt = request.CreatedAt
        };
    }

    private static IReadOnlyList<string> BuildFacts(ConversationContext? context)
    {
        if (context is null)
        {
            return Array.Empty<string>();
        }

        var facts = new List<string>();
        if (context.ModemRestarted)
        {
            facts.Add("Cliente reiniciou modem");
        }

        if (context.InternetStillDown)
        {
            facts.Add("Problema persiste");
        }

        if (!string.IsNullOrWhiteSpace(context.CurrentRequest))
        {
            facts.Add(context.CurrentRequest.Trim());
        }

        if (!string.IsNullOrWhiteSpace(context.ImportantFacts))
        {
            foreach (var line in context.ImportantFacts.Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var clean = line.TrimStart('-', '*', ' ').Trim();
                if (!string.IsNullOrWhiteSpace(clean) &&
                    !facts.Any(f => f.Equals(clean, StringComparison.OrdinalIgnoreCase)))
                {
                    facts.Add(clean);
                }
            }
        }

        return facts;
    }

    private static string BuildWelcomeMessage(string customerName, ConversationContext? context)
    {
        var facts = BuildFacts(context);
        var history = facts.Count == 0
            ? "Vi o histórico do seu atendimento e vou continuar daqui."
            : string.Join(" ", facts.Take(3).Select(f => f.TrimEnd('.') + "."));

        return $"Olá {customerName}, vi seu histórico. {history} Vou continuar seu atendimento.";
    }
}
