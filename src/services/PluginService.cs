using System.Diagnostics;
using Extism.Sdk;
using Extism.Sdk.Native;
using Newtonsoft.Json;
using Pannella.Models;
using Pannella.Models.Plugins;

namespace Pannella.Services;

public class PluginService : Base
{
    private readonly string pluginsDirectory;
    private readonly string installPath;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Converters = { new PluginMessageConverter(), new HostMessageConverter() },
    };

    public PluginService(string pluginsDirectory, string installPath)
    {
        this.pluginsDirectory = pluginsDirectory;
        this.installPath = installPath;
    }

    public List<PluginDescriptor> Discover()
    {
        var result = new List<PluginDescriptor>();

        if (string.IsNullOrEmpty(pluginsDirectory) || !Directory.Exists(pluginsDirectory))
            return result;

        foreach (var file in Directory.EnumerateFiles(pluginsDirectory, "*.wasm", SearchOption.TopDirectoryOnly))
        {
            result.Add(new PluginDescriptor
            {
                FilePath = file,
                DisplayName = Path.GetFileNameWithoutExtension(file),
            });
        }

        return result.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Run(PluginDescriptor descriptor)
    {
        if (descriptor == null || !File.Exists(descriptor.FilePath))
        {
            WriteMessage($"Plugin not found: {descriptor?.FilePath}");
            return;
        }

        HostFunction openUrl = null;
        Plugin plugin = null;

        try
        {
            var manifest = new Manifest(new PathWasmSource(descriptor.FilePath));
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                manifest.AllowedPaths.Add(installPath, "pocket");
            manifest.AllowedHosts.Add("*");
            manifest.Timeout = TimeSpan.FromMinutes(5);

            openUrl = new HostFunction(
                "open_url",
                new[] { ExtismValType.PTR },
                new[] { ExtismValType.PTR },
                null,
                HandleOpenUrl);

            Plugin.ConfigureCustomLogging(LogLevel.Info);
            plugin = new Plugin(manifest, new[] { openUrl }, withWasi: true);

            WriteMessage($"Running plugin: {descriptor.DisplayName}");

            var msg = CallPlugin(plugin, "start", null);
            while (msg is not ExitPluginMessage)
            {
                DrainLogs();

                HostMessage reply = msg switch
                {
                    ChoicePluginMessage c => PromptChoice(c),
                    TextPluginMessage t => PromptText(t),
                    _ => new KillHostMessage(),
                };

                if (reply is KillHostMessage)
                {
                    CallPlugin(plugin, "handle_response", reply);
                    break;
                }

                msg = CallPlugin(plugin, "handle_response", reply);
            }

            DrainLogs();
            WriteMessage($"Plugin finished: {descriptor.DisplayName}");
        }
        catch (Exception ex)
        {
            WriteMessage($"Plugin error: {ex.Message}");
        }
        finally
        {
            plugin?.Dispose();
            openUrl?.Dispose();
        }
    }

    private PluginMessage CallPlugin(Plugin plugin, string fn, HostMessage input)
    {
        var inputJson = input == null ? string.Empty : JsonConvert.SerializeObject(input, JsonSettings);
        var output = plugin.Call(fn, inputJson);
        return JsonConvert.DeserializeObject<PluginMessage>(output, JsonSettings);
    }

    private void DrainLogs()
    {
        Plugin.DrainCustomLogs(line =>
        {
            var trimmed = line?.TrimEnd();
            if (!string.IsNullOrEmpty(trimmed))
                WriteMessage($"[plugin] {trimmed}");
        });
    }

    private HostMessage PromptChoice(ChoicePluginMessage choice)
    {
        Console.WriteLine();
        Console.WriteLine(choice.Query);
        for (int i = 0; i < choice.Choices.Count; i++)
            Console.WriteLine($"  {i + 1}. {choice.Choices[i]}");
        Console.WriteLine("  0. [Cancel — kill plugin]");
        Console.Write("Select: ");

        var line = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(line) || !int.TryParse(line.Trim(), out var pick))
            return new KillHostMessage();
        if (pick <= 0 || pick > choice.Choices.Count)
            return new KillHostMessage();

        return new AnswerHostMessage
        {
            Name = choice.Name,
            Value = choice.Choices[pick - 1],
        };
    }

    private HostMessage PromptText(TextPluginMessage text)
    {
        Console.WriteLine();
        Console.WriteLine(text.Query);
        Console.WriteLine("  (leave blank to cancel and kill plugin)");
        Console.Write("> ");

        var line = Console.ReadLine();
        if (string.IsNullOrEmpty(line))
            return new KillHostMessage();

        return new AnswerHostMessage
        {
            Name = text.Name,
            Value = line,
        };
    }

    private void HandleOpenUrl(CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs)
    {
        string url = null;
        try
        {
            url = plugin.ReadString(new nint(inputs[0].v.ptr));
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            WriteMessage($"[plugin] opened browser: {url}");
        }
        catch (Exception ex)
        {
            WriteMessage($"[plugin] could not open browser ({ex.Message}). URL: {url ?? "<unknown>"}");
        }

        // Plugin declares `-> ()` but the host is registered with [PTR] output to
        // match pocket-plugin's demo_host. Write an empty string to keep the ABI
        // satisfied; the plugin's host_fn macro discards it.
        if (outputs.Length > 0)
        {
            outputs[0].t = ExtismValType.PTR;
            outputs[0].v.ptr = plugin.WriteString(string.Empty);
        }
    }
}
