using Cia.Api.DTOs;
using Cia.Api.Enums;

namespace Cia.Api.Services.Understanding;

public sealed class IntentRule
{
    public IntentType Intent { get; init; }
    public double Weight { get; init; }
    public IReadOnlyList<string> Patterns { get; init; } = Array.Empty<string>();
}

public sealed class IntentAnalysis
{
    public IntentType PrimaryIntent { get; init; } = IntentType.Unknown;
    public IReadOnlyList<IntentType> SecondaryIntents { get; init; } = Array.Empty<IntentType>();
    public double Confidence { get; init; }
    public IReadOnlyDictionary<IntentType, double> Scores { get; init; } = new Dictionary<IntentType, double>();
    public Dictionary<string, string> ExtractedFacts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> KnownFacts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Inferences { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public ContextUpdatesDto ContextUpdates { get; init; } = new();
    public bool ShouldAskClarification { get; init; }
    public string? ClarificationQuestion { get; init; }
    public bool ShouldEscalate { get; init; }
    public string? EscalationReason { get; init; }
    public bool TopicChanged { get; init; }
    public string? UserMeaning { get; init; }
    public string? SentimentOrUrgency { get; init; }
    public List<string> MissingInformation { get; init; } = new();
}

public static class IntentRuleEngine
{
    private static readonly IntentType[] Priority =
    {
        IntentType.HumanHandoff,
        IntentType.BillingQuestion,
        IntentType.ModemReplacement,
        IntentType.ContinueSupport,
        IntentType.ModemRestarted,
        IntentType.InternetProblem,
        IntentType.Greeting,
        IntentType.Unknown
    };

    private static readonly IntentRule[] Rules =
    {
        new()
        {
            Intent = IntentType.HumanHandoff,
            Weight = 0.96,
            Patterns = new[]
            {
                "atendente", "humano", "falar com um atendente", "falar com atendente",
                "transbordo", "pessoa real", "falar com alguem", "quero falar com alguem",
                "atendimento humano"
            }
        },
        new()
        {
            Intent = IntentType.BillingQuestion,
            Weight = 0.9,
            Patterns = new[]
            {
                "cobranca", "cobrada", "cobrado", "vai ser cobrada", "gerar cobranca",
                "financeiro", "fatura", "valor extra", "preco", "preço", "quanto custa",
                "quanto vai ficar", "quanto fica", "vai ficar cara", "taxas"
            }
        },
        new()
        {
            Intent = IntentType.ModemReplacement,
            Weight = 0.88,
            Patterns = new[]
            {
                "trocar o modem", "troca do modem", "troca de modem", "substituicao",
                "substituir o modem", "preciso trocar", "troca do equipamento", "avaliacao de troca"
            }
        },
        new()
        {
            Intent = IntentType.ContinueSupport,
            Weight = 0.82,
            Patterns = new[]
            {
                "continuar meu atendimento", "continuar o atendimento", "continuar atendimento",
                "quero continuar", "e agora", "podemos seguir", "pode seguir"
            }
        },
        new()
        {
            Intent = IntentType.ModemRestarted,
            Weight = 0.92,
            Patterns = new[]
            {
                "reiniciei", "ja reiniciei", "reiniciar o modem", "reiniciei o modem",
                "ja reiniciei o modem", "reiniciei duas", "ja tentei reiniciar"
            }
        },
        new()
        {
            Intent = IntentType.InternetProblem,
            Weight = 0.8,
            Patterns = new[]
            {
                "internet", "conexao", "wifi", "sem sinal", "sem conexao",
                "internet caiu", "internet morreu", "fora do ar", "sem internet"
            }
        },
        new()
        {
            Intent = IntentType.Greeting,
            Weight = 0.62,
            Patterns = new[] { "ola", "oi", "bom dia", "boa tarde", "boa noite", "eai", "e ai" }
        }
    };

    public static IntentType Detect(string message) => Analyze(CreateBareRequest(message)).PrimaryIntent;

    public static IntentAnalysis Analyze(ConversationUnderstandingRequest request)
    {
        var message = TextNormalizer.Normalize(request.CurrentMessage);
        var scores = new Dictionary<IntentType, double>();

        foreach (var rule in Rules)
        {
            if (rule.Patterns.Any(pattern => MatchesPattern(message, pattern)))
            {
                AddScore(scores, rule.Intent, rule.Weight);
            }
        }

        ApplyConversationalCues(message, request, scores);

        if (LooksLikeGreetingOnly(message, scores))
        {
            scores[IntentType.Greeting] = Math.Max(scores.GetValueOrDefault(IntentType.Greeting), 0.78);
        }

        var ranked = scores
            .Where(pair => pair.Value >= 0.28)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => Array.IndexOf(Priority, pair.Key))
            .Select(pair => pair.Key)
            .ToList();

