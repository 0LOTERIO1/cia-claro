using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Services;

namespace Cia.Api.Tests;

public class TelegramSessionLifecycleTests
{
    private const long TelegramUserId = 55667788;
    private const long TelegramChatId = 55667788;

    [Fact]
    public async Task Start_OnActiveAiSession_OffersContinueOrNew_WithoutCallingAi()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var scripted = new ScriptedAiProvider();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram, scripted);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var understandBefore = scripted.UnderstandCalls;
        telegram.Sent.Clear();

        await inbound.HandleUpdateAsync(CreateUpdate("/start"));

        Assert.Equal(understandBefore, scripted.UnderstandCalls);
        Assert.Single(db.ConversationSessions);
        Assert.Equal(SessionStatus.Active, db.ConversationSessions.Single().Status);
        Assert.Equal(TelegramCommandHandler.OfferRestartMessage, telegram.Sent.Single().Text);
        Assert.DoesNotContain(scripted.UnderstoodMessages, text => text.Contains("/start"));
    }

    [Fact]
    public async Task Continue_OnActiveAiSession_KeepsSameSessionId()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var scripted = new ScriptedAiProvider();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram, scripted);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var sessionId = db.ConversationSessions.Single().Id;
        var protocol = db.ConversationSessions.Single().Protocol;
        var understandBefore = scripted.UnderstandCalls;
        telegram.Sent.Clear();

        await inbound.HandleUpdateAsync(CreateUpdate("/continuar"));

        Assert.Equal(understandBefore, scripted.UnderstandCalls);
        Assert.Equal(sessionId, db.ConversationSessions.Single().Id);
        Assert.Equal(protocol, db.ConversationSessions.Single().Protocol);
        Assert.Equal(SessionStatus.Active, db.ConversationSessions.Single().Status);
        Assert.Equal(TelegramCommandHandler.ContinuedMessage, telegram.Sent.Single().Text);
        Assert.DoesNotContain(db.Messages, m => m.Content.Contains("/continuar"));
    }

    [Fact]
    public async Task Novo_OnActiveAiSession_ClosesOldAndCreatesEmptySession()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var scripted = new ScriptedAiProvider();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram, scripted);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var previous = db.ConversationSessions.Single();
        var understandBefore = scripted.UnderstandCalls;
        telegram.Sent.Clear();

        await inbound.HandleUpdateAsync(CreateUpdate("/novo"));

        Assert.Equal(understandBefore, scripted.UnderstandCalls);
        Assert.Equal(2, db.ConversationSessions.Count());
        var closed = db.ConversationSessions.Single(s => s.Id == previous.Id);
        Assert.Equal(SessionStatus.Resolved, closed.Status);
        Assert.Equal(SessionClosureReason.CustomerRestarted, closed.ClosureReason);
        Assert.False(SessionRules.CanRate(closed));

        var created = db.ConversationSessions.Single(s => s.Id != previous.Id);
        Assert.NotEqual(previous.Protocol, created.Protocol);
        Assert.Equal(SessionStatus.Active, created.Status);
        Assert.Null(created.ClosureReason);
        Assert.Equal(IssueType.None, db.ConversationContexts.Single(c => c.SessionId == created.Id).IssueType);
        Assert.Contains("Como posso ajudar você hoje?", telegram.Sent[^1].Text);
        Assert.True(db.Messages.Any(m => m.SessionId == previous.Id));
    }

    [Fact]
    public async Task Novo_DoesNotInheritInternetProblemFromPreviousSession()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var previous = db.ConversationSessions.Single();
        Assert.Equal(IntentType.InternetProblem, previous.DetectedIntent);

        await inbound.HandleUpdateAsync(CreateUpdate("/novo"));

        var created = db.ConversationSessions.Single(s => s.Id != previous.Id);
        Assert.Equal(IntentType.Unknown, created.DetectedIntent);
        var newContext = db.ConversationContexts.Single(c => c.SessionId == created.Id);
        Assert.Equal(IssueType.None, newContext.IssueType);
        Assert.True(string.IsNullOrWhiteSpace(newContext.ImportantFacts));
        Assert.True(string.IsNullOrWhiteSpace(newContext.ContextSummary));
        Assert.Equal(IssueType.InternetConnection, db.ConversationContexts.Single(c => c.SessionId == previous.Id).IssueType);
    }

    [Fact]
    public async Task Encerrar_ThenStart_CreatesNewSession()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var scripted = new ScriptedAiProvider();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram, scripted);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var previous = db.ConversationSessions.Single();
        var understandBefore = scripted.UnderstandCalls;
        telegram.Sent.Clear();

        await inbound.HandleUpdateAsync(CreateUpdate("/encerrar"));

        Assert.Equal(understandBefore, scripted.UnderstandCalls);
        Assert.Equal(SessionStatus.Resolved, db.ConversationSessions.Single().Status);
        Assert.Equal(SessionClosureReason.CustomerEnded, db.ConversationSessions.Single().ClosureReason);
        Assert.Equal(TelegramCommandHandler.EndedMessage, telegram.Sent.Single().Text);
        Assert.False(SessionRules.CanRate(db.ConversationSessions.Single()));

        telegram.Sent.Clear();
        await inbound.HandleUpdateAsync(CreateUpdate("/start"));

        Assert.Equal(2, db.ConversationSessions.Count());
        var created = db.ConversationSessions.Single(s => s.Id != previous.Id);
        Assert.Equal(SessionStatus.Active, created.Status);
        Assert.Contains("Como posso ajudar você hoje?", telegram.Sent[^1].Text);
    }

    [Fact]
    public async Task Novo_WhenWaitingForAgent_DoesNotCloseHumanSession()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("Quero falar com um atendente"));
        telegram.Sent.Clear();

        await inbound.HandleUpdateAsync(CreateUpdate("/novo"));

        Assert.Single(db.ConversationSessions);
        Assert.Equal(SessionStatus.WaitingForAgent, db.ConversationSessions.Single().Status);
        Assert.Null(db.ConversationSessions.Single().ClosureReason);
        Assert.Equal(TelegramCommandHandler.HumanSessionMessage, telegram.Sent.Single().Text);
    }

    [Fact]
    public async Task Encerrar_WhenAssigned_DoesNotCloseHumanSession()
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
        telegram.Sent.Clear();

        await inbound.HandleUpdateAsync(CreateUpdate("/encerrar"));

        Assert.Single(db.ConversationSessions);
        Assert.Equal(SessionStatus.Transferred, db.ConversationSessions.Single().Status);
        Assert.Equal(HumanAgentRequestStatus.Assigned, db.HumanAgentRequests.Single().Status);
        Assert.Null(db.ConversationSessions.Single().ClosureReason);
        Assert.Equal(TelegramCommandHandler.HumanEndMessage, telegram.Sent.Single().Text);
    }

    [Fact]
    public async Task AgentFinish_MarksCompleted_AndAllowsRating()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, humanAgent, _) = TestComposition.CreateServices(db);
        var agent = TestComposition.SeedAgent(db);

        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });
        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Quero falar com um atendente"
        });

        var queue = await humanAgent.GetQueueAsync();
        await humanAgent.AssumeAsync(queue[0].RequestId, agent.Id);
        var finished = await humanAgent.FinishAsync(queue[0].RequestId, agent.Id);

        Assert.Equal(SessionStatus.Resolved, finished.Session.Status);
        Assert.Equal(SessionClosureReason.Completed, finished.Session.ClosureReason);
        Assert.True(finished.Session.CanRate);

        var rated = await TestComposition.CreateRatings(db).SubmitAsync(
            DbSeeder.DemoCustomerId,
            finished.Session.Id,
            new SubmitServiceRatingRequest { Score = 5 });
        Assert.True(rated.Success);
    }

    [Fact]
    public async Task Novo_OldSessionCannotBeRated()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var previous = db.ConversationSessions.Single();
        await inbound.HandleUpdateAsync(CreateUpdate("/novo"));

        var error = await Assert.ThrowsAsync<ValidationAppException>(() =>
            TestComposition.CreateRatings(db).SubmitAsync(
                previous.CustomerId,
                previous.Id,
                new SubmitServiceRatingRequest { Score = 5 }));

        Assert.Contains("não está disponível", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(SessionRules.CanRate(db.ConversationSessions.Single(s => s.Id == previous.Id)));
    }

    [Fact]
    public async Task Encerrar_OldSessionCannotBeRated()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var session = db.ConversationSessions.Single();
        await inbound.HandleUpdateAsync(CreateUpdate("/encerrar"));

        var error = await Assert.ThrowsAsync<ValidationAppException>(() =>
            TestComposition.CreateRatings(db).SubmitAsync(
                session.CustomerId,
                session.Id,
                new SubmitServiceRatingRequest { Score = 4 }));

        Assert.Contains("não está disponível", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False((await TestComposition.CreateIdentities(db).GetActiveSessionAsync(session.CustomerId)).CanRate);
    }

    [Fact]
    public async Task LegacyResolvedWithoutClosureReason_RemainsRateable()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, _, _) = TestComposition.CreateServices(db);
        var created = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });

        var session = await db.ConversationSessions.FindAsync(created.SessionId);
        session!.Status = SessionStatus.Resolved;
        session.ClosureReason = null;
        await db.SaveChangesAsync();

        Assert.True(SessionRules.CanRate(session));
        var snapshot = await TestComposition.CreateIdentities(db).GetActiveSessionAsync(DbSeeder.DemoCustomerId);
        Assert.True(snapshot.CanRate);

        var rated = await TestComposition.CreateRatings(db).SubmitAsync(
            DbSeeder.DemoCustomerId,
            session.Id,
            new SubmitServiceRatingRequest { Score = 5 });
        Assert.True(rated.Success);
    }

    [Fact]
    public async Task PortalRestart_FollowsTheSameLifecycleAsNovo()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, _, _) = TestComposition.CreateServices(db);
        var created = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });

        var result = await TestComposition.CreateLifecycle(db, conversation)
            .RestartAsync(DbSeeder.DemoCustomerId, ChannelType.WebPortal);

        Assert.Equal(created.SessionId, result.ClosedSessionId);
        Assert.NotEqual(created.SessionId, result.NewSessionId);
        var closed = db.ConversationSessions.Single(s => s.Id == created.SessionId);
        Assert.Equal(SessionStatus.Resolved, closed.Status);
        Assert.Equal(SessionClosureReason.CustomerRestarted, closed.ClosureReason);
        Assert.False(SessionRules.CanRate(closed));
        Assert.Equal(ChannelType.WebPortal, db.ConversationSessions.Single(s => s.Id == result.NewSessionId).CurrentChannel);
        Assert.Equal(IssueType.None, db.ConversationContexts.Single(c => c.SessionId == result.NewSessionId).IssueType);
    }

    [Fact]
    public async Task Link_StillWorksAfterNovo()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("/novo"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));

        Assert.Equal(DbSeeder.DemoCustomerId, db.CustomerChannelIdentities.Single().CustomerId);
        Assert.Contains(db.ConversationSessions, s => s.CustomerId == DbSeeder.DemoCustomerId && s.Status == SessionStatus.Active);
    }

    [Fact]
    public async Task UnlinkThenStart_StillReusesTemporaryCustomer()
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

        var exception = await Record.ExceptionAsync(() => inbound.HandleUpdateAsync(CreateUpdate("/start")));
        Assert.Null(exception);
        Assert.Equal(1, db.Customers.Count(c => c.Id == $"TG-{TelegramUserId}"));
        Assert.Equal(TelegramUserId, db.Customers.Single(c => c.Id == $"TG-{TelegramUserId}").TelegramUserId);
    }

    private static TelegramUpdateDto CreateUpdate(string text)
    {
        return new TelegramUpdateDto
        {
            UpdateId = 12,
            Message = new TelegramMessageDto
            {
                MessageId = 20,
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
