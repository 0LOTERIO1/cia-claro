using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Services;

namespace Cia.Api.Tests;

public class TelegramTemporaryReuseTests
{
    private const long TelegramUserId = 123;
    private const long TelegramChatId = 123;
    private const string TemporaryCustomerId = "TG-123";

    [Fact]
    public async Task AfterUnlink_SameTelegramReusesOrphanTemporaryCustomer_WithoutDuplicateKey()
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

        var temporary = Assert.Single(db.Customers.Where(c => c.Id == TemporaryCustomerId));
        Assert.Equal(TelegramUserId, temporary.TelegramUserId);
        var originalSession = Assert.Single(db.ConversationSessions);
        Assert.Equal(TemporaryCustomerId, originalSession.CustomerId);
        var originalSessionId = originalSession.Id;
        var originalProtocol = originalSession.Protocol;
        var originalMessageCount = db.Messages.Count(m => m.SessionId == originalSessionId);

        var firstLink = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.PedroCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {firstLink.Code}"));

        Assert.Equal(DbSeeder.PedroCustomerId, db.ConversationSessions.Single(s => s.Id == originalSessionId).CustomerId);
        var orphan = db.Customers.Single(c => c.Id == TemporaryCustomerId);
        Assert.Null(orphan.TelegramUserId);
        Assert.Null(orphan.TelegramChatId);
        Assert.DoesNotContain(db.CustomerChannelIdentities, i => i.CustomerId == TemporaryCustomerId);

        await identities.UnlinkTelegramAsync(DbSeeder.PedroCustomerId);
        Assert.DoesNotContain(
            db.CustomerChannelIdentities,
            i => i.CustomerId == DbSeeder.PedroCustomerId && i.Channel == ChannelType.Telegram);

        telegram.Sent.Clear();
        var exception = await Record.ExceptionAsync(() => inbound.HandleUpdateAsync(CreateUpdate("oi")));
        Assert.Null(exception);

        Assert.Equal(1, db.Customers.Count(c => c.Id == TemporaryCustomerId));
        var reused = db.Customers.Single(c => c.Id == TemporaryCustomerId);
        Assert.Equal(TelegramUserId, reused.TelegramUserId);
        Assert.Equal(TelegramChatId, reused.TelegramChatId);
        Assert.Contains(
            db.CustomerChannelIdentities,
            i => i.CustomerId == TemporaryCustomerId
                 && i.Channel == ChannelType.Telegram
                 && i.ExternalUserId == TelegramUserId.ToString());

        var temporarySession = Assert.Single(db.ConversationSessions.Where(s => s.CustomerId == TemporaryCustomerId));
        Assert.NotEqual(originalSessionId, temporarySession.Id);
        Assert.Equal(ChannelType.Telegram, temporarySession.CurrentChannel);
        Assert.NotEmpty(telegram.Sent);
        Assert.Equal(TelegramChatId, telegram.Sent[0].ChatId);
        Assert.False(string.IsNullOrWhiteSpace(telegram.Sent[0].Text));

        var portalSession = db.ConversationSessions.Single(s => s.Id == originalSessionId);
        Assert.Equal(DbSeeder.PedroCustomerId, portalSession.CustomerId);
        Assert.Equal(originalProtocol, portalSession.Protocol);
        Assert.Equal(originalMessageCount, db.Messages.Count(m => m.SessionId == originalSessionId));

        var secondLink = await identities.GenerateTelegramLinkCodeAsync(DbSeeder.PedroCustomerId);
        await inbound.HandleUpdateAsync(CreateUpdate($"/link {secondLink.Code}"));

        Assert.Equal(DbSeeder.PedroCustomerId, db.ConversationSessions.Single(s => s.Id == temporarySession.Id).CustomerId);
        Assert.Equal(DbSeeder.PedroCustomerId, db.ConversationSessions.Single(s => s.Id == originalSessionId).CustomerId);
        Assert.Equal(DbSeeder.PedroCustomerId, db.CustomerChannelIdentities.Single(i => i.Channel == ChannelType.Telegram).CustomerId);
    }

    private static TelegramUpdateDto CreateUpdate(string text)
    {
        return new TelegramUpdateDto
        {
            UpdateId = 77,
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