        if (ranked.Count == 0)
        {
            ranked.Add(IntentType.Unknown);
        }

        var primary = ranked[0];
        var secondary = ranked.Skip(1).Where(intent => intent != IntentType.Unknown && intent != IntentType.Greeting).Distinct().ToList();
        var confidence = scores.GetValueOrDefault(primary, primary == IntentType.Unknown ? 0.2 : 0.5);
        confidence = Math.Clamp(confidence, 0, 1);

        var facts = FactExtractor.Extract(message, request);
        var topicChanged = DetectTopicChange(primary, request);
        var clarification = BuildClarification(message, request, primary, confidence, facts, scores);
        var escalate = primary == IntentType.HumanHandoff
                       || TextNormalizer.ContainsAny(message, "quero falar com alguem", "falar com alguem", "atendente");

        if (escalate)
        {
            primary = IntentType.HumanHandoff;
            confidence = Math.Max(confidence, 0.9);
        }

        var updates = BuildUpdates(primary, secondary, facts, request, topicChanged);

        return new IntentAnalysis
        {
            PrimaryIntent = primary,
            SecondaryIntents = secondary,
            Confidence = clarification.ShouldAsk ? Math.Min(confidence, 0.4) : confidence,
            Scores = scores,
            ExtractedFacts = facts.Extracted,
            KnownFacts = facts.Known,
            Inferences = facts.Inferences,
            ContextUpdates = updates,
            ShouldAskClarification = clarification.ShouldAsk,
            ClarificationQuestion = clarification.Question,
            ShouldEscalate = escalate,
            EscalationReason = escalate ? "Cliente pediu atendimento humano" : null,
            TopicChanged = topicChanged,
            UserMeaning = BuildMeaning(message, primary, request),
            SentimentOrUrgency = DetectSentiment(message),
            MissingInformation = clarification.Missing
        };
    }

    private static ConversationUnderstandingRequest CreateBareRequest(string message) => new()
    {
        CurrentMessage = message,
        Channel = ChannelType.WebPortal,
        CurrentDepartment = DepartmentType.Triage,
        SessionStatus = SessionStatus.Active
    };

    private static void ApplyConversationalCues(
        string message,
        ConversationUnderstandingRequest request,
        Dictionary<IntentType, double> scores)
    {
        var lastQuestion = TextNormalizer.Normalize(request.LastAssistantQuestion ?? LastAssistantContent(request));
        var affirmation = IsAffirmation(message);
        var negation = IsNegation(message);
        var persistence = IsPersistence(message);
        var continuation = IsContinuation(message);

        if (affirmation && LooksLikeModemRestartQuestion(lastQuestion))
        {
            AddScore(scores, IntentType.ModemRestarted, 0.94);
        }

        if (affirmation && LooksLikeBillingQuestion(lastQuestion))
        {
            AddScore(scores, IntentType.BillingQuestion, 0.86);
        }

        if (affirmation && LooksLikeReplacementQuestion(lastQuestion))
        {
            AddScore(scores, IntentType.ModemReplacement, 0.86);
        }

        if (negation && LooksLikeReplacementQuestion(lastQuestion))
        {
            AddScore(scores, IntentType.HumanHandoff, 0.55);
            AddScore(scores, IntentType.ContinueSupport, 0.6);
        }

        if (persistence && (request.IssueType == IssueType.InternetConnection || request.ModemRestarted || HasInternetMemory(request)))
        {
            AddScore(scores, request.ModemRestarted ? IntentType.ModemRestarted : IntentType.InternetProblem, 0.84);
            AddScore(scores, IntentType.ContinueSupport, 0.5);
        }

        if (continuation && HasUsefulContext(request))
        {
            AddScore(scores, IntentType.ContinueSupport, 0.8);
        }

        if (IsCostQuestion(message) && (request.CurrentRequest?.Contains("troca", StringComparison.OrdinalIgnoreCase) == true
            || HasFact(request, "replacement_requested")
            || lastQuestion.Contains("troca", StringComparison.Ordinal)))
        {
            AddScore(scores, IntentType.BillingQuestion, 0.9);
        }
    }

