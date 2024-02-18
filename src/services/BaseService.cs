using Pannella.Models;

namespace Pannella.Services;

public abstract class BaseService
{
    public event EventHandler<StatusUpdatedEventArgs> StatusUpdated;

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
