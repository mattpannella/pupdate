namespace Pannella.Models;

public class StatusUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Contains the message from the updater
    /// </summary>
    public string Message { get; set; }
}
