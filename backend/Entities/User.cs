using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CustomerId { get; set; }

    public Customer? Customer { get; set; }
    public ICollection<HumanAgentRequest> AssignedRequests { get; set; } = new List<HumanAgentRequest>();
}
