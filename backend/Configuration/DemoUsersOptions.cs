namespace Cia.Api.Configuration;

public class DemoUsersOptions
{
    public const string SectionName = "DemoUsers";

    public DemoUserCredentials Pedro { get; set; } = new();
    public DemoUserCredentials Lucas { get; set; } = new();
    public DemoUserCredentials Rafael { get; set; } = new();
    public DemoUserCredentials Agent { get; set; } = new();
    public DemoUserCredentials Admin { get; set; } = new();
}

public class DemoUserCredentials
{
    public string? Password { get; set; }
}
