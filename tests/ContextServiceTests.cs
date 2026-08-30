using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Repositories;
using Cia.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cia.Api.Tests;

public class ContextServiceTests
{
    [Fact]
    public async Task UpdateFromIntent_StoresInternetIssueAndModemRestart()
    {
        using var db = TestComposition.CreateDb();
        var sessions = new SessionRepository(db);
        var contexts = new ContextRepository(db);
        var service = new ContextService(contexts, sessions, NullLogger<ContextService>.Instance);

        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            Protocol = "CIA-20260827-0001",
            CustomerId = DbSeeder.DemoCustomerId,
            InitialChannel = ChannelType.AppClaro,
            CurrentChannel = ChannelType.AppClaro,
            Status = SessionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await sessions.AddAsync(session);
        await sessions.SaveChangesAsync();

        var context = await service.GetOrCreateAsync(session.Id);
        context = await service.UpdateFromIntentAsync(context, IntentType.InternetProblem, "Minha internet não está funcionando.");
        Assert.Equal(IssueType.InternetConnection, context.IssueType);
        Assert.False(context.ModemRestarted);

        context = await service.UpdateFromIntentAsync(context, IntentType.ModemRestarted, "Já reiniciei o modem.");
        Assert.True(context.ModemRestarted);
        Assert.Equal(IssueType.InternetConnection, db.ConversationContexts.Single().IssueType);
        Assert.True(db.ConversationContexts.Single().ModemRestarted);
    }
}
