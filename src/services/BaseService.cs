using Pannella.Models;

namespace Pannella.Services;

public abstract class BaseService
{
    public event EventHandler<StatusUpdatedEventArgs> StatusUpdated;

    public event EventHandler<UpdateProcessCompleteEventArgs> UpdateProcessComplete;

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

    protected void OnUpdateProcessComplete(UpdateProcessCompleteEventArgs e)
    {
        EventHandler<UpdateProcessCompleteEventArgs> handler = UpdateProcessComplete;

        handler?.Invoke(this, e);
    }
}
