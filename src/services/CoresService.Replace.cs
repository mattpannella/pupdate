using Newtonsoft.Json;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Pannella.Models.Updater;

namespace Pannella.Services;

public partial class CoresService
{
    public Substitute[] GetSubstitutes(string identifier)
    {
        string file = Path.Combine(this.installPath, "Cores", identifier, "updaters.json");

        if (!File.Exists(file))
        {
            return null;
        }

        string json = File.ReadAllText(file);
        Updaters config = JsonConvert.DeserializeObject<Updaters>(json);

        return config?.previous;
    }

    public void ReplaceCheck(string identifier)
    {
        var replaces = this.GetSubstitutes(identifier);

        if (replaces != null)
        {
            foreach (var replacement in replaces)
            {
                string newIdentifier = $"{replacement.author}.{replacement.shortname}";
                Core core = new Core { identifier = newIdentifier, platform_id = replacement.platform_id };

                if (this.IsInstalled(core.identifier))
                {
                    Replace(core, identifier);
                    this.Uninstall(core.identifier, core.platform_id);
                    WriteMessage($"Uninstalled {newIdentifier}. It was replaced by this core.");
                }
            }
        }
    }

    private void Replace(Core core, string identifier)
    {
        string path = Path.Combine(this.installPath, "Assets", core.platform_id, core.identifier);

        if (Directory.Exists(path))
        {
            Directory.Move(path, Path.Combine(this.installPath, "Assets", core.platform_id, identifier));
        }

        path = Path.Combine(this.installPath, "Saves", core.platform_id, core.identifier);

        if (Directory.Exists(path))
        {
            Directory.Move(path, Path.Combine(this.installPath, "Saves", core.platform_id, identifier));
        }

        path = Path.Combine(this.installPath, "Settings", core.identifier);

        if (Directory.Exists(path))
        {
            Directory.Move(path, Path.Combine(this.installPath, "Settings", identifier));
        }
    }
}
