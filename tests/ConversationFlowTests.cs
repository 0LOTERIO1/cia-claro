using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Enums;

namespace Cia.Api.Tests;

public class ConversationFlowTests
{
    [Fact]
    public async Task DepartmentJourney_PreservesSessionProtocolAndContext()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, handoff, _, _) = TestComposition.CreateServices(db);

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
        Assert.Equal(DepartmentType.Triage, first.PreviousDepartment);
        Assert.Equal(DepartmentType.TechnicalSupport, first.CurrentDepartment);
        Assert.True(first.DepartmentChanged);
        Assert.False(first.Context?.ModemRestarted);
        Assert.NotNull(first.AssistantMessage);
        Assert.Contains("suporte técnico", first.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reiniciou o modem", first.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);

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
        Assert.True(second.Context?.InternetStillDown);
        Assert.Equal(DepartmentType.ModemReplacement, second.CurrentDepartment);
        Assert.Equal(DepartmentType.TechnicalSupport, second.PreviousDepartment);
        Assert.NotNull(second.AssistantMessage);
        Assert.DoesNotContain("Você já tentou reiniciar o modem?", second.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reinicialização do modem", second.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);

        var persistedContext = db.ConversationContexts.Single(c => c.SessionId == first.SessionId);
        Assert.Equal(IssueType.InternetConnection, persistedContext.IssueType);
        Assert.True(persistedContext.ModemRestarted);
        Assert.True(persistedContext.InternetStillDown);

        var billing = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Essa troca vai gerar alguma cobrança?"
        });

        Assert.Equal(first.SessionId, billing.SessionId);
        Assert.Equal(first.Protocol, billing.Protocol);
        Assert.Equal(DepartmentType.Financial, billing.CurrentDepartment);
        Assert.Equal(IntentType.BillingQuestion, billing.DetectedIntent);
        Assert.NotNull(billing.AssistantMessage);
        Assert.Contains("cobrança", billing.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("modem", billing.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.True(billing.Messages.Count >= 6);

        var createdHandoff = await handoff.CreateHandoffAsync(first.SessionId);
        Assert.Contains("CLIENTE-001", createdHandoff.Summary);
        Assert.Contains(first.Protocol, createdHandoff.Summary);
        Assert.Contains("Triagem", createdHandoff.Summary);
        Assert.Contains("Suporte Técnico", createdHandoff.Summary);
        Assert.Contains("Troca de Modem", createdHandoff.Summary);
        Assert.Contains("Financeiro", createdHandoff.Summary);
        Assert.Contains("Reinicialização do modem", createdHandoff.Summary);

        var session = await conversation.GetSessionAsync(first.SessionId);
        Assert.Equal(SessionStatus.WaitingForAgent, session.Status);
        Assert.Equal(first.Protocol, session.Protocol);
        Assert.Equal(first.SessionId, session.Id);
        Assert.Equal(DepartmentType.HumanAgent, session.CurrentDepartment);
    }

    [Fact]
    public async Task DoesNotAskModemRestartAgain_WhenAlreadyRecorded()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, _, _) = TestComposition.CreateServices(db);

        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Minha internet não está funcionando."
        });

        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Já reiniciei o modem e continua sem funcionar."
        });

        var third = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Minha internet não está funcionando."
        });

        Assert.True(third.Context?.ModemRestarted);
        Assert.NotNull(third.AssistantMessage);
        Assert.DoesNotContain("Você já tentou reiniciar o modem?", third.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Você já reiniciou o modem?", third.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(third.SessionId, third.SessionId);
    }
}
