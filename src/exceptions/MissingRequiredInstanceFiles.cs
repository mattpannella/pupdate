namespace Pannella.Exceptions;

public class MissingRequiredInstanceFiles : Exception
{
    public MissingRequiredInstanceFiles() { }

    public MissingRequiredInstanceFiles(string message)
        : base(message) { }

    public MissingRequiredInstanceFiles(string message, Exception inner)
        : base(message, inner) { }
}
