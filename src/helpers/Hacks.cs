using System.IO;

namespace pannella.analoguepocket;

public class Hacks
{
    private static string gamegearCore = @"{
    ""core"": {
        ""magic"": ""APF_VER_1"",
        ""metadata"": {
            ""platform_ids"": [
				""gg""
            ],
            ""shortname"": ""GG"",
            ""description"": ""GG Core"",
            ""author"": ""Spiritualized"",
            ""url"": """",
            ""version"": ""1.3.0"",
            ""date_release"": ""2022-08-25""
        },
        ""framework"": {
            ""target_product"": ""Analogue Pocket"",
            ""version_required"": ""1.1"",
            ""sleep_supported"": true,
            ""dock"": {
                ""supported"": true,
                ""analog_output"": false
            },
            ""hardware"": {
                ""link_port"": false,
                ""cartridge_adapter"": -1
            }
        },
        ""cores"": [
            {
                ""name"": ""default"",
                ""id"": 0,
                ""filename"": ""gg.rev""
            }
        ]
    }
}";

    public static void GamegearFix(string path)
    {
        string corefile = Path.Combine(path, "Cores", "Spiritualized.GG", "core.json");
        File.WriteAllText(corefile, gamegearCore, System.Text.Encoding.Default);
    }

}
