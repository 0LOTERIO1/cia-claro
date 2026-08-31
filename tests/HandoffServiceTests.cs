using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Enums;

namespace Cia.Api.Tests;

public class HandoffServiceTests
{
    [Fact]
    public async Task CreateHandoff_BuildsJourneySummary()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, handoff, _) = TestComposition.CreateServices(db);

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
            Content = "Já reiniciei o modem."
        });

        var session = (await conversation.GetSessionsByCustomerAsync(DbSeeder.DemoCustomerId))[0];
        var result = await handoff.CreateHandoffAsync(session.Id);

        Assert.Contains("Lucas", result.Summary);
        Assert.Contains("CLIENTE-001", result.Summary);
        Assert.Contains("Internet", result.Summary);
        Assert.Contains("Reinicialização do modem", result.Summary);
        Assert.Contains("Triagem", result.Summary);

        var updated = await conversation.GetSessionAsync(session.Id);
        Assert.Equal(SessionStatus.Transferred, updated.Status);
        Assert.Equal(session.Protocol, updated.Protocol);
        Assert.Equal(DepartmentType.HumanAgent, updated.CurrentDepartment);
    }
}
