using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cia.Api.Data;
using Cia.Api.DTOs;
using Cia.Api.Entities;
using Cia.Api.Enums;
using Cia.Api.Services;
using Cia.Api.Services.Knowledge;
using Cia.Api.Services.Understanding;
using Microsoft.Extensions.Options;

namespace Cia.Api.Tests;

public class ConversationUnderstandingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task EvaluationCases_MatchExpectedUnderstanding()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ConversationCases.json");
        var payload = JsonSerializer.Deserialize<ConversationCaseFile>(File.ReadAllText(path), JsonOptions)
                      ?? throw new InvalidOperationException("ConversationCases.json inválido.");
        var understanding = TestComposition.CreateUnderstanding();

        foreach (var testCase in payload.Cases)
        {
            var result = await understanding.UnderstandAsync(ToRequest(testCase));
            var expect = testCase.Expect;
            if (!string.IsNullOrWhiteSpace(expect.PrimaryIntent))
            {
                Assert.True(
                    Enum.TryParse<IntentType>(expect.PrimaryIntent, true, out var intent) && result.PrimaryIntent == intent,
                    $"{testCase.Id}: intent {result.PrimaryIntent} != {expect.PrimaryIntent}");
            }

            if (expect.AnyOfPrimary is { Count: > 0 })
            {
                Assert.Contains(result.PrimaryIntent.ToString(), expect.AnyOfPrimary, StringComparer.OrdinalIgnoreCase);
            }

            if (expect.MustIncludeIntents is { Count: > 0 })
            {
                var all = result.AllIntents.Select(i => i.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var required in expect.MustIncludeIntents)
                {
                    Assert.True(all.Contains(required), $"{testCase.Id}: missing intent {required}");
                }
            }

            if (expect.KnownFactKeys is { Count: > 0 })
            {
                foreach (var key in expect.KnownFactKeys)
                {
                    Assert.True(result.KnownFacts.ContainsKey(key) || result.ExtractedFacts.ContainsKey(key),
                        $"{testCase.Id}: missing fact {key}");
                }
            }

            if (expect.ShouldAskClarification.HasValue)
            {
                Assert.Equal(expect.ShouldAskClarification.Value, result.ShouldAskClarification);
            }

            if (expect.ShouldEscalate.HasValue)
            {
                Assert.Equal(expect.ShouldEscalate.Value, result.ShouldEscalate);
            }

            if (expect.TopicChanged.HasValue)
            {
                Assert.Equal(expect.TopicChanged.Value, result.TopicChanged);
            }
        }
    }

    [Fact]
    public async Task ShortReply_UsesPreviousQuestion_AndDoesNotAskKnownFactAgain()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, _, _) = TestComposition.CreateServices(db);

        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });

        var second = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "já"
        });

        Assert.Equal(IntentType.ModemRestarted, second.DetectedIntent);
        Assert.True(second.Context?.ModemRestarted);
        Assert.DoesNotContain("Você já reiniciou o modem?", second.AssistantMessage!.Content, StringComparison.OrdinalIgnoreCase);

        var third = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });

        Assert.DoesNotContain("Você já reiniciou o modem?", third.AssistantMessage!.Content, StringComparison.OrdinalIgnoreCase);
        Assert.True(third.Context?.ModemRestarted);
    }

    [Fact]
    public async Task ContextPersists_BetweenTelegramAndPortal_SameCustomer()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, _, _) = TestComposition.CreateServices(db);

        var telegram = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.Telegram,
            Content = "Minha internet não está funcionando."
        });

        var portal = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "nao resolveu"
        });

        Assert.Equal(telegram.SessionId, portal.SessionId);
        Assert.Equal(ChannelType.WebPortal, portal.CurrentChannel);
        Assert.Equal(IssueType.InternetConnection, portal.Context?.IssueType);
        Assert.Equal(telegram.Protocol, portal.Protocol);
        Assert.Contains("internet", portal.Context?.ContextSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HumanSession_DoesNotInvokeAssistant()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, _, _) = TestComposition.CreateServices(db);

        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "Quero falar com um atendente"
        });

        var followUp = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "ainda estou na linha"
        });

        Assert.Equal(SessionStatus.WaitingForAgent, followUp.Status);
        Assert.Equal(MessageSender.Customer, followUp.Messages[^1].Sender);
        Assert.DoesNotContain(followUp.Messages.TakeLast(1), m => m.Sender == MessageSender.Assistant);
    }

    [Fact]
    public async Task TopicChange_KeepsPreviousFacts()
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
            Content = "Já reiniciei o modem."
        });

        var billing = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "também queria entender minha fatura"
        });

        Assert.Equal(IntentType.BillingQuestion, billing.DetectedIntent);
        Assert.Equal(DepartmentType.Financial, billing.CurrentDepartment);
        Assert.True(billing.Context?.ModemRestarted);
        Assert.Equal(IssueType.InternetConnection, billing.Context?.IssueType);
        Assert.Contains("cobrança", billing.Context?.ContextSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Customers_AreIsolated_PedroLucasRafael()
    {
        using var db = TestComposition.CreateDb();
        db.Customers.AddRange(
            new Customer { Id = DbSeeder.PedroCustomerId, Name = "Pedro", Phone = "11988887777", CreatedAt = DateTime.UtcNow },
            new Customer { Id = DbSeeder.RafaelCustomerId, Name = "Rafael", Phone = "11977776666", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var (conversation, _, _, _) = TestComposition.CreateServices(db);

        var lucas = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Minha internet não está funcionando."
        });
        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Já reiniciei o modem."
        });

        var pedro = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.PedroCustomerId,
            Channel = ChannelType.Telegram,
            Content = "Oi, quero falar da minha fatura"
        });

        var rafael = await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.RafaelCustomerId,
            Channel = ChannelType.WebPortal,
            Content = "Quero continuar meu atendimento."
        });

        Assert.NotEqual(lucas.SessionId, pedro.SessionId);
        Assert.NotEqual(lucas.SessionId, rafael.SessionId);
        Assert.NotEqual(pedro.SessionId, rafael.SessionId);

        var lucasContext = db.ConversationContexts.Single(c => c.SessionId == lucas.SessionId);
        var pedroContext = db.ConversationContexts.Single(c => c.SessionId == pedro.SessionId);
        var rafaelContext = db.ConversationContexts.Single(c => c.SessionId == rafael.SessionId);

        Assert.True(lucasContext.ModemRestarted);
        Assert.False(pedroContext.ModemRestarted);
        Assert.False(rafaelContext.ModemRestarted);
        Assert.DoesNotContain("reinici", pedroContext.ImportantFacts ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DbSeeder.DemoCustomerId, pedro.Messages.Select(m => m.Content).ToArray());
        Assert.Equal(IntentType.BillingQuestion, pedro.DetectedIntent);
        Assert.NotEqual(lucas.Protocol, pedro.Protocol);
        Assert.NotEqual(lucas.Protocol, rafael.Protocol);
        Assert.NotEqual(lucasContext.Id, rafaelContext.Id);
    }

    [Fact]
    public async Task ExternalUnavailable_UsesFallback()
    {
        var provider = new ScriptedAiProvider
        {
            UnderstandException = new HttpRequestException("AI down")
        };
        var understanding = TestComposition.CreateUnderstanding(provider);
        var result = await understanding.UnderstandAsync(new ConversationUnderstandingRequest
        {
            CurrentMessage = "Minha internet caiu",
            Channel = ChannelType.WebPortal,
            CurrentDepartment = DepartmentType.Triage,
            SessionStatus = SessionStatus.Active
        });

        Assert.True(result.UsedFallback);
        Assert.Equal(IntentType.InternetProblem, result.PrimaryIntent);
        Assert.Equal(nameof(LocalFallbackAiProvider), result.Provider);
    }

    [Fact]
    public async Task InvalidExternalResult_UsesFallback()
    {
        var provider = new ScriptedAiProvider
        {
            OnUnderstand = _ => new ConversationUnderstandingResult
            {
                PrimaryIntent = (IntentType)123,
                Confidence = 4
            }
        };
        var understanding = TestComposition.CreateUnderstanding(provider);
        var result = await understanding.UnderstandAsync(new ConversationUnderstandingRequest
        {
            CurrentMessage = "Minha internet não funciona",
            Channel = ChannelType.Telegram,
            CurrentDepartment = DepartmentType.Triage,
            SessionStatus = SessionStatus.Active
        });

        Assert.True(result.UsedFallback);
        Assert.True(Enum.IsDefined(result.PrimaryIntent));
        Assert.InRange(result.Confidence, 0, 1);
    }

    [Fact]
    public async Task InvalidDepartment_IsRejected()
    {
        var provider = new ScriptedAiProvider
        {
            OnUnderstand = _ => new ConversationUnderstandingResult
            {
                PrimaryIntent = IntentType.InternetProblem,
                Confidence = 0.9,
                SuggestedDepartment = (DepartmentType)99,
                ResponseSuggestion = "Vamos seguir com o suporte técnico."
            }
        };
        var understanding = TestComposition.CreateUnderstanding(provider);
        var result = await understanding.UnderstandAsync(new ConversationUnderstandingRequest
        {
            CurrentMessage = "Minha internet não funciona",
            Channel = ChannelType.WebPortal,
            CurrentDepartment = DepartmentType.Triage,
            SessionStatus = SessionStatus.Active
        });

        Assert.False(result.SuggestedDepartment.HasValue);
        Assert.Equal(IntentType.InternetProblem, result.PrimaryIntent);
    }

    [Fact]
    public void Guardrails_BlockInventedCommercialClaims()
    {
        var knowledge = new LocalKnowledgeService();
        var guardrails = new ConversationGuardrails(knowledge, Options.Create(new Cia.Api.Configuration.AiOptions()));
        var sanitized = guardrails.Sanitize(new ConversationUnderstandingResult
        {
            PrimaryIntent = IntentType.BillingQuestion,
            Confidence = 0.9,
            ResponseSuggestion = "Seu plano custa R$ 99."
        }, new ConversationUnderstandingRequest
        {
            CurrentMessage = "quanto custa?",
            Channel = ChannelType.WebPortal,
            CurrentDepartment = DepartmentType.Financial,
            SessionStatus = SessionStatus.Active
        });

        Assert.Equal(knowledge.CommercialFallback, sanitized.ResponseSuggestion);
        Assert.True(knowledge.AllowsCustomerFacingClaim("Posso encaminhar para o financeiro."));
    }

    [Fact]
    public async Task LowConfidence_AsksSpecificClarification()
    {
        var result = await TestComposition.CreateUnderstanding().UnderstandAsync(new ConversationUnderstandingRequest
        {
            CurrentMessage = "xyz",
            Channel = ChannelType.WebPortal,
            CurrentDepartment = DepartmentType.Triage,
            SessionStatus = SessionStatus.Active
        });

        Assert.True(result.ShouldAskClarification);
        Assert.False(string.IsNullOrWhiteSpace(result.ClarificationQuestion));
        Assert.DoesNotContain("Pode me contar mais", result.ClarificationQuestion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DualIntent_DoesNotDropBillingOrInternet()
    {
        var result = await TestComposition.CreateUnderstanding().UnderstandAsync(new ConversationUnderstandingRequest
        {
            CurrentMessage = "Minha internet continua fora e queria saber se a troca é cobrada.",
            Channel = ChannelType.WebPortal,
            CurrentDepartment = DepartmentType.ModemReplacement,
            SessionStatus = SessionStatus.Active,
            IssueType = IssueType.InternetConnection,
            ModemRestarted = true
        });

        var intents = result.AllIntents.ToHashSet();
        Assert.Contains(IntentType.InternetProblem, intents);
        Assert.Contains(IntentType.BillingQuestion, intents);
    }

    [Fact]
    public async Task Inferences_AreNotStoredAsKnownFacts()
    {
        using var db = TestComposition.CreateDb();
        var (conversation, _, _, _) = TestComposition.CreateServices(db);
        await conversation.SendMessageAsync(new SendMessageRequest
        {
            CustomerId = DbSeeder.DemoCustomerId,
            Channel = ChannelType.AppClaro,
            Content = "já reiniciei duas vezes e a luz continua vermelha"
        });

        var context = db.ConversationContexts.Single();
        var memory = ContextMemory.Read(context);
        Assert.Equal("true", memory.KnownFacts.GetValueOrDefault("modem_restart_attempted"));
        Assert.True(memory.Inferences.ContainsKey("possible_equipment_failure"));
        Assert.DoesNotContain("possible_equipment_failure", context.ImportantFacts ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.True(context.ModemRestarted);
    }

    private static ConversationUnderstandingRequest ToRequest(ConversationCase testCase)
    {
        var history = (testCase.History ?? new List<CaseTurn>())
            .Select(turn => new RecentConversationMessage
            {
                Sender = Enum.Parse<MessageSender>(turn.Sender, true),
                Content = turn.Content
            })
            .ToList();
        history.Add(new RecentConversationMessage { Sender = MessageSender.Customer, Content = testCase.Message });

        IntentType? lastIntent = null;
        if (!string.IsNullOrWhiteSpace(testCase.Context?.LastDetectedIntent)
            && Enum.TryParse<IntentType>(testCase.Context.LastDetectedIntent, true, out var parsed))
        {
            lastIntent = parsed;
        }

        IssueType issue = IssueType.None;
        if (!string.IsNullOrWhiteSpace(testCase.Context?.IssueType))
        {
            Enum.TryParse(testCase.Context.IssueType, true, out issue);
        }

        return new ConversationUnderstandingRequest
        {
            CurrentMessage = testCase.Message,
            RecentMessages = history,
            Channel = ChannelType.WebPortal,
            CurrentDepartment = DepartmentType.Triage,
            SessionStatus = SessionStatus.Active,
            LastDetectedIntent = lastIntent,
            ContextSummary = testCase.Context?.ContextSummary,
            CurrentRequest = testCase.Context?.CurrentRequest,
            IssueType = issue,
            ModemRestarted = testCase.Context?.ModemRestarted ?? false,
            InternetStillDown = testCase.Context?.InternetStillDown ?? false,
            KnownFacts = testCase.Context?.KnownFacts ?? new Dictionary<string, string>(),
            LastAssistantQuestion = testCase.Context?.LastAssistantQuestion
                ?? history.LastOrDefault(m => m.Sender == MessageSender.Assistant)?.Content
        };
    }

    private sealed class ConversationCaseFile
    {
        public List<ConversationCase> Cases { get; set; } = new();
    }

    private sealed class ConversationCase
    {
        public string Id { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<CaseTurn>? History { get; set; }
        public CaseContext? Context { get; set; }
        public CaseExpect Expect { get; set; } = new();
    }

    private sealed class CaseTurn
    {
        public string Sender { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class CaseContext
    {
        public string? IssueType { get; set; }
        public bool ModemRestarted { get; set; }
        public bool InternetStillDown { get; set; }
        public string? CurrentRequest { get; set; }
        public string? ContextSummary { get; set; }
        public string? LastAssistantQuestion { get; set; }
        public string? LastDetectedIntent { get; set; }
        public Dictionary<string, string>? KnownFacts { get; set; }
    }

    private sealed class CaseExpect
    {
        public string? PrimaryIntent { get; set; }
        public List<string>? AnyOfPrimary { get; set; }
        public List<string>? MustIncludeIntents { get; set; }
        public List<string>? KnownFactKeys { get; set; }
        public bool? ShouldAskClarification { get; set; }
        public bool? ShouldEscalate { get; set; }
        public bool? TopicChanged { get; set; }
    }
}
