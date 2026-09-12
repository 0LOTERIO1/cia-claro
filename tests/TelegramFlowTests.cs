using Cia.Api.Configuration;
using Cia.Api.Controllers;
using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Cia.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cia.Api.Tests;

public class TelegramFlowTests
{
    private const long ChatId = 55119999;
    private const long UserId = 55119999;

    [Fact]
    public async Task Webhook_RejectsInvalidSecret()
    {
        var inbound = new RecordingInboundService();
        var controller = CreateController("expected-secret", inbound);
        controller.ControllerContext.HttpContext.Request.Headers[TelegramWebhookController.SecretHeaderName] = "wrong";

        var result = await controller.Webhook(CreateUpdate("Olá"), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, inbound.Calls);
    }

    [Fact]
    public async Task Webhook_RejectsMissingSecret()
    {
        var inbound = new RecordingInboundService();
        var controller = CreateController("expected-secret", inbound);

        var result = await controller.Webhook(CreateUpdate("Olá"), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, inbound.Calls);
    }

    [Fact]
    public async Task Webhook_AcceptsValidSecret()
    {
        var inbound = new RecordingInboundService();
        var controller = CreateController("expected-secret", inbound);
        controller.ControllerContext.HttpContext.Request.Headers[TelegramWebhookController.SecretHeaderName] = "expected-secret";

        var result = await controller.Webhook(CreateUpdate("Olá"), CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal(1, inbound.Calls);
    }

    [Fact]
    public async Task TelegramMessage_CreatesCustomerAndReusesSession()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = CreateInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("Ja reiniciei o modem"));

        var customer = db.Customers.Single(c => c.TelegramUserId == UserId);
        Assert.Equal($"TG-{UserId}", customer.Id);
        Assert.Equal("Lucas", customer.Name);
        Assert.Equal(ChatId, customer.TelegramChatId);
        Assert.Single(db.ConversationSessions);
        Assert.Equal(ChannelType.Telegram, db.ConversationSessions.Single().CurrentChannel);
        Assert.Equal(2, telegram.Sent.Count);
        Assert.All(telegram.Sent, item => Assert.Equal(ChatId, item.ChatId));
    }

    [Fact]
    public async Task TelegramMessage_GoesThroughConversationService()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, _, _) = TestComposition.CreateServices(db, telegram);
        var inbound = CreateInbound(db, conversation, telegram);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));

        var session = db.ConversationSessions.Single();
        Assert.Equal(DepartmentType.TechnicalSupport, session.CurrentDepartment);
        Assert.Equal(IntentType.InternetProblem, session.DetectedIntent);
        Assert.Contains(db.Messages, m => m.Sender == MessageSender.Customer);
        Assert.Contains(db.Messages, m => m.Sender == MessageSender.Assistant);
        Assert.False(string.IsNullOrWhiteSpace(telegram.Sent[0].Text));
    }

    [Fact]
    public async Task HumanHandoff_DoesNotAutoReplyWithAi_AndAgentMessageGoesToTelegram()
    {
        using var db = TestComposition.CreateDb();
        var telegram = new FakeTelegramService();
        var (conversation, _, humanAgent, _) = TestComposition.CreateServices(db, telegram);
        var inbound = CreateInbound(db, conversation, telegram);
        var agent = TestComposition.SeedAgent(db);

        await inbound.HandleUpdateAsync(CreateUpdate("Minha internet nao esta funcionando"));
        await inbound.HandleUpdateAsync(CreateUpdate("Ja reiniciei o modem"));
        await inbound.HandleUpdateAsync(CreateUpdate("Quero falar com um atendente"));

        var ciaReplies = telegram.Sent.Count;
        var session = db.ConversationSessions.Single();
        Assert.Equal(SessionStatus.WaitingForAgent, session.Status);

        var queue = await humanAgent.GetQueueAsync();
        var assumed = await humanAgent.AssumeAsync(queue[0].RequestId, agent.Id);
        Assert.Equal(SessionStatus.Transferred, assumed.Session.Status);
        Assert.True(telegram.Sent.Count > ciaReplies);

        telegram.Sent.Clear();
        await inbound.HandleUpdateAsync(CreateUpdate("Continuo no Telegram"));

        Assert.Empty(telegram.Sent);
        var lastAfterCustomer = db.Messages.AsEnumerable().OrderBy(m => m.CreatedAt).Last();
        Assert.Equal(MessageSender.Customer, lastAfterCustomer.Sender);

        await humanAgent.SendMessageAsync(session.Id, agent.Id, "Olá, vou continuar seu atendimento pelo Telegram.");
        Assert.Single(telegram.Sent);
        Assert.Equal(ChatId, telegram.Sent[0].ChatId);
        Assert.Contains("Telegram", telegram.Sent[0].Text);
    }

    private static TelegramInboundService CreateInbound(
        AppDbContext db,
        ConversationService conversation,
        ITelegramService telegram)
    {
        return TestComposition.CreateTelegramInbound(db, conversation, telegram);
    }

    private static TelegramWebhookController CreateController(string secret, ITelegramInboundService inbound)
    {
        var controller = new TelegramWebhookController(
            Options.Create(new TelegramOptions { BotToken = "test-token", WebhookSecret = secret }),
            inbound,
            NullLogger<TelegramWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
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
                    Id = UserId,
                    FirstName = "Lucas",
                    Username = "lucas_claro"
                },
                Chat = new TelegramChatDto
                {
                    Id = ChatId,
                    Type = "private"
                }
            }
        };
    }

    private sealed class RecordingInboundService : ITelegramInboundService
    {
        public int Calls { get; private set; }

        public Task HandleUpdateAsync(TelegramUpdateDto update, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
