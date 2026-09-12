using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class DashboardService : IDashboardService
{
    private readonly ISessionRepository _sessions;
    private readonly IMessageRepository _messages;
    private readonly IHandoffRepository _handoffs;
    private readonly IChannelIdentityRepository _identities;
    private readonly IServiceRatingRepository _ratings;

    public DashboardService(
        ISessionRepository sessions,
        IMessageRepository messages,
        IHandoffRepository handoffs,
        IChannelIdentityRepository identities,
        IServiceRatingRepository ratings)
    {
        _sessions = sessions;
        _messages = messages;
        _handoffs = handoffs;
        _identities = identities;
        _ratings = ratings;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await _sessions.GetAllAsync(cancellationToken);
        var ratings = await _ratings.GetAllAsync(cancellationToken);
        var resolvedCount = sessions.Count(s => s.Status == SessionStatus.Resolved);
        var ratedCount = ratings.Count;
        var rateableCount = sessions.Count(s =>
            s.Status == SessionStatus.Resolved && SessionRules.IsCompletedClosure(s));

        return new DashboardDto
        {
            TotalSessions = sessions.Count,
            ActiveSessions = sessions.Count(s => s.Status == SessionStatus.Active),
            ResolvedSessions = resolvedCount,
            TransferredSessions = sessions.Count(s =>
                s.Status is SessionStatus.Transferred or SessionStatus.WaitingForAgent),
            SessionsByChannel = Enum.GetValues<ChannelType>()
                .Select(channel => new ChannelCountDto
                {
                    Channel = channel,
                    Count = sessions.Count(s => s.CurrentChannel == channel)
                })
                .ToList(),
            SessionsByDepartment = Enum.GetValues<DepartmentType>()
                .Select(department => new DepartmentCountDto
                {
                    Department = department,
                    Count = sessions.Count(s => s.CurrentDepartment == department)
                })
                .ToList(),
            AverageScore = ratedCount == 0
                ? null
                : Math.Round((decimal)ratings.Average(r => r.Score), 1, MidpointRounding.AwayFromZero),
            RatedSessions = ratedCount,
            RatingRate = rateableCount == 0
                ? 0
                : Math.Round((decimal)ratedCount * 100m / rateableCount, 0, MidpointRounding.AwayFromZero),
            ScoreDistribution = Enumerable.Range(1, 5)
                .Select(score => new StarCountDto
                {
                    Score = score,
                    Count = ratings.Count(r => r.Score == score)
                })
                .ToList()
        };
    }

    public async Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await _sessions.GetAllAsync(cancellationToken);
        return sessions.Select(s => s.ToDto()).ToList();
    }

    public async Task<AdminSessionDetailDto> GetSessionDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Sessão não encontrada.");

        var messages = await _messages.GetBySessionIdAsync(id, cancellationToken);
        var handoff = await _handoffs.GetLatestBySessionIdAsync(id, cancellationToken);
        var identities = await _identities.GetByCustomerIdAsync(session.CustomerId, cancellationToken);

        return new AdminSessionDetailDto
        {
            Session = session.ToDto(),
            Customer = session.Customer.ToDto(),
            Context = session.Context?.ToDto(),
            Messages = messages.Select(m => m.ToDto()).ToList(),
            Handoff = handoff?.ToDto(),
            Transfers = (session.Transfers ?? Array.Empty<DepartmentTransfer>())
                .OrderBy(t => t.CreatedAt)
                .Select(t => t.ToDto())
                .ToList(),
            LinkedChannels = identities.Select(identity => new CustomerChannelDto
            {
                Channel = identity.Channel,
                Connected = true,
                DisplayName = identity.DisplayName,
                VerifiedAt = identity.VerifiedAt ?? identity.CreatedAt
            }).ToList(),
            Rating = session.Rating?.ToDto()
        };
    }
}
