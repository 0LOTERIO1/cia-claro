using Cia.Api.DTOs;
using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Exceptions;
using Cia.Api.Repositories;
using Cia.Api.Services;

namespace Cia.Api.Tests;

public class RegionalOutageTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 19, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AdminCreatesOutage_AndCustomerFindsItByPostalCode()
    {
        using var db = TestComposition.CreateDb();
        var service = CreateService(db);

        var created = await service.CreateAsync(Guid.NewGuid(), new CreateRegionalOutageRequest
        {
            PostalCodePrefix = "01001-",
            Title = "Instabilidade regional",
            Description = "Equipe técnica atuando.",
            ExpectedResolutionAt = Now.AddHours(2).UtcDateTime
        });
        var result = await service.CheckAsync("01001-000");

        Assert.Equal("01001", created.PostalCodePrefix);
        Assert.True(result.HasOutage);
        Assert.Equal(created.Id, result.Outage?.Id);
        Assert.Contains("indisponibilidade", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MostSpecificPostalCodePrefix_Wins()
    {
        using var db = TestComposition.CreateDb();
        db.RegionalOutages.AddRange(
            CreateOutage("010", "Alerta amplo"),
            CreateOutage("01001", "Alerta específico"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.CheckAsync("01001-999");

        Assert.True(result.HasOutage);
        Assert.Equal("Alerta específico", result.Outage?.Title);
    }

    [Fact]
    public async Task PostalCodeWithoutOutage_ReturnsClearStatus()
    {
        using var db = TestComposition.CreateDb();
        db.RegionalOutages.Add(CreateOutage("010", "Alerta"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.CheckAsync("22041-001");

        Assert.False(result.HasOutage);
        Assert.Null(result.Outage);
        Assert.Contains("não identificamos", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidPostalCode_IsRejected()
    {
        using var db = TestComposition.CreateDb();
        var service = CreateService(db);

        var error = await Assert.ThrowsAsync<ValidationAppException>(() => service.CheckAsync("123"));

        Assert.Contains("CEP válido", error.Message);
    }

    [Fact]
    public async Task ResolvedOutage_IsNoLongerReturnedToCustomer()
    {
        using var db = TestComposition.CreateDb();
        var outage = CreateOutage("01310", "Falha na região");
        db.RegionalOutages.Add(outage);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.ResolveAsync(outage.Id);
        var result = await service.CheckAsync("01310-100");

        Assert.False(result.HasOutage);
        Assert.NotNull(outage.ResolvedAt);
    }

    [Fact]
    public async Task DuplicateActivePrefix_IsRejected()
    {
        using var db = TestComposition.CreateDb();
        db.RegionalOutages.Add(CreateOutage("01310", "Primeiro alerta"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(Guid.NewGuid(), new CreateRegionalOutageRequest
            {
                PostalCodePrefix = "01310",
                Title = "Segundo alerta",
                Description = "Descrição do segundo alerta."
            }));

        Assert.Contains("já existe", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RegionalOutageService CreateService(AppDbContext db)
    {
        return new RegionalOutageService(
            new RegionalOutageRepository(db),
            new FixedTimeProvider(Now));
    }

    private static RegionalOutage CreateOutage(string prefix, string title)
    {
        return new RegionalOutage
        {
            Id = Guid.NewGuid(),
            PostalCodePrefix = prefix,
            Title = title,
            Description = "Indisponibilidade de demonstração.",
            StartedAt = Now.AddMinutes(-30).UtcDateTime,
            CreatedAt = Now.AddMinutes(-30).UtcDateTime,
            CreatedByUserId = Guid.NewGuid()
        };
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
