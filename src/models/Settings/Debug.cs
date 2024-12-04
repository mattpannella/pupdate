using Newtonsoft.Json;

namespace Pannella.Models.Settings;

public class Debug
{
    public bool show_stack_traces { get; set; } = false;
}
