using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Exceptions;
using Cia.Api.Repositories;
using Cia.Api.Services;

namespace Cia.Api.Tests;

public class OmnichannelFlowTests
{
    private const long TelegramUserId = 99887766;
    private const long TelegramChatId = 99887766;

    [Fact]
    public async Task UnknownTelegram_CreatesTemporaryIdentityAndSession()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));

        var customer = Assert.Single(db.Customers.Where(c => c.Id.StartsWith("TG-")));
        Assert.Equal($"TG-{TelegramUserId}", customer.Id);
        Assert.Equal(TelegramUserId, customer.TelegramUserId);
        Assert.Equal(TelegramChatId, customer.TelegramChatId);

        var identity = Assert.Single(db.CustomerChannelIdentities);
        Assert.Equal(customer.Id, identity.CustomerId);
        Assert.Equal(ChannelType.Telegram, identity.Channel);
        Assert.Equal(TelegramUserId.ToString(), identity.ExternalUserId);

        var session = Assert.Single(db.ConversationSessions);
        Assert.Equal(customer.Id, session.CustomerId);
        Assert.Equal(ChannelType.Telegram, session.CurrentChannel);
        Assert.Equal(ChannelType.Telegram, session.InitialChannel);
    }

    [Fact]
    public async Task PortalCustomer_GeneratesTelegramLinkCode()
    {
        using var db = TestComposition.CreateDb();
        var identities = TestComposition.CreateIdentities(db);

        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);

        Assert.StartsWith("CIA-", generated.Code);
        Assert.Equal($"/link {generated.Code}", generated.Command);
        Assert.Equal(10, generated.ExpiresInMinutes);
        Assert.True(generated.ExpiresAt > DateTime.UtcNow.AddMinutes(9));
        var stored = Assert.Single(db.ChannelLinkCodes);
        Assert.Equal(ChannelIdentityService.HashCode(generated.Code), stored.CodeHash);
        Assert.Null(stored.UsedAt);
        Assert.DoesNotContain(stored.CodeHash, generated.Code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IncorrectLinkCode_IsRejected()
    {
        using var db = TestComposition.CreateDb();
        var identities = TestComposition.CreateIdentities(db);
        await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);

        var ex = await Assert.ThrowsAsync<ValidationAppException>(() =>
            identities.RedeemTelegramLinkAsync("CIA-000000", TelegramUserId, TelegramChatId, "Pedro"));
        Assert.Contains("inválido", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.CustomerChannelIdentities);
    }

    [Fact]
    public async Task ExpiredLinkCode_IsRejected()
    {
        using var db = TestComposition.CreateDb();
        var identities = TestComposition.CreateIdentities(db);
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        var stored = db.ChannelLinkCodes.Single();
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationAppException>(() =>
            identities.RedeemTelegramLinkAsync(generated.Code, TelegramUserId, TelegramChatId, "Pedro"));
        Assert.Contains("expirou", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UsedLinkCode_CannotBeRedeemedTwice()
    {
        using var db = TestComposition.CreateDb();
        var identities = TestComposition.CreateIdentities(db);
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);

        await identities.RedeemTelegramLinkAsync(generated.Code, TelegramUserId, TelegramChatId, "Pedro");
        var ex = await Assert.ThrowsAsync<ValidationAppException>(() =>
            identities.RedeemTelegramLinkAsync(generated.Code, TelegramUserId, TelegramChatId, "Pedro"));
        Assert.Contains("já foi utilizado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TelegramLinkedToAnotherCustomer_CannotBeStolen()
    {
        using var db = TestComposition.CreateDb();
        db.Customers.Add(new Customer
        {
            Id = "CLIENTE-002",
            Name = "Maria",
            Phone = "11988888888",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var identities = TestComposition.CreateIdentities(db);
        var first = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await identities.RedeemTelegramLinkAsync(first.Code, TelegramUserId, TelegramChatId, "Lucas");

        var second = await identities.GenerateTelegramLinkCodeAsync("CLIENTE-002");
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            identities.RedeemTelegramLinkAsync(second.Code, TelegramUserId, TelegramChatId, "Maria"));
        Assert.Contains("outra conta", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DbSeeder.DemoCustomerId, db.CustomerChannelIdentities.Single().CustomerId);
    }

    [Fact]
    public async Task ValidLinkCommand_BindsTelegramToPortalCustomer()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);

        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));

        var portal = db.Customers.Single(c => c.Id == DbSeeder.DemoCustomerId);
        Assert.Equal(TelegramUserId, portal.TelegramUserId);
        Assert.Equal(TelegramChatId, portal.TelegramChatId);
        var identity = Assert.Single(db.CustomerChannelIdentities);
        Assert.Equal(DbSeeder.DemoCustomerId, identity.CustomerId);
        Assert.NotNull(identity.VerifiedAt);
        Assert.Contains("vinculada com sucesso", telegram.Sent[^1].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.ConversationSessions);
    }

    [Fact]
    public async Task SessionStartedBeforeLink_IsAdoptedWithoutLosingHistory()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("Ja reiniciei o modem e continua sem internet"));

        var original = db.ConversationSessions.Single();
        var sessionId = original.Id;
        var protocol = original.Protocol;
        var messageCount = db.Messages.Count();
        var context = db.ConversationContexts.Single();
        Assert.StartsWith("TG-", original.CustomerId);
        Assert.True(context.ModemRestarted);

        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));

        var adopted = db.ConversationSessions.Single();
        Assert.Equal(sessionId, adopted.Id);
        Assert.Equal(protocol, adopted.Protocol);
        Assert.Equal(DbSeeder.DemoCustomerId, adopted.CustomerId);
        Assert.Equal(messageCount, db.Messages.Count());
        Assert.Equal(context.Id, db.ConversationContexts.Single().Id);
        Assert.True(db.ConversationContexts.Single().ModemRestarted);
        Assert.True(db.ConversationContexts.Single().InternetStillDown);
        Assert.Null(db.Customers.Single(c => c.Id.StartsWith("TG-")).TelegramUserId);
    }

    [Fact]
    public async Task AfterLink_TelegramMessageResolvesToPortalCustomer()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));
        await inbound.HandleUpdateAsync(CreateUpdate("Quero continuar o atendimento"));

        Assert.Equal(DbSeeder.DemoCustomerId, db.ConversationSessions.Single().CustomerId);
        Assert.DoesNotContain(db.ConversationSessions, s => s.CustomerId.StartsWith("TG-"));
        Assert.Equal(1, db.Customers.Count(c => c.TelegramUserId == TelegramUserId));
    }

    [Fact]
    public async Task Portal_RecoversSessionStartedOnTelegram()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));

        var snapshot = await identities.GetActiveSessionAsync(DbSeeder.DemoCustomerId);
        Assert.NotNull(snapshot.Session);
        Assert.Equal(db.ConversationSessions.Single().Id, snapshot.Session.Id);
        Assert.Equal(ChannelType.Telegram, snapshot.Session.InitialChannel);
        Assert.Contains(snapshot.Messages, m => m.Content.Contains("internet", StringComparison.OrdinalIgnoreCase));
        Assert.True(snapshot.Channels[0].Connected);
    }

    [Fact]
    public async Task PortalMessage_ContinuesSameSession()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));
        var sessionId = db.ConversationSessions.Single().Id;
        var protocol = db.ConversationSessions.Single().Protocol;

        var portal = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Quero continuar meu atendimento por aqui."
        });

        Assert.Equal(sessionId, portal.SessionId);
        Assert.Equal(protocol, portal.Protocol);
        Assert.Equal(ChannelType.WebPortal, portal.CurrentChannel);
        Assert.Contains(portal.Messages, m => m.Channel == ChannelType.Telegram);
        Assert.Contains(portal.Messages, m => m.Channel == ChannelType.WebPortal && m.Sender == MessageSender.Customer);
        Assert.Single(db.ConversationSessions);
    }

    [Fact]
    public async Task CurrentChannel_SwitchesTelegramToWebPortal()
    {
        using var db = TestComposition.CreateDb();
        var identities = TestComposition.CreateIdentities(db);
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));

        Assert.Equal(ChannelType.Telegram, db.ConversationSessions.Single().CurrentChannel);
        var resumed = await identities.ResumeActiveSessionAsync(DbSeeder.DemoCustomerId);
        Assert.Equal(ChannelType.WebPortal, resumed.Session?.CurrentChannel);
        Assert.Equal(ChannelType.Telegram, resumed.Session?.InitialChannel);
    }

    [Fact]
    public async Task LaterTelegramMessage_SwitchesBackWithoutNewSession()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));
        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Quero continuar por aqui."
        });

        Assert.Equal(ChannelType.WebPortal, db.ConversationSessions.Single().CurrentChannel);
        var sessionId = db.ConversationSessions.Single().Id;

        await inbound.HandleUpdateAsync(CreateUpdate("Voltei no Telegram"));

        Assert.Single(db.ConversationSessions);
        Assert.Equal(sessionId, db.ConversationSessions.Single().Id);
        Assert.Equal(ChannelType.Telegram, db.ConversationSessions.Single().CurrentChannel);
    }

    [Fact]
    public async Task Context_RemainsDuringChannelSwitch()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("Ja reiniciei o modem e continua sem internet"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));

        var before = db.ConversationContexts.Single();
        Assert.Equal(IssueType.InternetConnection, before.IssueType);
        Assert.True(before.ModemRestarted);

        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Quero continuar por aqui."
        });
        await inbound.HandleUpdateAsync(CreateUpdate("Ainda estou sem internet"));

        var after = db.ConversationContexts.Single();
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(IssueType.InternetConnection, after.IssueType);
        Assert.True(after.ModemRestarted);
        Assert.True(after.InternetStillDown);
        Assert.Equal(DepartmentType.ModemReplacement, db.ConversationSessions.Single().CurrentDepartment);
    }

    [Fact]
    public async Task HumanAgentOnTelegram_SendsReplyToTelegram()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, humanAgent, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var agent = TestComposition.SeedAgent(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("Quero falar com um atendente"));
        telegram.Sent.Clear();

        var queue = await humanAgent.GetQueueAsync();
        await humanAgent.AssumeAsync(queue[0].RequestId, agent.Id);
        Assert.NotEmpty(telegram.Sent);

        telegram.Sent.Clear();
        await humanAgent.SendMessageAsync(db.ConversationSessions.Single().Id, agent.Id, "Vou ajudar pelo Telegram.");
        Assert.Single(telegram.Sent);
        Assert.Equal(TelegramChatId, telegram.Sent[0].ChatId);
        Assert.Contains("Telegram", telegram.Sent[0].Text);
    }

    [Fact]
    public async Task HumanAgentOnPortal_DoesNotDuplicateToTelegram()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, humanAgent, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);
        var agent = TestComposition.SeedAgent(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));
        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Quero falar com um atendente"
        });

        Assert.Equal(ChannelType.WebPortal, db.ConversationSessions.Single().CurrentChannel);
        telegram.Sent.Clear();

        var queue = await humanAgent.GetQueueAsync();
        await humanAgent.AssumeAsync(queue[0].RequestId, agent.Id);
        Assert.Empty(telegram.Sent);

        await humanAgent.SendMessageAsync(db.ConversationSessions.Single().Id, agent.Id, "Vou continuar pelo Portal CIA.");
        Assert.Empty(telegram.Sent);
        Assert.Contains(db.Messages, m => m.Sender == MessageSender.HumanAgent && m.Channel == ChannelType.WebPortal);
    }

    [Fact]
    public async Task SwitchingToPortalDuringHumanAttendance_KeepsAgentAndDisablesAi()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, humanAgent, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);
        var agent = TestComposition.SeedAgent(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("Quero falar com um atendente"));
        var queue = await humanAgent.GetQueueAsync();
        await humanAgent.AssumeAsync(queue[0].RequestId, agent.Id);

        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));

        var assistantCount = db.Messages.Count(m => m.Sender == MessageSender.Assistant);
        var followUp = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Vou continuar por aqui com o atendente."
        });

        Assert.Equal(SessionStatus.Transferred, followUp.Status);
        Assert.Equal(assistantCount, db.Messages.Count(m => m.Sender == MessageSender.Assistant));
        Assert.Equal(MessageSender.Customer, followUp.Messages[^1].Sender);
        Assert.Equal(queue[0].RequestId, db.HumanAgentRequests.Single().Id);
        Assert.Equal(agent.Id, db.HumanAgentRequests.Single().AssignedAgentId);
        Assert.Equal(ChannelType.WebPortal, db.ConversationSessions.Single().CurrentChannel);
        Assert.Single(db.ConversationSessions);
    }

    [Fact]
    public async Task AdminDetail_ShowsLinkedChannelsAndJourney()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("Ja reiniciei o modem"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));
        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Quero continuar por aqui."
        });

        var dashboard = new DashboardService(
            new SessionRepository(db),
            new MessageRepository(db),
            new HandoffRepository(db),
            new ChannelIdentityRepository(db),
            new ServiceRatingRepository(db));
        var detail = await dashboard.GetSessionDetailAsync(db.ConversationSessions.Single().Id);

        Assert.Equal(DbSeeder.DemoCustomerId, detail.Customer.Id);
        Assert.Contains(detail.LinkedChannels, c => c.Channel == ChannelType.Telegram && c.Connected);
        Assert.Equal(ChannelType.Telegram, detail.Session.InitialChannel);
        Assert.Equal(ChannelType.WebPortal, detail.Session.CurrentChannel);
        Assert.Contains(detail.Messages, m => m.Channel == ChannelType.Telegram);
        Assert.Contains(detail.Messages, m => m.Channel == ChannelType.WebPortal);
        Assert.True(detail.Transfers.Count >= 1);
        Assert.Equal(DepartmentType.ModemReplacement, detail.Session.CurrentDepartment);
    }

    [Fact]
    public async Task LinkCommand_IsNotSentToConversationService()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);

        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));

        Assert.Empty(db.Messages);
        Assert.Empty(db.ConversationSessions);
    }

    private static TelegramUpdateDto CreateUpdate(string text)
    {
        return new TelegramUpdateDto
        {
            UpdateId = 1,
            Message = new TelegramMessageDto
            {
                MessageId = 10,
                Text = text,
                From = new TelegramUserDto
                {
                    Id = TelegramUserId,
                    FirstName = "Pedro",
                    Username = "pedro_claro"
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
