namespace Cia.Api.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}

public class ValidationAppException : Exception
{
    public ValidationAppException(string message) : base(message)
    {
    }
}
