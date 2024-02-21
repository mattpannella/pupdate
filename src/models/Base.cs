namespace Pannella.Models;

public class Base
{
    protected const string ARCHIVE_BASE_URL = "https://archive.org/download";

    public event EventHandler<StatusUpdatedEventArgs> StatusUpdated;

    public void ClearStatusUpdated()
    {
        if (StatusUpdated != null)
        {
            foreach (Delegate d in StatusUpdated.GetInvocationList())
            {
                StatusUpdated -= d as EventHandler<StatusUpdatedEventArgs>;
            }
        }
    }

    public bool IsStatusUpdateRegistered()
    {
        return this.StatusUpdated != null;
    }

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
