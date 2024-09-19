using Pannella.Models.Events;

namespace Pannella.Models;

public class Base
{
    public event EventHandler<StatusUpdatedEventArgs> StatusUpdated;

    public const string DIVIDER = "-------------";

    protected void WriteMessage(string message)
    {
        StatusUpdatedEventArgs args = new StatusUpdatedEventArgs
        {
            Message = message
        };

        OnStatusUpdated(args);
    }

    protected void Divide()
    {
        WriteMessage(DIVIDER);
    }

    protected void OnStatusUpdated(StatusUpdatedEventArgs e)
    {
        EventHandler<StatusUpdatedEventArgs> handler = this.StatusUpdated;

        handler?.Invoke(this, e);
    }
}
