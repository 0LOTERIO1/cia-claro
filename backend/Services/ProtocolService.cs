using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class ProtocolService : IProtocolService
{
    private readonly ISessionRepository _sessions;

    public ProtocolService(ISessionRepository sessions)
    {
        _sessions = sessions;
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var count = await _sessions.CountCreatedOnDateAsync(today, cancellationToken);
        return $"CIA-{today:yyyyMMdd}-{(count + 1):0000}";
    }
}
