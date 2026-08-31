using Cia.Api.Enums;

namespace Cia.Api.Entities;

public class DepartmentTransfer
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public DepartmentType FromDepartment { get; set; }
    public DepartmentType ToDepartment { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ConversationSession Session { get; set; } = null!;
}
