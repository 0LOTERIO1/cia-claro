using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Cia.Api.Services.Understanding;

namespace Cia.Api.Services;

public class IntentService : IIntentService
{
    public IntentType Detect(string message) => IntentRuleEngine.Detect(message);
}
