using Cia.Api.Enums;

namespace Cia.Api.DTOs;

public class DashboardDto
{
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int ResolvedSessions { get; set; }
    public int TransferredSessions { get; set; }
    public IReadOnlyList<ChannelCountDto> SessionsByChannel { get; set; } = Array.Empty<ChannelCountDto>();
    public IReadOnlyList<DepartmentCountDto> SessionsByDepartment { get; set; } = Array.Empty<DepartmentCountDto>();
}

public class ChannelCountDto
{
    public ChannelType Channel { get; set; }
    public int Count { get; set; }
}

public class DepartmentCountDto
{
    public DepartmentType Department { get; set; }
    public int Count { get; set; }
}
