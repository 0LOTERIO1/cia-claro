using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Cia.Api.Repositories;
using Cia.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cia.Api.Tests;

internal static class TestComposition
{
    public static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Customers.Add(new Customer
        {
            Id = DbSeeder.DemoCustomerId,
            Name = "Lucas",
            Phone = "11999999999",
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        return db;
    }

    public static (ConversationService Conversation, HandoffService Handoff, AppDbContext Db) CreateServices(AppDbContext db)
    {
        ICustomerRepository customers = new CustomerRepository(db);
        ISessionRepository sessions = new SessionRepository(db);
        IMessageRepository messages = new MessageRepository(db);
        IContextRepository contexts = new ContextRepository(db);
        IHandoffRepository handoffs = new HandoffRepository(db);
        IIntentService intent = new IntentService();
        IContextService contextService = new ContextService(contexts, sessions, NullLogger<ContextService>.Instance);
        IAiProvider provider = new LocalFallbackAiProvider(intent);
        IAiService ai = new AiService(
            provider,
            Microsoft.Extensions.Options.Options.Create(new Cia.Api.Configuration.AiOptions()),
            NullLogger<AiService>.Instance);
        IProtocolService protocol = new ProtocolService(sessions);
        var handoff = new HandoffService(
            sessions,
            messages,
            contextService,
            handoffs,
            ai,
            NullLogger<HandoffService>.Instance);
        var conversation = new ConversationService(
            customers,
            sessions,
            messages,
            contextService,
            intent,
            ai,
            handoff,
            protocol,
            NullLogger<ConversationService>.Instance);

        return (conversation, handoff, db);
    }
}