    private static bool LooksLikeGreetingOnly(string message, Dictionary<IntentType, double> scores)
    {
        if (message.Length > 24)
        {
            return false;
        }

        return scores.Keys.All(key => key is IntentType.Greeting or IntentType.Unknown)
               && TextNormalizer.ContainsAny(message, "ola", "oi", "bom dia", "boa tarde", "boa noite");
    }

    private static bool MatchesPattern(string message, string pattern)
    {
        var normalizedPattern = TextNormalizer.Normalize(pattern);
        if (string.IsNullOrWhiteSpace(normalizedPattern))
        {
            return false;
        }

        if (normalizedPattern.Length <= 3)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                message,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(normalizedPattern)}\b");
        }

        return message.Contains(normalizedPattern, StringComparison.Ordinal);
    }

    private static ContextUpdatesDto BuildUpdates(
        IntentType primary,
        IReadOnlyList<IntentType> secondary,
        FactBundle facts,
        ConversationUnderstandingRequest request,
        bool topicChanged)
    {
        var updates = new ContextUpdatesDto();
        var intents = new[] { primary }.Concat(secondary).ToHashSet();

        if (intents.Contains(IntentType.InternetProblem)
            || intents.Contains(IntentType.ModemRestarted)
            || intents.Contains(IntentType.ModemReplacement)
            || facts.Known.ContainsKey("modem_restart_attempted"))
        {
            updates.IssueType = IssueType.InternetConnection;
            updates.OriginalProblem ??= string.IsNullOrWhiteSpace(request.OriginalProblem)
                ? "Internet residencial sem conexão"
                : request.OriginalProblem;
        }

        if (intents.Contains(IntentType.InternetProblem))
        {
            updates.CurrentRequest = "Falha de conexão de internet";
            updates.FactsToAppend.Add("Problema original: internet sem conexão");
        }

        if (intents.Contains(IntentType.ModemRestarted) || facts.Known.GetValueOrDefault("modem_restart_attempted") == "true")
        {
            updates.ModemRestarted = true;
            updates.InternetStillDown = true;
            updates.TroubleshootingPerformed = "Cliente já reiniciou o modem";
            updates.CurrentRequest = "Internet continua sem funcionar após reinício do modem";
            updates.FactsToAppend.Add("Modem já foi reiniciado");
            updates.FactsToAppend.Add("Problema persistiu após o procedimento");
        }

        if (intents.Contains(IntentType.ModemReplacement))
        {
            updates.CurrentRequest = "Avaliação de substituição do modem";
            updates.FactsToAppend.Add("Cliente solicitou ou foi encaminhado para troca de modem");
        }

        if (intents.Contains(IntentType.BillingQuestion))
        {
            updates.CurrentRequest = "Dúvida sobre cobrança da troca de equipamento";
            updates.FactsToAppend.Add("Cliente perguntou se a troca do modem gera cobrança");
        }

        if (facts.Known.GetValueOrDefault("issue_persists") == "true")
        {
            updates.InternetStillDown = true;
            updates.FactsToAppend.Add("Problema persistiu após o procedimento");
        }

        if (topicChanged && intents.Contains(IntentType.BillingQuestion))
        {
            updates.CurrentRequest = "Dúvida sobre cobrança da troca de equipamento";
        }

        return updates;
    }

    private static (bool ShouldAsk, string? Question, List<string> Missing) BuildClarification(
        string message,
        ConversationUnderstandingRequest request,
        IntentType primary,
        double confidence,
        FactBundle facts,
        Dictionary<IntentType, double> scores)
    {
        var missing = new List<string>();
        if (primary == IntentType.Unknown && !HasUsefulContext(request) && message.Length > 0 && !IsAffirmation(message) && !IsNegation(message) && !IsPersistence(message))
        {
            missing.Add("assunto");
            return (true, "Você está falando de conexão, de um equipamento ou de uma cobrança?", missing);
        }

        var hasInternet = HasInternetMemory(request) || scores.ContainsKey(IntentType.InternetProblem);
        var hasBilling = scores.ContainsKey(IntentType.BillingQuestion) || HasFact(request, "billing_question");
        var hasReplacement = scores.ContainsKey(IntentType.ModemReplacement) || HasFact(request, "replacement_requested");

        if (IsCostQuestion(message) && hasInternet && !hasBilling && request.CurrentDepartment != DepartmentType.Financial)
        {
            missing.Add("alvo_da_cobranca");
            return (true, "Você quer saber se a troca do equipamento gera cobrança ou se há outro valor na fatura?", missing);
        }

        if (IsAmbiguousReference(message) && hasInternet && hasBilling)
        {
            missing.Add("topico_atual");
            return (true, "Você está falando da troca do modem ou da cobrança?", missing);
        }

        if (IsPersistence(message)
            && hasInternet
            && string.IsNullOrWhiteSpace(request.TroubleshootingPerformed)
            && !request.ModemRestarted
            && facts.Known.GetValueOrDefault("modem_restart_attempted") != "true"
            && primary != IntentType.ModemRestarted)
        {
            missing.Add("procedimento_realizado");
            return (true, "Quando você diz que não funcionou, o modem continua sem conexão?", missing);
        }

        if (confidence < 0.42 && primary is IntentType.Unknown or IntentType.ContinueSupport && HasUsefulContext(request))
        {
            if (hasInternet && hasBilling)
            {
                missing.Add("topico_atual");
                return (true, "Você está falando da troca do modem ou da cobrança?", missing);
            }

            missing.Add("confirmacao");
            return (true, "Você quer continuar o atendimento atual ou falar de outro assunto?", missing);
        }

        return (false, null, missing);
    }

    private static bool DetectTopicChange(IntentType primary, ConversationUnderstandingRequest request)
    {
        if (request.LastDetectedIntent is null or IntentType.Unknown or IntentType.Greeting or IntentType.ContinueSupport)
        {
            return false;
        }

        if (primary is IntentType.Unknown or IntentType.Greeting or IntentType.ContinueSupport or IntentType.ModemRestarted)
        {
            return false;
        }

        return primary != request.LastDetectedIntent;
    }

    private static string BuildMeaning(string message, IntentType primary, ConversationUnderstandingRequest request)
    {
        if (IsAffirmation(message) && LooksLikeModemRestartQuestion(TextNormalizer.Normalize(request.LastAssistantQuestion)))
        {
            return "Cliente confirmou que já reiniciou o modem.";
        }

        if (IsPersistence(message))
        {
            return "Cliente informou que o problema continua.";
        }

        return primary switch
        {
            IntentType.InternetProblem => "Cliente relatou problema de conexão.",
            IntentType.ModemRestarted => "Cliente informou que já reiniciou o equipamento e o problema permanece.",
            IntentType.BillingQuestion => "Cliente perguntou sobre cobrança.",
            IntentType.ModemReplacement => "Cliente falou sobre troca de equipamento.",
            IntentType.HumanHandoff => "Cliente pediu atendimento humano.",
            IntentType.ContinueSupport => "Cliente quer continuar a jornada atual.",
            IntentType.Greeting => "Cliente cumprimentou.",
            _ => "Cliente enviou uma mensagem que depende do contexto da conversa."
        };
    }

    private static string DetectSentiment(string message)
    {
        if (TextNormalizer.ContainsAny(message, "terceira vez", "de novo", "absurdo", "raiva", "urgente", "nao aguento"))
        {
            return "frustrated";
        }

        if (TextNormalizer.ContainsAny(message, "por favor rapido", "urgente", "agora"))
        {
            return "urgent";
        }

        return "neutral";
    }

    private static void AddScore(Dictionary<IntentType, double> scores, IntentType intent, double weight)
    {
        scores[intent] = Math.Max(scores.GetValueOrDefault(intent), weight);
    }

    private static bool IsAffirmation(string message) =>
        TextNormalizer.EqualsAny(message, "sim", "ss", "beleza", "blz", "ok", "pode", "pode ser", "quero", "isso", "ja", "ja fiz", "claro", "uhum", "certo")
        || TextNormalizer.ContainsAny(message, "ja fiz isso", "ja tentei", "pode ser", "isso mesmo");

    private static bool IsNegation(string message) =>
        TextNormalizer.EqualsAny(message, "nao", "n", "nao quero", "nada")
        || TextNormalizer.ContainsAny(message, "nao quero", "nao precisa", "nao foi");

    private static bool IsPersistence(string message) =>
        TextNormalizer.ContainsAny(message, "nao resolveu", "n resolveu", "continua igual", "continua ruim", "continua sem", "ainda nao", "terceira vez", "nao funcionou", "nao foi");

    private static bool IsContinuation(string message) =>
        TextNormalizer.EqualsAny(message, "e agora", "e agora?", "continua", "pode seguir")
        || TextNormalizer.ContainsAny(message, "e agora", "como falei", "como te falei");

    private static bool IsCostQuestion(string message) =>
        TextNormalizer.ContainsAny(message, "quanto", "cobr", "preco", "valor");

    private static bool IsAmbiguousReference(string message) =>
        TextNormalizer.EqualsAny(message, "isso", "esse", "essa", "esse ai", "essa troca", "aquilo")
        || TextNormalizer.ContainsAny(message, "esse ai", "essa troca", "aquilo");

    private static bool LooksLikeModemRestartQuestion(string lastQuestion) =>
        TextNormalizer.ContainsAny(lastQuestion, "reiniciou o modem", "reiniciar o modem", "ja tentou reiniciar");

    private static bool LooksLikeBillingQuestion(string lastQuestion) =>
        TextNormalizer.ContainsAny(lastQuestion, "cobr", "fatura", "valor");

    private static bool LooksLikeReplacementQuestion(string lastQuestion) =>
        TextNormalizer.ContainsAny(lastQuestion, "troca", "substitu");

    private static bool HasUsefulContext(ConversationUnderstandingRequest request) =>
        request.IssueType != IssueType.None
        || request.ModemRestarted
        || !string.IsNullOrWhiteSpace(request.ContextSummary)
        || !string.IsNullOrWhiteSpace(request.CurrentRequest);

    private static bool HasInternetMemory(ConversationUnderstandingRequest request) =>
        request.IssueType == IssueType.InternetConnection
        || request.ModemRestarted
        || (request.OriginalProblem?.Contains("internet", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool HasFact(ConversationUnderstandingRequest request, string key) =>
        request.KnownFacts.ContainsKey(key);

    private static string? LastAssistantContent(ConversationUnderstandingRequest request) =>
        request.RecentMessages.LastOrDefault(m => m.Sender == MessageSender.Assistant)?.Content;
}

internal sealed class FactBundle
{
    public Dictionary<string, string> Extracted { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Known { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Inferences { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal static class FactExtractor
{
    public static FactBundle Extract(string message, ConversationUnderstandingRequest request)
    {
        var bundle = new FactBundle();
        var lastQuestion = TextNormalizer.Normalize(request.LastAssistantQuestion ?? request.RecentMessages.LastOrDefault(m => m.Sender == MessageSender.Assistant)?.Content);

        if (TextNormalizer.ContainsAny(message, "reiniciei", "ja reiniciei", "ja tentei reiniciar")
            || (LooksLikeRestartConfirmation(message) && TextNormalizer.ContainsAny(lastQuestion, "reiniciou", "reiniciar")))
        {
            Put(bundle, "modem_restart_attempted", "true", known: true);
        }

        var count = ExtractCount(message);
        if (count is not null && bundle.Known.ContainsKey("modem_restart_attempted"))
        {
            Put(bundle, "restart_count", count, known: true);
        }

        if (TextNormalizer.ContainsAny(message, "luz vermelha", "led vermelho")
            || (TextNormalizer.ContainsAny(message, "luz") && TextNormalizer.ContainsAny(message, "vermelha")))
        {
            Put(bundle, "modem_light", "red", known: true);
        }

        if (TextNormalizer.ContainsAny(message, "nao resolveu", "continua", "ainda nao", "nao funcionou", "nao foi", "persist"))
        {
            Put(bundle, "issue_persists", "true", known: true);
        }

        if (TextNormalizer.ContainsAny(message, "nao quero trocar", "nao quero a troca"))
        {
            Put(bundle, "declined_replacement", "true", known: true);
        }

        if (TextNormalizer.ContainsAny(message, "atendente", "falar com alguem", "humano"))
        {
            Put(bundle, "wants_human_agent", "true", known: true);
        }

        if (bundle.Known.GetValueOrDefault("modem_restart_attempted") == "true"
            && bundle.Known.GetValueOrDefault("issue_persists") == "true")
        {
            bundle.Inferences["possible_equipment_failure"] = "true";
        }

        return bundle;
    }

    private static bool LooksLikeRestartConfirmation(string message) =>
        TextNormalizer.EqualsAny(message, "sim", "ss", "beleza", "ja", "ja fiz", "isso", "ok", "ja fiz isso")
        || TextNormalizer.ContainsAny(message, "ja fiz", "ja tentei", "isso mesmo");

    private static string? ExtractCount(string message)
    {
        if (TextNormalizer.ContainsAny(message, "duas vezes", "2 vezes", "reiniciei duas"))
        {
            return "2";
        }

        if (TextNormalizer.ContainsAny(message, "tres vezes", "três vezes", "3 vezes", "terceira vez"))
        {
            return "3";
        }

        return null;
    }

    private static void Put(FactBundle bundle, string key, string value, bool known)
    {
        bundle.Extracted[key] = value;
        if (known)
        {
            bundle.Known[key] = value;
        }
        else
        {
            bundle.Inferences[key] = value;
        }
    }
}
