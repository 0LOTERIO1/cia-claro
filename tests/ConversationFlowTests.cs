using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Enums;
using Cia.Api.Services;

namespace Cia.Api.Tests;

public class ConversationFlowTests
{
    [Fact]
    public async Task FullOmnichannelFlow_PersistsContextAndKeepsProtocol()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, handoff, _) = TestComposition.CreateServices(db);

        var first = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Minha internet não está funcionando."
        });

        Assert.StartsWith("CIA-", first.Protocol);
        Assert.Equal(SessionStatus.Active, first.Status);
        Assert.Equal(IntentType.InternetProblem, first.DetectedIntent);
        Assert.Equal(IssueType.InternetConnection, first.Context?.IssueType);
        Assert.False(first.Context?.ModemRestarted);
        Assert.Contains("reiniciar o modem", first.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, first.Messages.Count);

        var second = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Sim, já reiniciei o modem e continua sem internet."
        });

        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(first.Protocol, second.Protocol);
        Assert.Equal(IntentType.ModemRestarted, second.DetectedIntent);
        Assert.True(second.Context?.ModemRestarted);
        Assert.Equal(IssueType.InternetConnection, second.Context?.IssueType);

        var persistedContext = db.ConversationContexts.Single(c => c.SessionId == first.SessionId);
        Assert.Equal(IssueType.InternetConnection, persistedContext.IssueType);
        Assert.True(persistedContext.ModemRestarted);

        var switched = await conversation.ChangeChannelAsync(first.SessionId, ChannelType.WhatsApp);
        Assert.Equal(first.SessionId, switched.Id);
        Assert.Equal(first.Protocol, switched.Protocol);
        Assert.Equal(ChannelType.AppClaro, switched.InitialChannel);
        Assert.Equal(ChannelType.WhatsApp, switched.CurrentChannel);
        Assert.True(switched.ContextRestored);

        var continued = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WhatsApp,
            Content = "Quero continuar meu atendimento."
        });

        Assert.Equal(first.SessionId, continued.SessionId);
        Assert.Equal(first.Protocol, continued.Protocol);
        Assert.True(continued.ContextRestored);
        Assert.Contains("internet", continued.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("modem", continued.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);

        var createdHandoff = await handoff.CreateHandoffAsync(first.SessionId);
        Assert.Contains("CLIENTE-001", createdHandoff.Summary);
        Assert.Contains(first.Protocol, createdHandoff.Summary);
        Assert.Contains("reiniciou o modem", createdHandoff.Summary, StringComparison.OrdinalIgnoreCase);

        var session = await conversation.GetSessionAsync(first.SessionId);
        Assert.Equal(SessionStatus.Transferred, session.Status);
        Assert.Equal(ChannelType.WhatsApp, session.CurrentChannel);
        Assert.Equal(first.Protocol, session.Protocol);
    }
}
