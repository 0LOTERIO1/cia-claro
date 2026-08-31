using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class TransferDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public DepartmentType FromDepartment { get; set; }
    public DepartmentType ToDepartment { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ChangeDepartmentRequest
{
    public DepartmentType Department { get; set; }
    public string? Reason { get; set; }
}
