using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cia.Api.Tests;

public class HumanAgentFlowTests
{
    [Fact]
    public async Task HumanQueue_AssumeAndChat_KeepsProtocolAndHistory()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, humanAgent, _) = TestComposition.CreateServices(db);
        var agent = TestComposition.SeedAgent(db);

        var internet = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Minha internet não está funcionando."
        });

        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Já reiniciei o modem."
        });

        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Essa troca tem cobrança?"
        });

        var handoffMessage = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Quero falar com um atendente"
        });

        Assert.Equal(internet.SessionId, handoffMessage.SessionId);
        Assert.Equal(internet.Protocol, handoffMessage.Protocol);
        Assert.Equal(SessionStatus.WaitingForAgent, handoffMessage.Status);
        Assert.Equal(DepartmentType.HumanAgent, handoffMessage.CurrentDepartment);
        Assert.Equal(IntentType.HumanHandoff, handoffMessage.DetectedIntent);

        var queue = await humanAgent.GetQueueAsync();
        Assert.Single(queue);
        Assert.Equal(internet.Protocol, queue[0].Protocol);
        Assert.Equal("Lucas", queue[0].CustomerName);
        Assert.Contains(queue[0].ContextFacts, fact => fact.Contains("reiniciou", StringComparison.OrdinalIgnoreCase));

        var assumed = await humanAgent.AssumeAsync(queue[0].RequestId, agent.Id);
        Assert.Equal(HumanAgentRequestStatus.Assigned, assumed.Request.Status);
        Assert.Equal(SessionStatus.Transferred, assumed.Session.Status);
        Assert.Equal(internet.Protocol, assumed.Session.Protocol);
        Assert.Contains(assumed.Messages, m => m.Sender == MessageSender.HumanAgent);
        Assert.Contains(assumed.Messages, m => m.Sender == MessageSender.Assistant);
        Assert.Contains(assumed.Messages, m => m.Sender == MessageSender.Customer);

        var customerFollowUp = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Obrigado, estou no telefone."
        });

        Assert.Equal(internet.Protocol, customerFollowUp.Protocol);
        Assert.Equal(SessionStatus.Transferred, customerFollowUp.Status);
        Assert.Equal(MessageSender.Customer, customerFollowUp.Messages[^1].Sender);

        var history = await humanAgent.SendMessageAsync(
            internet.SessionId,
            agent.Id,
            "Olá Lucas, vi seu histórico. Você já realizou a reinicialização do modem. Vou continuar seu atendimento.");

        Assert.Contains(history, m => m.Sender == MessageSender.HumanAgent && m.Content.Contains("Vou continuar seu atendimento"));
        Assert.Equal(internet.Protocol, db.ConversationSessions.Single().Protocol);
        Assert.Equal(1, await db.HumanAgentRequests.CountAsync());
    }
}
