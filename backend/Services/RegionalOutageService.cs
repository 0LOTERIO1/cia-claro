using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class RegionalOutageService : IRegionalOutageService
{
    private const int MinPrefixLength = 3;
    private const int PostalCodeLength = 8;
    private const int MaxTitleLength = 120;
    private const int MaxDescriptionLength = 500;

    private readonly IRegionalOutageRepository _outages;
    private readonly TimeProvider _timeProvider;

    public RegionalOutageService(IRegionalOutageRepository outages, TimeProvider timeProvider)
    {
        _outages = outages;
        _timeProvider = timeProvider;
    }

    public async Task<RegionalOutageCheckResponse> CheckAsync(
        string postalCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedPostalCode = NormalizeDigits(postalCode);
        if (normalizedPostalCode.Length != PostalCodeLength)
        {
            throw new ValidationAppException("Informe um CEP válido com 8 dígitos.");
        }

        var now = UtcNow;
        var activeOutages = await _outages.ListActiveAsync(cancellationToken);
        var match = activeOutages
            .Where(outage =>
                outage.StartedAt <= now &&
                normalizedPostalCode.StartsWith(outage.PostalCodePrefix, StringComparison.Ordinal))
            .OrderByDescending(outage => outage.PostalCodePrefix.Length)
            .ThenByDescending(outage => outage.StartedAt)
            .FirstOrDefault();

        if (match is null)
        {
            return new RegionalOutageCheckResponse
            {
                PostalCode = normalizedPostalCode,
                HasOutage = false,
                Message = "Não identificamos indisponibilidade regional cadastrada para este CEP."
            };
        }

        return new RegionalOutageCheckResponse
        {
            PostalCode = normalizedPostalCode,
            HasOutage = true,
            Message = match.ExpectedResolutionAt is null
                ? "Identificamos uma indisponibilidade regional. Nossa equipe já está acompanhando."
                : $"Identificamos uma indisponibilidade regional. Previsão de normalização: {match.ExpectedResolutionAt:dd/MM/yyyy HH:mm} UTC.",
            Outage = match.ToDto()
        };
    }

    public async Task<IReadOnlyList<RegionalOutageDto>> ListAsync(
        bool includeResolved,
        CancellationToken cancellationToken = default)
    {
        var outages = await _outages.ListAsync(includeResolved, cancellationToken);
        return outages.Select(outage => outage.ToDto()).ToList();
    }

    public async Task<RegionalOutageDto> CreateAsync(
        Guid adminUserId,
        CreateRegionalOutageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId == Guid.Empty)
        {
            throw new ValidationAppException("Administrador inválido.");
        }

        var prefix = NormalizeDigits(request.PostalCodePrefix);
        if (prefix.Length is < MinPrefixLength or > PostalCodeLength)
        {
            throw new ValidationAppException("O prefixo do CEP deve ter entre 3 e 8 dígitos.");
        }

        var title = NormalizeRequired(request.Title, "O título", MaxTitleLength);
        var description = NormalizeRequired(request.Description, "A descrição", MaxDescriptionLength);
        var startedAt = NormalizeUtc(request.StartedAt ?? UtcNow);
        DateTime? expectedResolutionAt = request.ExpectedResolutionAt is null
            ? null
            : NormalizeUtc(request.ExpectedResolutionAt.Value);

        if (expectedResolutionAt <= startedAt)
        {
            throw new ValidationAppException("A previsão de normalização deve ser posterior ao início.");
        }

        var existing = await _outages.ListActiveAsync(cancellationToken);
        if (existing.Any(outage => outage.PostalCodePrefix == prefix))
        {
            throw new ConflictException("Já existe uma indisponibilidade ativa para este prefixo de CEP.");
        }

        var outage = new RegionalOutage
        {
            Id = Guid.NewGuid(),
            PostalCodePrefix = prefix,
            Title = title,
            Description = description,
            StartedAt = startedAt,
            ExpectedResolutionAt = expectedResolutionAt,
            CreatedAt = UtcNow,
            CreatedByUserId = adminUserId
        };

        await _outages.AddAsync(outage, cancellationToken);
        await _outages.SaveChangesAsync(cancellationToken);
        return outage.ToDto();
    }

    public async Task<RegionalOutageDto> ResolveAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var outage = await _outages.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Indisponibilidade regional não encontrada.");

        if (outage.ResolvedAt is not null)
        {
            throw new ConflictException("Esta indisponibilidade já foi encerrada.");
        }

        outage.ResolvedAt = UtcNow;
        await _outages.SaveChangesAsync(cancellationToken);
        return outage.ToDto();
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private static string NormalizeDigits(string value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    private static string NormalizeRequired(string value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new ValidationAppException($"{fieldName} é obrigatório.");
        }

        if (normalized.Length > maxLength)
        {
            throw new ValidationAppException($"{fieldName} pode ter no máximo {maxLength} caracteres.");
        }

        return normalized;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
