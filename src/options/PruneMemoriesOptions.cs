using CommandLine;

namespace Pannella.Options;

[Verb("prune-memories",  HelpText = "Prune old memories")]
public class PruneMemoriesOptions : BaseOptions
{
    [Option ('c', "core", Required = false, HelpText = "The core you want to prune save states for.")]
    public string CoreName { get; set; }

    [Option ('k', "keep", Required = false, HelpText = "Number of save states to keep per game (default: save_states_to_keep setting).")]
    public int? Keep { get; set; }
}
