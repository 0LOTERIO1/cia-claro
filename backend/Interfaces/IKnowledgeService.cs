namespace Cia.Api.Interfaces;

public interface IKnowledgeService
{
    bool AllowsCustomerFacingClaim(string? text);
    string CommercialFallback { get; }
}
