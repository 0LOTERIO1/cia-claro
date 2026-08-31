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
        if (context.ModemRestarted && LooksLikeModemRestartQuestion(message))
        {
            return Task.FromResult(
                "Esse procedimento já está registrado. Você já reiniciou o modem e o problema de internet persistiu. Vamos continuar a partir daqui.");
        }

        var response = intent switch
        {
            IntentType.Greeting =>
                $"Olá, {customer.Name}. Sou a CIA, a camada central de atendimento da Claro. Como posso ajudar?",
            IntentType.InternetProblem => BuildInternetResponse(context, session),
            IntentType.ModemRestarted => BuildModemRestartedResponse(context, session),
            IntentType.ModemReplacement => BuildModemReplacementResponse(context),
            IntentType.BillingQuestion => BuildFinancialResponse(context, session),
            IntentType.ContinueSupport => BuildContinueResponse(context, session),
            IntentType.HumanHandoff =>
                "Vou encaminhar seu atendimento para um especialista com o resumo completo da jornada. Estou gerando o histórico do que já conversamos.",
            _ => BuildDefaultResponse(context, session)
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
        var journey = BuildJourney(session);
        var procedures = context.ModemRestarted
            ? "* Reinicialização do modem"
            : "* Nenhum procedimento técnico confirmado";
        var result = context.InternetStillDown
            ? "* Problema persistiu"
            : "* Resultado ainda em avaliação";
        var issue = context.OriginalProblem
            ?? (context.IssueType == IssueType.InternetConnection
                ? "Internet sem conexão"
                : "Não identificado");

        var summary =
            $"""
            Resumo do atendimento

            Cliente: {customer.Name}
            Customer ID: {customer.Id}
            Protocolo: {session.Protocol}
            Problema original: {issue}
            Jornada:
            {journey}
            Procedimentos realizados:
            {procedures}
            Resultado:
            {result}
            Contexto atual:
            * {context.ContextSummary ?? context.CurrentRequest ?? "Atendimento em andamento"}
            Status:
            Transferido para atendimento humano
            """;

        return Task.FromResult(summary.Trim());
    }

    private static string BuildInternetResponse(ConversationContext context, ConversationSession session)
    {
        if (context.ModemRestarted)
        {
            return "Vi que o problema de internet já está registrado e o modem já foi reiniciado. Não preciso que você repita isso. Vamos continuar a partir da falha persistente.";
        }

        if (session.CurrentDepartment == DepartmentType.TechnicalSupport)
        {
            return "Entendi. Vou direcionar seu atendimento para o suporte técnico e manter as informações que você já forneceu. Você já reiniciou o modem?";
        }

        return "Entendi. Vou direcionar seu atendimento para o suporte técnico e manter as informações que você já forneceu.";
    }

    private static string BuildModemRestartedResponse(ConversationContext context, ConversationSession session)
    {
        if (session.CurrentDepartment == DepartmentType.ModemReplacement || context.ModemRestarted)
        {
            return "Vi que sua internet continua sem funcionar mesmo após a reinicialização do modem. Vou continuar seu atendimento verificando a possibilidade de substituição do equipamento.";
        }

        return "Entendido. Registrei que o modem já foi reiniciado e o problema continua.";
    }

    private static string BuildModemReplacementResponse(ConversationContext context)
    {
        if (context.ModemRestarted)
        {
            return "Vi que sua internet continua sem funcionar mesmo após a reinicialização do modem. Vou continuar seu atendimento verificando a possibilidade de substituição do equipamento.";
        }

        return "Vou verificar a possibilidade de substituição do modem com o contexto já registrado nesta sessão.";
    }

    private static string BuildFinancialResponse(ConversationContext context, ConversationSession session)
    {
        var origin = session.PreviousDepartment.HasValue
            ? DepartmentNames.Format(session.PreviousDepartment.Value)
            : "atendimento técnico";

        return $"Você veio do atendimento de {origin} referente à falha de conexão e possível troca do modem. Vou continuar a partir desse ponto para verificar a questão de cobrança.";
    }

    private static string BuildContinueResponse(ConversationContext context, ConversationSession session)
    {
        var area = DepartmentNames.Format(session.CurrentDepartment);
        if (context.IssueType == IssueType.InternetConnection && context.ModemRestarted)
        {
            return $"Claro. Continuando em {area}: você estava tratando de uma falha na internet residencial e já realizou a reinicialização do modem. Vamos seguir a partir daqui.";
        }

        if (context.IssueType == IssueType.InternetConnection)
        {
            return $"Claro. Continuando em {area}: identifiquei que o problema original é a internet residencial. Você não precisa repetir essas informações.";
        }

        return $"Claro. Recuperei o contexto da sessão e vamos continuar em {area}.";
    }

    private static string BuildDefaultResponse(ConversationContext context, ConversationSession session)
    {
        if (context.ModemRestarted)
        {
            return $"Estou no {DepartmentNames.Format(session.CurrentDepartment)} com o histórico já registrado. O modem já foi reiniciado e a internet continuou sem funcionar. Como posso seguir?";
        }

        return "Recebi sua mensagem. Pode me contar um pouco mais para eu continuar o atendimento?";
    }

    private static string BuildJourney(ConversationSession session)
    {
        var steps = new List<string> { DepartmentNames.Format(DepartmentType.Triage) };
        foreach (var transfer in session.Transfers.OrderBy(t => t.CreatedAt))
        {
            var name = DepartmentNames.Format(transfer.ToDepartment);
            if (!steps.Contains(name))
            {
                steps.Add(name);
            }
        }

        if (!steps.Contains(DepartmentNames.Format(session.CurrentDepartment)))
        {
            steps.Add(DepartmentNames.Format(session.CurrentDepartment));
        }

        return string.Join("\n→ ", steps);
    }

    private static bool LooksLikeModemRestartQuestion(string message)
    {
        var text = message.ToLowerInvariant();
        return text.Contains("já tentou reiniciar", StringComparison.Ordinal)
            || text.Contains("ja tentou reiniciar", StringComparison.Ordinal);
    }
}
