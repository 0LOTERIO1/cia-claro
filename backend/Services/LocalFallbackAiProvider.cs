using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;

namespace Cia.Api.Services;

public class LocalFallbackAiProvider : IAiProvider
{
    private readonly IIntentService _intentService;

    public LocalFallbackAiProvider(IIntentService intentService)
    {
        _intentService = intentService;
    }

    public Task<IntentType> AnalyzeIntentAsync(string message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_intentService.Detect(message));
    }

    public Task<string> GenerateResponseAsync(
        string message,
        IntentType intent,
        ConversationContext context,
        Customer customer,
        ConversationSession session,
        CancellationToken cancellationToken = default)
    {
        var response = intent switch
        {
            IntentType.Greeting =>
                $"Olá, {customer.Name}. Sou a CIA, a assistente da Claro. Como posso ajudar?",
            IntentType.InternetProblem =>
                "Entendi. Vamos verificar sua conexão. Você já tentou reiniciar o modem?",
            IntentType.ModemRestarted =>
                "Entendido. Registrei que o modem já foi reiniciado e o problema continua.",
            IntentType.ContinueSupport => BuildContinueResponse(context),
            IntentType.HumanHandoff =>
                "Vou encaminhar seu atendimento para um especialista. Estou gerando o resumo do que já conversamos.",
            _ => "Recebi sua mensagem. Pode me contar um pouco mais para eu continuar o atendimento?"
        };

        return Task.FromResult(response);
    }

    public Task<string> GenerateHandoffSummaryAsync(
        Customer customer,
        ConversationSession session,
        ConversationContext context,
        IReadOnlyList<Message> messages,
        CancellationToken cancellationToken = default)
    {
        var procedures = context.ModemRestarted
            ? "* Cliente já reiniciou o modem"
            : "* Nenhum procedimento técnico confirmado";

        var issue = context.IssueType == IssueType.InternetConnection
            ? "Internet residencial sem conexão"
            : "Não identificado";

        var summary =
            $"""
            Resumo do atendimento

            Cliente: {customer.Name}
            Customer ID: {customer.Id}
            Protocolo: {session.Protocol}
            Canal inicial: {FormatChannel(session.InitialChannel)}
            Canal atual: {FormatChannel(session.CurrentChannel)}
            Problema identificado: {issue}
            Procedimentos realizados:
            {procedures}
            Status:
            Encaminhado para atendimento humano
            """;

        return Task.FromResult(summary.Trim());
    }

    private static string BuildContinueResponse(ConversationContext context)
    {
        if (context.IssueType == IssueType.InternetConnection && context.ModemRestarted)
        {
            return "Claro. Identifiquei que você estava tratando de uma falha na sua internet residencial e que já realizou a reinicialização do modem. Vamos continuar a partir daqui.";
        }

        if (context.IssueType == IssueType.InternetConnection)
        {
            return "Claro. Identifiquei que você estava tratando de uma falha na sua internet residencial. Vamos continuar a partir daqui.";
        }

        return "Claro. Recuperei sua sessão anterior. Vamos continuar o atendimento.";
    }

    private static string FormatChannel(ChannelType channel) => channel switch
    {
        ChannelType.AppClaro => "App Claro",
        ChannelType.WhatsApp => "WhatsApp",
        _ => channel.ToString()
    };
}
