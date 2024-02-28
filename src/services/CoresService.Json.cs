using System.Text.Json;
using Pannella.Helpers;
using Pannella.Models.Analogue.Data;
using Pannella.Models.Analogue.Video;
using Pannella.Models.OpenFPGA_Cores_Inventory;
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
        var platforms = JsonSerializer.Deserialize<Dictionary<string, Platform>>(File.ReadAllText(dataFile));

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
        var options = new JsonSerializerOptions { AllowTrailingCommas = true };
        AnalogueCore config = JsonSerializer.Deserialize<Dictionary<string, AnalogueCore>>(json, options)["core"];

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
        var options = new JsonSerializerOptions { Converters = { new StringConverter() } };
        DataJSON data = JsonSerializer.Deserialize<DataJSON>(json, options);

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
        var options = new JsonSerializerOptions { AllowTrailingCommas = true };
        Video config = JsonSerializer.Deserialize<Dictionary<string, Video>>(json, options)["video"];

        return config;
    }
}
