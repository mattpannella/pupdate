using Newtonsoft.Json;

namespace Pannella.Models.Settings;

public class Debug
{
#if DEBUG
    public bool show_stack_traces = true;
#else
    public bool show_stack_traces { get; set; } = false;
#endif
}
