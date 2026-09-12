namespace Cia.Api.Services;

public static class TelegramCommandParser
{
    public static bool TryParse(string text, out string command, out string argument)
    {
        command = string.Empty;
        argument = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (!value.StartsWith('/'))
        {
            return false;
        }

        var body = value[1..];
        var separator = body.IndexOfAny([' ', '\n', '\t']);
        var head = separator < 0 ? body : body[..separator];
        argument = separator < 0 ? string.Empty : body[(separator + 1)..].Trim();

        var at = head.IndexOf('@');
        command = (at < 0 ? head : head[..at]).Trim().ToLowerInvariant();
        return command.Length > 0;
    }

    public static bool IsStart(string command) => command == "start";
    public static bool IsLink(string command) => command == "link";
    public static bool IsContinue(string command) => command is "continuar" or "continue";
    public static bool IsRestart(string command) => command is "novo";
    public static bool IsEnd(string command) => command is "encerrar";

    public static bool IsSessionLifecycle(string command)
        => IsStart(command) || IsContinue(command) || IsRestart(command) || IsEnd(command);
}

public static class TelegramCommandActions
{
    public const string CreatedNewSession = "CreatedNewSession";
    public const string ContinuedActiveSession = "ContinuedActiveSession";
    public const string HumanSessionPreserved = "HumanSessionPreserved";
}
