using Cia.Api.Enums;
using Cia.Api.Services;

namespace Cia.Api.Tests;

public class IntentServiceTests
{
    private readonly IntentService _service = new();

    [Theory]
    [InlineData("Minha internet não está funcionando.", IntentType.InternetProblem)]
    [InlineData("Sim, já reiniciei o modem e continua sem internet.", IntentType.ModemRestarted)]
    [InlineData("Preciso trocar o modem", IntentType.ModemReplacement)]
    [InlineData("Essa troca vai ser cobrada?", IntentType.BillingQuestion)]
    [InlineData("Quero continuar meu atendimento.", IntentType.ContinueSupport)]
    [InlineData("Quero falar com um atendente.", IntentType.HumanHandoff)]
    [InlineData("Olá", IntentType.Greeting)]
    [InlineData("abc xyz", IntentType.Unknown)]
    public void Detect_ReturnsExpectedIntent(string message, IntentType expected)
    {
        Assert.Equal(expected, _service.Detect(message));
    }
}
