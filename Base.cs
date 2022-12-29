namespace pannella.analoguepocket;

public class Base
{
    protected const string ARCHIVE_BASE_URL = "https://archive.org/download";
    public event EventHandler<StatusUpdatedEventArgs>? StatusUpdated;
    protected void Divide()
    {
        _writeMessage("-------------");
    }

    protected void _writeMessage(string message)
    {
        StatusUpdatedEventArgs args = new StatusUpdatedEventArgs();
        args.Message = message;
        OnStatusUpdated(args);
    }

    protected virtual void OnStatusUpdated(StatusUpdatedEventArgs e)
    {
        EventHandler<StatusUpdatedEventArgs> handler = StatusUpdated;
        if(handler != null)
        {
            handler(this, e);
        }
    }
}

public class StatusUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Contains the message from the updater
    /// </summary>
    public string Message { get; set; }
}
