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

    public static (ConversationService Conversation, HandoffService Handoff, HumanAgentService HumanAgent, AppDbContext Db) CreateServices(AppDbContext db)
    {
        ICustomerRepository customers = new CustomerRepository(db);
        ISessionRepository sessions = new SessionRepository(db);
        IMessageRepository messages = new MessageRepository(db);
        IContextRepository contexts = new ContextRepository(db);
        IHandoffRepository handoffs = new HandoffRepository(db);
        IHumanAgentRequestRepository humanRequests = new HumanAgentRequestRepository(db);
        IUserRepository users = new UserRepository(db);
        ITransferRepository transfers = new TransferRepository(db);
        IIntentService intent = new IntentService();
        IContextService contextService = new ContextService(contexts, sessions, NullLogger<ContextService>.Instance);
        IOrchestrationService orchestration = new OrchestrationService(transfers, sessions, NullLogger<OrchestrationService>.Instance);
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
            humanRequests,
            ai,
            NullLogger<HandoffService>.Instance);
        var humanAgent = new HumanAgentService(
            humanRequests,
            sessions,
            messages,
            handoffs,
            users,
            NullLogger<HumanAgentService>.Instance);
        var conversation = new ConversationService(
            customers,
            sessions,
            messages,
            contextService,
            intent,
            ai,
            handoff,
            protocol,
            orchestration,
            NullLogger<ConversationService>.Instance);

        return (conversation, handoff, humanAgent, db);
    }

    public static User SeedAgent(AppDbContext db, string name = "Ana Souza")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = $"{Guid.NewGuid():N}@claro.com",
            PasswordHash = PasswordProtector.Hash("Claro@123"),
            Role = UserRole.Agent,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }
}
