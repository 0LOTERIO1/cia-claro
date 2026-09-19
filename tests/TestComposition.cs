using Cia.Api.Configuration;
using Cia.Api.Data;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Cia.Api.Repositories;
using Cia.Api.Services;
using Cia.Api.Services.Knowledge;
using Cia.Api.Services.Understanding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cia.Api.Tests;

internal static class TestComposition
{
    public static readonly string TwoFactorEncryptionKey =
        Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

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

    public static (ConversationService Conversation, HandoffService Handoff, HumanAgentService HumanAgent, AppDbContext Db) CreateServices(
        AppDbContext db,
        ITelegramService? telegram = null,
        IAiProvider? understandingProvider = null)
    {
        ICustomerRepository customers = new CustomerRepository(db);
        ISessionRepository sessions = new SessionRepository(db);
        IMessageRepository messages = new MessageRepository(db);
        IContextRepository contexts = new ContextRepository(db);
        IHandoffRepository handoffs = new HandoffRepository(db);
        IHumanAgentRequestRepository humanRequests = new HumanAgentRequestRepository(db);
        IUserRepository users = new UserRepository(db);
        ITransferRepository transfers = new TransferRepository(db);
        IChannelIdentityRepository identities = new ChannelIdentityRepository(db);
        IChannelLinkCodeRepository codes = new ChannelLinkCodeRepository(db);
        var identityService = new ChannelIdentityService(
            customers,
            sessions,
            messages,
            identities,
            codes,
            NullLogger<ChannelIdentityService>.Instance);
        IIntentService intent = new IntentService();
        IContextService contextService = new ContextService(contexts, sessions, NullLogger<ContextService>.Instance);
        IOrchestrationService orchestration = new OrchestrationService(transfers, sessions, NullLogger<OrchestrationService>.Instance);
        var fallbackProvider = new LocalFallbackAiProvider(intent);
        var aiOptions = Options.Create(new Cia.Api.Configuration.AiOptions());
        var knowledge = new LocalKnowledgeService();
        var promptSecurity = new PromptSecurityService();
        var guardrails = new ConversationGuardrails(knowledge, aiOptions, promptSecurity);
        IAiService ai = new AiService(
            fallbackProvider,
            fallbackProvider,
            knowledge,
            promptSecurity,
            new SensitiveDataRedactor(),
            aiOptions,
            NullLogger<AiService>.Instance);
        IConversationUnderstandingService understanding = new ConversationUnderstandingService(
            understandingProvider ?? fallbackProvider,
            fallbackProvider,
            guardrails,
            promptSecurity,
            NullLogger<ConversationUnderstandingService>.Instance);
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
            telegram ?? new FakeTelegramService(),
            identityService,
            NullLogger<HumanAgentService>.Instance);
        var conversation = new ConversationService(
            customers,
            sessions,
            messages,
            contextService,
            understanding,
            ai,
            handoff,
            protocol,
            orchestration,
            new SensitiveDataRedactor(),
            aiOptions,
            NullLogger<ConversationService>.Instance);

        return (conversation, handoff, humanAgent, db);
    }

    public static ConversationUnderstandingService CreateUnderstanding(IAiProvider? provider = null)
    {
        var fallback = provider as LocalFallbackAiProvider ?? new LocalFallbackAiProvider(new IntentService());
        var aiOptions = Options.Create(new Cia.Api.Configuration.AiOptions());
        var promptSecurity = new PromptSecurityService();
        return new ConversationUnderstandingService(
            provider ?? fallback,
            fallback,
            new ConversationGuardrails(new LocalKnowledgeService(), aiOptions, promptSecurity),
            promptSecurity,
            NullLogger<ConversationUnderstandingService>.Instance);
    }

    public static ChannelIdentityService CreateIdentities(AppDbContext db)
    {
        return new ChannelIdentityService(
            new CustomerRepository(db),
            new SessionRepository(db),
            new MessageRepository(db),
            new ChannelIdentityRepository(db),
            new ChannelLinkCodeRepository(db),
            NullLogger<ChannelIdentityService>.Instance);
    }

    public static ServiceRatingService CreateRatings(AppDbContext db)
    {
        return new ServiceRatingService(new SessionRepository(db), new ServiceRatingRepository(db));
    }

    public static DashboardService CreateDashboard(AppDbContext db)
    {
        return new DashboardService(
            new SessionRepository(db),
            new MessageRepository(db),
            new HandoffRepository(db),
            new ChannelIdentityRepository(db),
            new ServiceRatingRepository(db));
    }

    public static ISessionLifecycleService CreateLifecycle(AppDbContext db, ConversationService conversation)
    {
        return new SessionLifecycleService(
            new CustomerRepository(db),
            new SessionRepository(db),
            conversation,
            new MessageRepository(db),
            NullLogger<SessionLifecycleService>.Instance);
    }

    public static TelegramInboundService CreateTelegramInbound(
        AppDbContext db,
        ConversationService conversation,
        ITelegramService telegram)
    {
        return new TelegramInboundService(
            conversation,
            telegram,
            CreateIdentities(db),
            new TelegramCommandHandler(
                CreateLifecycle(db, conversation),
                telegram,
                NullLogger<TelegramCommandHandler>.Instance),
            NullLogger<TelegramInboundService>.Instance);
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

    public static AuthService CreateAuth(
        AppDbContext db,
        TimeProvider? timeProvider = null,
        TwoFactorOptions? twoFactorOptions = null)
    {
        var options = twoFactorOptions ?? new TwoFactorOptions
        {
            EncryptionKey = TwoFactorEncryptionKey
        };
        var wrappedOptions = Options.Create(options);

        return new AuthService(
            new UserRepository(db),
            new TwoFactorRepository(db),
            Options.Create(new JwtOptions()),
            wrappedOptions,
            new TwoFactorProtector(wrappedOptions),
            new TwoFactorCodeService(wrappedOptions),
            timeProvider ?? TimeProvider.System);
    }
}
