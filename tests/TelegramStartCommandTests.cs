using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Services;

namespace Cia.Api.Tests;

public class TelegramStartCommandTests
{
    private const long TelegramUserId = 44112233;
    private const long TelegramChatId = 44112233;

    [Fact]
    public async Task Start_WithoutSession_CreatesNewSession_WithoutCallingUnderstanding()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var scripted = new ScriptedAiProvider();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram, scripted);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("/start"));

        var session = Assert.Single(db.ConversationSessions);
        Assert.Equal($"TG-{TelegramUserId}", session.CustomerId);
        Assert.Equal(ChannelType.Telegram, session.CurrentChannel);
        Assert.Equal(ChannelType.Telegram, session.InitialChannel);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.Equal(IntentType.Unknown, session.DetectedIntent);
        Assert.False(string.IsNullOrWhiteSpace(session.Protocol));
        Assert.DoesNotContain(db.Messages, m => m.Sender == MessageSender.Customer);
        Assert.DoesNotContain(db.Messages, m => m.Content.Contains("/start", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, scripted.UnderstandCalls);
        Assert.Single(telegram.Sent);
        Assert.Contains("Sou a CIA, assistente virtual da Claro", telegram.Sent[0].Text);
        Assert.Contains("Como posso ajudar você hoje?", telegram.Sent[0].Text);
        Assert.DoesNotContain("conexão, de um equipamento ou de uma cobrança", telegram.Sent[0].Text, StringComparison.OrdinalIgnoreCase);

        var context = db.ConversationContexts.Single(c => c.SessionId == session.Id);
        Assert.Equal(IssueType.None, context.IssueType);
        Assert.True(string.IsNullOrWhiteSpace(context.ContextSummary));
        Assert.True(string.IsNullOrWhiteSpace(context.ImportantFacts));
        Assert.True(string.IsNullOrWhiteSpace(context.AdditionalData));
    }

    [Fact]
    public async Task Start_AfterResolved_CreatesDifferentSession_WithEmptyContext()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var previous = db.ConversationSessions.Single();
        previous.Status = SessionStatus.Resolved;
        previous.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        telegram.Sent.Clear();

        await inbound.HandleUpdateAsync(CreateUpdate("/start"));

        Assert.Equal(2, db.ConversationSessions.Count());
        var created = db.ConversationSessions.Single(s => s.Id != previous.Id);
        Assert.NotEqual(previous.Id, created.Id);
        Assert.NotEqual(previous.Protocol, created.Protocol);
        Assert.Equal(SessionStatus.Active, created.Status);
        Assert.Equal(SessionStatus.Resolved, db.ConversationSessions.Single(s => s.Id == previous.Id).Status);

        var context = db.ConversationContexts.Single(c => c.SessionId == created.Id);
        Assert.Equal(IssueType.None, context.IssueType);
        Assert.True(string.IsNullOrWhiteSpace(context.ContextSummary));
        Assert.True(string.IsNullOrWhiteSpace(context.ImportantFacts));
        Assert.True(string.IsNullOrWhiteSpace(context.AdditionalData));
        Assert.Contains("Como posso ajudar você hoje?", telegram.Sent[^1].Text);
    }

    [Fact]
    public async Task Start_AfterInternetProblemResolved_DoesNotCarryInternetIntent()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var previous = db.ConversationSessions.Single();
        Assert.Equal(IntentType.InternetProblem, previous.DetectedIntent);
        Assert.Equal(IssueType.InternetConnection, db.ConversationContexts.Single(c => c.SessionId == previous.Id).IssueType);

        previous.Status = SessionStatus.Resolved;
        await db.SaveChangesAsync();

        await inbound.HandleUpdateAsync(CreateUpdate("/start"));

        var created = db.ConversationSessions.Single(s => s.Id != previous.Id);
        Assert.Equal(IntentType.Unknown, created.DetectedIntent);
        Assert.NotEqual(IntentType.InternetProblem, created.DetectedIntent);
        var newContext = db.ConversationContexts.Single(c => c.SessionId == created.Id);
        Assert.Equal(IssueType.None, newContext.IssueType);
        Assert.Null(newContext.OriginalProblem);
    }

    [Fact]
    public async Task Start_OnActiveAiSession_DoesNotSendCommandToUnderstanding()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var scripted = new ScriptedAiProvider();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram, scripted);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var sessionId = db.ConversationSessions.Single().Id;
        var understandBefore = scripted.UnderstandCalls;
        telegram.Sent.Clear();

        await inbound.HandleUpdateAsync(CreateUpdate("/start"));

        Assert.Equal(understandBefore, scripted.UnderstandCalls);
        Assert.DoesNotContain(scripted.UnderstoodMessages, text => text.Contains("/start", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(sessionId, db.ConversationSessions.Single().Id);
        Assert.DoesNotContain(db.Messages, m => m.Content.Contains("/start", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(TelegramCommandHandler.OfferRestartMessage, telegram.Sent.Single().Text);
        Assert.Contains("/continuar", telegram.Sent.Single().Text);
        Assert.Contains("/novo", telegram.Sent.Single().Text);
        Assert.Equal(IntentType.InternetProblem, db.ConversationSessions.Single().DetectedIntent);
    }

    [Fact]
    public async Task Start_WhenWaitingForAgent_DoesNotCreateSession_AndDoesNotCallAi()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var scripted = new ScriptedAiProvider();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram, scripted);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("Quero falar com um atendente"));
        var session = db.ConversationSessions.Single();
        Assert.Equal(SessionStatus.WaitingForAgent, session.Status);
        var understandBefore = scripted.UnderstandCalls;
        telegram.Sent.Clear();

        await inbound.HandleUpdateAsync(CreateUpdate("/start"));

        Assert.Equal(understandBefore, scripted.UnderstandCalls);
        Assert.Single(db.ConversationSessions);
        Assert.Equal(SessionStatus.WaitingForAgent, db.ConversationSessions.Single().Status);
        Assert.Equal(TelegramCommandHandler.HumanSessionMessage, telegram.Sent.Single().Text);
        Assert.DoesNotContain(db.Messages, m => m.Content.Contains("/start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Start_WhenAssignedToHuman_PreservesHandoff()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, humanAgent, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var agent = TestComposition.SeedAgent(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("Quero falar com um atendente"));
        var queue = await humanAgent.GetQueueAsync();
        await humanAgent.AssumeAsync(queue[0].RequestId, agent.Id);
        var session = db.ConversationSessions.Single();
        Assert.Equal(SessionStatus.Transferred, session.Status);
        var requestId = queue[0].RequestId;
        telegram.Sent.Clear();

        await inbound.HandleUpdateAsync(CreateUpdate("/start"));

        Assert.Single(db.ConversationSessions);
        Assert.Equal(SessionStatus.Transferred, db.ConversationSessions.Single().Status);
        Assert.Equal(HumanAgentRequestStatus.Assigned, db.HumanAgentRequests.Single(r => r.Id == requestId).Status);
        Assert.Equal(TelegramCommandHandler.HumanSessionMessage, telegram.Sent.Single().Text);
        Assert.DoesNotContain(db.Messages, m => m.Content.Contains("/start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Link_StillWorksAfterStartHandler()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("/start"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));

        Assert.Equal(DbSeeder.DemoCustomerId, db.ConversationSessions.Single().CustomerId);
        Assert.Contains(telegram.Sent, item => item.Text.Contains("vinculad", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(DbSeeder.DemoCustomerId, db.CustomerChannelIdentities.Single().CustomerId);
    }

    [Fact]
    public async Task UnlinkThenStart_ReusesTemporaryCustomerWithoutDuplicateKey()
    {
        using var db = TestComposition.CreateDb();
        db.Customers.Add(new Customer
        {
            Id = DbSeeder.PedroCustomerId,
            Name = "Pedro",
            Phone = "11988887777",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var firstLink = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.PedroCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {firstLink.Code}"));
        await identities.UnlinkTelegramAsync(DbSeeder.PedroCustomerId);
        telegram.Sent.Clear();

        var exception = await Record.ExceptionAsync(() => inbound.HandleUpdateAsync(CreateUpdate("/start")));
        Assert.Null(exception);

        Assert.Equal(1, db.Customers.Count(c => c.Id == $"TG-{TelegramUserId}"));
        var reused = db.Customers.Single(c => c.Id == $"TG-{TelegramUserId}");
        Assert.Equal(TelegramUserId, reused.TelegramUserId);
        var newSession = Assert.Single(db.ConversationSessions.Where(s => s.CustomerId == reused.Id));
        Assert.Equal(SessionStatus.Active, newSession.Status);
        Assert.Contains("Como posso ajudar você hoje?", telegram.Sent[^1].Text);
        Assert.Equal(DbSeeder.PedroCustomerId, db.ConversationSessions.Single(s => s.CustomerId == DbSeeder.PedroCustomerId).CustomerId);
    }

    [Fact]
    public async Task Start_DoesNotDeletePreviousSessionOrRating()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var previous = db.ConversationSessions.Single();
        previous.Status = SessionStatus.Resolved;
        await db.SaveChangesAsync();
        await TestComposition.CreateRatings(db).SubmitAsync(
            previous.CustomerId,
            previous.Id,
            new SubmitServiceRatingRequest { Score = 5, Comment = "Bom atendimento." });
        var oldMessageCount = db.Messages.Count(m => m.SessionId == previous.Id);

        await inbound.HandleUpdateAsync(CreateUpdate("/start"));

        Assert.Equal(2, db.ConversationSessions.Count());
        Assert.True(db.ConversationSessions.Any(s => s.Id == previous.Id));
        Assert.Equal(oldMessageCount, db.Messages.Count(m => m.SessionId == previous.Id));
        Assert.Equal(5, db.ServiceRatings.Single(r => r.SessionId == previous.Id).Score);
        Assert.Equal("Bom atendimento.", db.ServiceRatings.Single().Comment);
    }

    private static TelegramUpdateDto CreateUpdate(string text)
    {
        return new TelegramUpdateDto
        {
            UpdateId = 9,
            Message = new TelegramMessageDto
            {
                MessageId = 10,
                Text = text,
                From = new TelegramUserDto
                {
                    Id = TelegramUserId,
                    FirstName = "Ana",
                    Username = "ana_claro"
                },
                Chat = new TelegramChatDto
                {
                    Id = TelegramChatId,
                    Type = "private"
                }
            }
        };
    }
}
