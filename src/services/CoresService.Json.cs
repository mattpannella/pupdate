using Newtonsoft.Json;
using Pannella.Models.Analogue.Data;
using Pannella.Models.Analogue.Video;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Pannella.Models.Updater;
using AnalogueCore = Pannella.Models.Analogue.Core.Core;

namespace Pannella.Services;

public partial class CoresService
{
    public Platform ReadPlatformJson(string identifier)
    {
        var info = this.ReadCoreJson(identifier);

        if (info == null)
        {
            return null;
        }

        // cores with multiple platforms won't work...not sure any exist right now?
        string platformsFolder = Path.Combine(this.installPath, "Platforms");
        string dataFile = Path.Combine(platformsFolder, info.metadata.platform_ids[0] + ".json");
        var platforms = JsonConvert.DeserializeObject<Dictionary<string, Platform>>(File.ReadAllText(dataFile));

        return platforms["platform"];
    }

    public AnalogueCore ReadCoreJson(string identifier)
    {
        string file = Path.Combine(this.installPath, "Cores", identifier, "core.json");

        if (!File.Exists(file))
        {
            return null;
        }

        string json = File.ReadAllText(file);
        AnalogueCore config = JsonConvert.DeserializeObject<Dictionary<string, AnalogueCore>>(json)["core"];

        return config;
    }

    public DataJSON ReadDataJson(string identifier)
    {
        string file = Path.Combine(this.installPath, "Cores", identifier, "data.json");

        if (!File.Exists(file))
        {
            return null;
        }

        string json = File.ReadAllText(file);
        DataJSON data = JsonConvert.DeserializeObject<DataJSON>(json);

        return data;
    }

    public Video ReadVideoJson(string identifier)
    {
        string file = Path.Combine(this.installPath, "Cores", identifier, "video.json");

        if (!File.Exists(file))
        {
            return null;
        }

        string json = File.ReadAllText(file);
        Video config = JsonConvert.DeserializeObject<Dictionary<string, Video>>(json)["video"];

        return config;
    }

    public Updaters ReadUpdatersJson(string identifier)
    {
        string file = Path.Combine(this.installPath, "Cores", identifier, "updaters.json");

        if (!File.Exists(file))
        {
            return null;
        }

        string json = File.ReadAllText(file);
        Updaters data = JsonConvert.DeserializeObject<Updaters>(json);

        return data;
    }
}
