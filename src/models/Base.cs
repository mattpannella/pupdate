namespace Pannella.Models;

public class Base
{
    protected const string ARCHIVE_BASE_URL = "https://archive.org/download";

    public event EventHandler<StatusUpdatedEventArgs> StatusUpdated;

    protected void Divide()
    {
        WriteMessage("-------------");
    }

    protected void WriteMessage(string message)
    {
        StatusUpdatedEventArgs args = new StatusUpdatedEventArgs
        {
            Message = message
        };

        OnStatusUpdated(args);
    }

    protected void OnStatusUpdated(StatusUpdatedEventArgs e)
    {
        EventHandler<StatusUpdatedEventArgs> handler = StatusUpdated;

        handler?.Invoke(this, e);
    }
}

public class StatusUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Contains the message from the updater
    /// </summary>
    public string Message { get; set; }
}
