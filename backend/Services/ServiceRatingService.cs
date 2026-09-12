using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Services;

public class ServiceRatingService : IServiceRatingService
{
    public const int MaxCommentLength = 1000;

    private readonly ISessionRepository _sessions;
    private readonly IServiceRatingRepository _ratings;

    public ServiceRatingService(ISessionRepository sessions, IServiceRatingRepository ratings)
    {
        _sessions = sessions;
        _ratings = ratings;
    }

    public async Task<SubmitServiceRatingResponse> SubmitAsync(
        string customerId,
        Guid sessionId,
        SubmitServiceRatingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Score is < 1 or > 5)
        {
            throw new ValidationAppException("A nota deve ser um valor inteiro entre 1 e 5.");
        }

        var comment = NormalizeComment(request.Comment);

        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new NotFoundException("Sessão não encontrada.");

        if (!string.Equals(session.CustomerId, customerId, StringComparison.Ordinal))
        {
            throw new ValidationAppException("Você não pode avaliar este atendimento.");
        }

        if (session.Status != SessionStatus.Resolved)
        {
            throw new ValidationAppException("Só é possível avaliar um atendimento finalizado.");
        }

        if (session.Rating is not null || await _ratings.GetBySessionIdAsync(sessionId, cancellationToken) is not null)
        {
            throw new ConflictException("Este atendimento já foi avaliado.");
        }

        var rating = new ServiceRating
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            CustomerId = session.CustomerId,
            Score = request.Score,
            Comment = comment,
            AgentId = session.HumanAgentRequests?
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.AssignedAgentId)
                .FirstOrDefault(id => id.HasValue),
            ChannelAtCompletion = session.CurrentChannel,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _ratings.AddAsync(rating, cancellationToken);
            await _ratings.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Este atendimento já foi avaliado.");
        }

        return new SubmitServiceRatingResponse
        {
            Success = true,
            Message = "Obrigado pela sua avaliação.",
            Rating = rating.ToDto()
        };
    }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        var trimmed = comment.Trim();
        if (trimmed.Length > MaxCommentLength)
        {
            throw new ValidationAppException($"O comentário pode ter no máximo {MaxCommentLength} caracteres.");
        }

        return trimmed;
    }
}
