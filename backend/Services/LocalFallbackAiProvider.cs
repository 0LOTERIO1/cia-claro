using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Cia.Api.Services.Understanding;

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

    public Task<ConversationUnderstandingResult> UnderstandAsync(
        ConversationUnderstandingRequest request,
        CancellationToken cancellationToken = default)
    {
        var analysis = IntentRuleEngine.Analyze(request);
        var result = new ConversationUnderstandingResult
        {
            PrimaryIntent = analysis.PrimaryIntent,
            SecondaryIntents = analysis.SecondaryIntents.ToList(),
            Confidence = analysis.Confidence,
            UserMeaning = analysis.UserMeaning,
            SuggestedDepartment = SuggestDepartment(analysis.PrimaryIntent, analysis.SecondaryIntents, request),
            ExtractedFacts = analysis.ExtractedFacts,
            KnownFacts = analysis.KnownFacts,
            Inferences = analysis.Inferences,
            ContextUpdates = analysis.ContextUpdates,
            MissingInformation = analysis.MissingInformation,
            ShouldAskClarification = analysis.ShouldAskClarification,
            ClarificationQuestion = analysis.ClarificationQuestion,
            ShouldEscalate = analysis.ShouldEscalate,
            EscalationReason = analysis.EscalationReason,
            TopicChanged = analysis.TopicChanged,
            SentimentOrUrgency = analysis.SentimentOrUrgency,
            Provider = nameof(LocalFallbackAiProvider)
        };

        return Task.FromResult(result);
    }

    public Task<string> GenerateResponseAsync(
        string message,
        IntentType intent,
        ConversationContext context,
        Customer customer,
        ConversationSession session,
        CancellationToken cancellationToken = default,
        IReadOnlyList<Message>? recentMessages = null,
        ConversationUnderstandingResult? understanding = null)
    {
        if (understanding?.ShouldAskClarification == true && !string.IsNullOrWhiteSpace(understanding.ClarificationQuestion))
        {
            return Task.FromResult(understanding.ClarificationQuestion);
        }

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
                "Vou colocar você na fila de atendimento humano com o histórico completo desta jornada. Um funcionário da Claro assumirá este protocolo em instantes.",
            _ => BuildDefaultResponse(context, session, understanding)
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

    private static DepartmentType? SuggestDepartment(
        IntentType primary,
        IReadOnlyList<IntentType> secondary,
        ConversationUnderstandingRequest request)
    {
        var intents = new[] { primary }.Concat(secondary);
        if (intents.Contains(IntentType.HumanHandoff))
        {
            return DepartmentType.HumanAgent;
        }

        if (intents.Contains(IntentType.BillingQuestion))
        {
            return DepartmentType.Financial;
        }

        if (intents.Contains(IntentType.ModemReplacement) || (primary == IntentType.ModemRestarted && request.ModemRestarted))
        {
            return DepartmentType.ModemReplacement;
        }

        if (primary == IntentType.InternetProblem && request.CurrentDepartment == DepartmentType.Triage)
        {
            return DepartmentType.TechnicalSupport;
        }

        return null;
    }

    private static string BuildInternetResponse(ConversationContext context, ConversationSession session)
    {
        if (context.ModemRestarted)
        {
            return "Vi que o problema de internet já está registrado e o modem já foi reiniciado. Não preciso que você repita isso. Vamos continuar a partir da falha persistente.";
        }

        if (session.CurrentDepartment == DepartmentType.TechnicalSupport)
        {
            return "Vou direcionar seu atendimento para o suporte técnico e manter as informações que você já forneceu. Você já reiniciou o modem?";
        }

        return "Vou direcionar seu atendimento para o suporte técnico e manter as informações que você já forneceu.";
    }

    private static string BuildModemRestartedResponse(ConversationContext context, ConversationSession session)
    {
        if (session.CurrentDepartment == DepartmentType.ModemReplacement || context.ModemRestarted)
        {
            return "Vi que sua internet continua sem funcionar mesmo após a reinicialização do modem. Vou continuar seu atendimento verificando a possibilidade de substituição do equipamento.";
        }

        return "Registrei que o modem já foi reiniciado e o problema continua.";
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
            return $"Continuando em {area}: você estava tratando de uma falha na internet residencial e já realizou a reinicialização do modem. Vamos seguir a partir daqui.";
        }

        if (context.IssueType == IssueType.InternetConnection)
        {
            return $"Continuando em {area}: identifiquei que o problema original é a internet residencial. Você não precisa repetir essas informações.";
        }

        return $"Recuperei o contexto da sessão e vamos continuar em {area}.";
    }

    private static string BuildDefaultResponse(
        ConversationContext context,
        ConversationSession session,
        ConversationUnderstandingResult? understanding)
    {
        if (context.ModemRestarted)
        {
            return $"Estou no {DepartmentNames.Format(session.CurrentDepartment)} com o histórico já registrado. O modem já foi reiniciado e a internet continuou sem funcionar. Como posso seguir?";
        }

        if (understanding?.ShouldAskClarification == true && !string.IsNullOrWhiteSpace(understanding.ClarificationQuestion))
        {
            return understanding.ClarificationQuestion;
        }

        if (context.IssueType == IssueType.InternetConnection)
        {
            return "Como a tentativa anterior ainda está no histórico, me diga se a conexão voltou ou se o modem continua sem sinal.";
        }

        return "Você está falando de conexão, de um equipamento ou de uma cobrança?";
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
        var text = TextNormalizer.Normalize(message);
        return text.Contains("ja tentou reiniciar", StringComparison.Ordinal)
            || text.Contains("ja reiniciou o modem", StringComparison.Ordinal);
    }
}
