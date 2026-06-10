using System.Diagnostics;
using System.Text.RegularExpressions;
using Extism.Sdk;
using Extism.Sdk.Native;
using Newtonsoft.Json;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Plugins;

namespace Pannella.Services;

public class PluginService : Base
{
    private const string WASM_FILE = "plugin.wasm";
    private const string MANIFEST_FILE = "plugin.json";
    private const string INSTALL_INFO_FILE = "installed.json";

    private static readonly Regex RepoSpecRegex = new(
        @"^(?:https?://github\.com/)?(?<owner>[\w\-\.]+)/(?<repo>[\w\-\.]+?)(?:\.git)?/?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string pluginsDirectory;
    private readonly string installPath;
    private readonly string pluginDataRoot;
    private readonly string githubToken;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Converters = { new PluginMessageConverter(), new HostMessageConverter() },
    };

    public PluginService(string pluginsDirectory, string installPath, string pluginDataRoot, string githubToken = null)
    {
        this.pluginsDirectory = pluginsDirectory;
        this.installPath = installPath;
        this.pluginDataRoot = pluginDataRoot;
        this.githubToken = githubToken;
    }

    public List<PluginDescriptor> Discover()
    {
        var result = new List<PluginDescriptor>();

        if (string.IsNullOrEmpty(pluginsDirectory) || !Directory.Exists(pluginsDirectory))
            return result;

        foreach (var dir in Directory.EnumerateDirectories(pluginsDirectory))
        {
            var wasm = Path.Combine(dir, WASM_FILE);
            var json = Path.Combine(dir, MANIFEST_FILE);

            if (!File.Exists(wasm) || !File.Exists(json))
                continue;

            PluginManifest manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<PluginManifest>(File.ReadAllText(json));
            }
            catch (Exception ex)
            {
                WriteMessage($"Skipping plugin '{Path.GetFileName(dir)}': invalid plugin.json ({ex.Message})");
                continue;
            }

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Name))
            {
                WriteMessage($"Skipping plugin '{Path.GetFileName(dir)}': plugin.json missing required 'name' field");
                continue;
            }

            var installInfo = TryReadInstallInfo(dir);

            result.Add(new PluginDescriptor
            {
                WasmPath = wasm,
                DirectoryName = Path.GetFileName(dir),
                PluginDirectory = dir,
                DisplayName = manifest.Name,
                Description = manifest.Description,
                LogoUrl = manifest.LogoUrl,
                AllowedHosts = manifest.AllowedHosts ?? new List<string>(),
                Repo = installInfo?.Repo,
                InstalledTag = installInfo?.ReleaseTag,
            });
        }

        return result.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool InstallFromGithub(string repoSpec)
    {
        if (string.IsNullOrWhiteSpace(repoSpec))
        {
            WriteMessage("Repo spec is empty.");
            return false;
        }

        var m = RepoSpecRegex.Match(repoSpec.Trim());
        if (!m.Success)
        {
            WriteMessage($"Could not parse '{repoSpec}'. Expected 'owner/repo' or a github.com URL.");
            return false;
        }

        var owner = m.Groups["owner"].Value;
        var repo = m.Groups["repo"].Value;
        var slug = $"{owner}/{repo}";

        WriteMessage($"Fetching latest release of {slug}...");
        var release = SafeCall(() => GithubApiService.GetLatestRelease(owner, repo, githubToken),
            $"Could not fetch release info for {slug}");
        if (release == null)
            return false;

        var wasmAsset = release.assets?.FirstOrDefault(a => string.Equals(a.name, WASM_FILE, StringComparison.OrdinalIgnoreCase));
        var jsonAsset = release.assets?.FirstOrDefault(a => string.Equals(a.name, MANIFEST_FILE, StringComparison.OrdinalIgnoreCase));

        if (wasmAsset == null || jsonAsset == null)
        {
            WriteMessage($"Release {release.tag_name} of {slug} is missing plugin.wasm and/or plugin.json assets.");
            return false;
        }

        var safeDirName = $"{owner}.{repo}";
        var targetDir = Path.Combine(pluginsDirectory, safeDirName);

        try
        {
            Directory.CreateDirectory(targetDir);
            WriteMessage($"Downloading {wasmAsset.name} from {release.tag_name}...");
            HttpHelper.Instance.DownloadFile(wasmAsset.browser_download_url, Path.Combine(targetDir, WASM_FILE), timeout: 300);
            WriteMessage($"Downloading {jsonAsset.name}...");
            HttpHelper.Instance.DownloadFile(jsonAsset.browser_download_url, Path.Combine(targetDir, MANIFEST_FILE), timeout: 60);

            WriteInstallInfo(targetDir, new PluginInstallInfo
            {
                Repo = slug,
                ReleaseTag = release.tag_name,
                InstalledAt = DateTime.UtcNow.ToString("o"),
            });

            WriteMessage($"Installed {slug} ({release.tag_name}) to {targetDir}");
            return true;
        }
        catch (Exception ex)
        {
            WriteMessage($"Install failed: {ex.Message}");
            return false;
        }
    }

    // Returns the new release tag if an update is available, otherwise null.
    public string CheckForUpdate(PluginDescriptor descriptor)
    {
        if (string.IsNullOrEmpty(descriptor?.Repo))
            return null;

        var parts = descriptor.Repo.Split('/');
        if (parts.Length != 2)
            return null;

        var release = SafeCall(() => GithubApiService.GetLatestRelease(parts[0], parts[1], githubToken),
            $"Could not check {descriptor.Repo} for updates");
        if (release == null)
            return null;

        if (string.Equals(release.tag_name, descriptor.InstalledTag, StringComparison.OrdinalIgnoreCase))
            return null;

        return release.tag_name;
    }

    public bool Update(PluginDescriptor descriptor)
    {
        if (string.IsNullOrEmpty(descriptor?.Repo))
        {
            WriteMessage($"Plugin '{descriptor?.DisplayName}' has no recorded repo to update from.");
            return false;
        }

        return InstallFromGithub(descriptor.Repo);
    }

    private static PluginInstallInfo TryReadInstallInfo(string dir)
    {
        var path = Path.Combine(dir, INSTALL_INFO_FILE);
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonConvert.DeserializeObject<PluginInstallInfo>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static void WriteInstallInfo(string dir, PluginInstallInfo info)
    {
        File.WriteAllText(Path.Combine(dir, INSTALL_INFO_FILE),
            JsonConvert.SerializeObject(info, Formatting.Indented));
    }

    private T SafeCall<T>(Func<T> call, string errorPrefix) where T : class
    {
        try
        {
            return call();
        }
        catch (Exception ex)
        {
            WriteMessage($"{errorPrefix}: {ex.Message}");
            return null;
        }
    }

    public void Run(PluginDescriptor descriptor)
    {
        if (descriptor == null || !File.Exists(descriptor.WasmPath))
        {
            WriteMessage($"Plugin not found: {descriptor?.WasmPath}");
            return;
        }

        HostFunction openUrl = null;
        HostFunction printMsg = null;
        Plugin plugin = null;

        try
        {
            var manifest = new Manifest(new PathWasmSource(descriptor.WasmPath));
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                manifest.AllowedPaths.Add(installPath, "pocket");

            var hostFolder = EnsureHostFolder(descriptor);
            if (hostFolder != null)
                manifest.AllowedPaths.Add(hostFolder, "host");

            foreach (var allowed in descriptor.AllowedHosts)
                manifest.AllowedHosts.Add(allowed);

            manifest.Timeout = TimeSpan.FromMinutes(5);

            openUrl = new HostFunction(
                "open_url",
                new[] { ExtismValType.PTR },
                new[] { ExtismValType.PTR },
                null,
                HandleOpenUrl);

            printMsg = new HostFunction(
                "print_msg",
                new[] { ExtismValType.PTR },
                new[] { ExtismValType.PTR },
                null,
                HandlePrintMsg);

            Plugin.ConfigureCustomLogging(LogLevel.Info);
            plugin = new Plugin(manifest, new[] { openUrl, printMsg }, withWasi: true);

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

                // Per pocket-plugin README: sending Kill notifies the plugin
                // to wind down, but the plugin may run cleanup / confirmation
                // logic before returning Exit. Stay in the loop and keep
                // responding to whatever it asks for until it returns Exit.
                // The manifest Timeout is the backstop if a plugin misbehaves.
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
            printMsg?.Dispose();
        }
    }

    private string EnsureHostFolder(PluginDescriptor descriptor)
    {
        if (string.IsNullOrEmpty(pluginDataRoot) || string.IsNullOrEmpty(descriptor.DirectoryName))
            return null;

        try
        {
            var dir = Path.Combine(pluginDataRoot, descriptor.DirectoryName);
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch (Exception ex)
        {
            WriteMessage($"Could not create plugin host folder: {ex.Message}");
            return null;
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

        WriteEmptyOutput(plugin, outputs);
    }

    private void HandlePrintMsg(CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs)
    {
        // `print_msg` is the plugin's stdout equivalent — fires synchronously
        // during the wasm call so progress bars and live status output render
        // immediately, unlike `info!()` logs which are batched by Extism and
        // only delivered when DrainCustomLogs runs. Write raw to Console
        // without appending a newline; the plugin includes \n itself when it
        // wants one (the pocket-plugin sample does `format!("{msg}\n")` for
        // its `println` helper).
        try
        {
            var msg = plugin.ReadString(new nint(inputs[0].v.ptr));
            if (!string.IsNullOrEmpty(msg))
                Console.Write(msg);
        }
        catch (Exception ex)
        {
            WriteMessage($"[plugin] print_msg failed: {ex.Message}");
        }

        WriteEmptyOutput(plugin, outputs);
    }

    private static void WriteEmptyOutput(CurrentPlugin plugin, Span<ExtismVal> outputs)
    {
        // pocket-plugin's demo_host registers host functions with `[PTR], [PTR]`
        // (matching input and output ptr), even though the Rust signatures return
        // `()`. The plugin's host_fn macro ignores the output, so write any valid
        // pointer to keep the ABI happy.
        if (outputs.Length > 0)
        {
            outputs[0].t = ExtismValType.PTR;
            outputs[0].v.ptr = plugin.WriteString(string.Empty);
        }
    }
}
