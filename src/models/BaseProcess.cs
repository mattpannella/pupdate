using Pannella.Models.Events;

namespace Pannella.Models;

public abstract class BaseProcess : Base
{
    public event EventHandler<UpdateProcessCompleteEventArgs> UpdateProcessComplete;

    protected void OnUpdateProcessComplete(UpdateProcessCompleteEventArgs e)
    {
        EventHandler<UpdateProcessCompleteEventArgs> handler = this.UpdateProcessComplete;

        handler?.Invoke(this, e);
    }
}
