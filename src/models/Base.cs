namespace Pannella.Models;

public class Base
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

    protected void Divide()
    {
        WriteMessage("-------------");
    }

    protected void OnStatusUpdated(StatusUpdatedEventArgs e)
    {
        EventHandler<StatusUpdatedEventArgs> handler = this.StatusUpdated;

        handler?.Invoke(this, e);
    }

    public void ClearStatusUpdated()
    {
        if (StatusUpdated != null)
        {
            foreach (Delegate d in StatusUpdated.GetInvocationList())
            {
                this.StatusUpdated -= d as EventHandler<StatusUpdatedEventArgs>;
            }
        }
    }

    public bool IsStatusUpdatedRegistered()
    {
        return this.StatusUpdated != null;
    }
}
