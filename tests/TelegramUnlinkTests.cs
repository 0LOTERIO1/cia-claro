using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Services;

namespace Cia.Api.Tests;

public class TelegramUnlinkTests
{
    private const long TelegramUserId = 44112233;
    private const long TelegramChatId = 44112233;

    [Fact]
    public async Task Customer_CanUnlinkOwnTelegram_WithoutLosingSession()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = TestComposition.CreateTelegramInbound(db, conversation, telegram);
        var identities = TestComposition.CreateIdentities(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {generated.Code}"));

        var session = Assert.Single(db.ConversationSessions);
        var sessionId = session.Id;
        var protocol = session.Protocol;
        var messageCount = db.Messages.Count();
        var contextId = db.ConversationContexts.Single().Id;
        Assert.Equal(DbSeeder.DemoCustomerId, session.CustomerId);
        Assert.Contains(db.CustomerChannelIdentities, i => i.CustomerId == DbSeeder.DemoCustomerId);

        var result = await identities.UnlinkTelegramAsync(DbSeeder.DemoCustomerId);

        Assert.True(result.Success);
        Assert.Equal("Telegram desconectado com sucesso.", result.Message);
        Assert.DoesNotContain(db.CustomerChannelIdentities, i => i.CustomerId == DbSeeder.DemoCustomerId && i.Channel == ChannelType.Telegram);
        Assert.Null(db.Customers.Single(c => c.Id == DbSeeder.DemoCustomerId).TelegramUserId);
        Assert.Null(db.Customers.Single(c => c.Id == DbSeeder.DemoCustomerId).TelegramChatId);

        var kept = db.ConversationSessions.Single();
        Assert.Equal(sessionId, kept.Id);
        Assert.Equal(protocol, kept.Protocol);
        Assert.Equal(DbSeeder.DemoCustomerId, kept.CustomerId);
        Assert.Equal(messageCount, db.Messages.Count());
        Assert.Equal(contextId, db.ConversationContexts.Single().Id);
        Assert.Equal(ChannelType.WebPortal, kept.CurrentChannel);
    }

    [Fact]
    public async Task Unlink_IsIdempotent_WhenTelegramIsNotConnected()
    {
        using var db = TestComposition.CreateDb();
        var identities = TestComposition.CreateIdentities(db);

        var first = await identities.UnlinkTelegramAsync(DbSeeder.DemoCustomerId);
        var second = await identities.UnlinkTelegramAsync(DbSeeder.DemoCustomerId);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal("Nenhum Telegram estava conectado.", first.Message);
        Assert.Equal("Nenhum Telegram estava conectado.", second.Message);
        Assert.Empty(db.CustomerChannelIdentities);
    }

    [Fact]
    public async Task OtherCustomer_CannotRemoveSomeoneElsesTelegram()
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

        var identities = TestComposition.CreateIdentities(db);
        var generated = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await identities.RedeemTelegramLinkAsync(generated.Code, TelegramUserId, TelegramChatId, "Lucas");

        var pedroResult = await identities.UnlinkTelegramAsync(DbSeeder.PedroCustomerId);

        Assert.True(pedroResult.Success);
        Assert.Equal("Nenhum Telegram estava conectado.", pedroResult.Message);
        Assert.Contains(
            db.CustomerChannelIdentities,
            i => i.CustomerId == DbSeeder.DemoCustomerId
                 && i.Channel == ChannelType.Telegram
                 && i.ExternalUserId == TelegramUserId.ToString());
        Assert.Equal(TelegramUserId, db.Customers.Single(c => c.Id == DbSeeder.DemoCustomerId).TelegramUserId);
    }

    [Fact]
    public async Task AfterUnlink_NewLinkCodeBindsAgainToSameCustomer()
    {
        using var db = TestComposition.CreateDb();
        var identities = TestComposition.CreateIdentities(db);

        var first = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        await identities.RedeemTelegramLinkAsync(first.Code, TelegramUserId, TelegramChatId, "Lucas");
        await identities.UnlinkTelegramAsync(DbSeeder.DemoCustomerId);

        var second = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.DemoCustomerId);
        var confirmation = await identities.RedeemTelegramLinkAsync(second.Code, TelegramUserId, TelegramChatId, "Lucas");

        Assert.Contains("vinculada com sucesso", confirmation, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DbSeeder.DemoCustomerId, db.CustomerChannelIdentities.Single().CustomerId);
        Assert.Equal(TelegramUserId, db.Customers.Single(c => c.Id == DbSeeder.DemoCustomerId).TelegramUserId);
    }

    [Fact]
    public async Task Unlink_DoesNotLoseHumanAttendance()
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
        await inbound.HandleUpdateAsync(CreateUpdate("Quero falar com um atendente"));

        var queue = await humanAgent.GetQueueAsync();
        await humanAgent.AssumeAsync(queue[0].RequestId, agent.Id);
        var requestId = queue[0].RequestId;
        var sessionId = db.ConversationSessions.Single().Id;
        var assistantCount = db.Messages.Count(m => m.Sender == MessageSender.Assistant);

        await identities.UnlinkTelegramAsync(DbSeeder.DemoCustomerId);

        var session = db.ConversationSessions.Single();
        Assert.Equal(sessionId, session.Id);
        Assert.Equal(SessionStatus.Transferred, session.Status);
        Assert.Equal(DepartmentType.HumanAgent, session.CurrentDepartment);
        Assert.Equal(ChannelType.WebPortal, session.CurrentChannel);
        Assert.Equal(requestId, db.HumanAgentRequests.Single().Id);
        Assert.Equal(agent.Id, db.HumanAgentRequests.Single().AssignedAgentId);
        Assert.Equal(HumanAgentRequestStatus.Assigned, db.HumanAgentRequests.Single().Status);
        Assert.Equal(assistantCount, db.Messages.Count(m => m.Sender == MessageSender.Assistant));

        telegram.Sent.Clear();
        await humanAgent.SendMessageAsync(sessionId, agent.Id, "Continuamos pelo Portal CIA.");
        Assert.Empty(telegram.Sent);
        Assert.Contains(db.Messages, m => m.Sender == MessageSender.HumanAgent && m.Content.Contains("Portal CIA"));
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
                    FirstName = "Lucas",
                    Username = "lucas_claro"
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
