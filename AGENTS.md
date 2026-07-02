# AGENTS.md

Guidance for AI coding agents (Claude Code and others) working in this repository.

## What This Project Is

Pupdate is a CLI tool for updating openFPGA cores, firmware, and assets on an Analogue Pocket handheld device. It runs in both an interactive menu mode and a non-interactive CLI mode.

## Commands

```bash
# Restore dependencies
dotnet restore pupdate.csproj

# Build
dotnet build pupdate.csproj --no-restore

# Run tests
dotnet test pupdate.csproj --no-build --verbosity normal

# Run a specific test (xUnit)
dotnet test pupdate.csproj --filter "FullyQualifiedName~TestName"

# Run the app
dotnet run --project pupdate.csproj -- [verb] [options]

# Publish for a target platform
dotnet publish pupdate.csproj -r osx-arm64 --self-contained true -c Release -o ./out
```

The project targets **net10.0**. (The former `pupdate_legacy.csproj` / .NET Framework 4.8 build has been removed — there is no legacy target to keep compatible with.)

## Architecture

**Entry point:** `src/Program.cs` + partial files in `src/partials/`

**Request flow:**
1. `Program.Main` parses CLI args via CommandLineParser into verb-specific option classes (`src/options/`)
2. Initializes `ServiceHelper` (`src/helpers/ServiceHelper.cs`) which wires up all services
3. Routes to the appropriate handler based on the parsed verb (e.g., `UpdateOptions` → `CoreUpdaterService.RunUpdates()`)
4. Default with no verb: interactive mode. Two front-ends share the same services — the classic console menu (`DisplayMenu`, the default) and a full-screen Terminal.Gui TUI behind the `--tui` flag (`TuiApp.Run`, see below). `--tui` is **opt-in** for now; the classic menu stays the default and must keep working.

**Key layers:**

- `src/services/` — Core business logic. The main orchestrator is `CoreUpdaterService`. Other notable services:
  - `CoresService` — installs/uninstalls cores; has partial files for Jotego-specific renaming, display modes, video, instance JSON
  - `AssetsService` — downloads ROMs/BIOS files from configured archives
  - `GithubApiService` — fetches release info from GitHub
  - `FirmwareService` — handles Pocket firmware updates
  - `SettingsService` — reads/writes `pupdate_settings.json`

- `src/models/` — Data models. Organized into subdirectories: `Analogue/` (device file formats), `Settings/`, `Archive/`, `Github/`, `OpenFPGA_Cores_Inventory/`, `Extras/`, etc.

- `src/options/` — One class per CLI verb (`UpdateOptions`, `AssetsOptions`, `FirmwareOptions`, etc.), all inheriting `BaseOptions`

- `src/partials/` — Partial classes that extend `Program` for interactive menus and non-core CLI flows (display modes, Game Boy palettes, instance generator, self-update)

- `src/helpers/` — Utilities: `HttpHelper` (singleton HTTP client with caching), `ZipHelper`/`SevenZipHelper`, `SemverUtil`, `ConsoleHelper`, `Util`

**Status updates** use an event pattern: services fire `StatusUpdatedEventArgs` events. `Program.Main` initializes services first (`ServiceHelper.Initialize` with no sinks), resolves which interactive UI is launching (`ResolveInteractiveUi`: `--tui`/`--classic`/`PUPDATE_TUI` flag → saved `use_tui` setting → one-time startup prompt), then attaches the matching sinks via `ServiceHelper.AttachSinks` — the classic console writer, or `TuiApp.StatusSink`/`CompleteSink` which stream into the TUI status pane. The attached sink is what `ReloadSettings` re-attaches (issue #299). Likewise `HttpHelper.Instance.DownloadProgressUpdate` drives either the console `\r` bar or the TUI progress bar, gated by `ServiceHelper.InteractiveTui`.

## Interactive TUI (`src/tui/`)

A full-screen Terminal.Gui v2 (`Terminal.Gui` 2.4.x, net10-only) interface, marked **Beta**. It's opt-in: the classic menu is the default, and users reach the TUI via a one-time startup prompt (remembered in the `use_tui` setting, also a toggle in the settings menu / Settings tab), the `--tui` flag, or `PUPDATE_TUI=1`; `--classic` forces the classic menu and skips the prompt. It calls the **same services** as the classic menu — only the presentation differs. Every service-layer change must stay backward-compatible so the classic console UI keeps working.

- **`TuiApp.Run`** — entry point: applies the theme + checkbox glyphs, wires the plugin output/prompt handlers, runs the shell, and restores the terminal in a `finally`.
- **`TuiHost`** — the single seam over Terminal.Gui's obsolete static `Application` API (one `#pragma warning disable CS0618`). Use `TuiHost.Init/Run/Shutdown/Invoke/RequestStop/Refresh` rather than touching `Application` directly.
- **`TuiShell`** — root window: pinned welcome banner, a `Tabs` strip (Main / Setup / Maintenance / Extras / Plugins / Settings), and the `StatusPane`. Global keys: Esc quit · F6 toggle status pane · F9 theme picker (these ride the bubble-up `Window.KeyDown` since no view consumes function keys) — plus the single-key accelerators below, which are handled in `OnGlobalKeyDown`. Keep tabs/status as direct children of the window (a non-focusable container breaks tab navigation).
- **`TuiContext.RunBackground`** — runs blocking service calls on a `Task` (single-operation busy guard) so the UI never freezes.
- **`TuiAccelerators`** — single source of truth for the single-keypress shortcuts (see "Keyboard accelerators" below).

**Threading rule (critical):** any long/blocking service call must run via `TuiContext.RunBackground`, and **every UI mutation from a background thread must be marshaled with `TuiHost.Invoke`**. Leaving a synchronous service call on the UI thread freezes the TUI.

**Keyboard accelerators (`TuiAccelerators` + `TuiShell.OnGlobalKeyDown`):** single keypress, no modifiers. `A`–`F` jump to a tab (one letter per tab, in order); `0`–`9` then `G`–`Z` run the Nth menu item on the active action tab. Item letters deliberately **start after the tab letters** (`G`–`Z` for 6 tabs) so the two key-spaces never collide — `TuiAccelerators` is the one place that mapping and the `[X] label` prefixing live (bump `TabCount` there if the tab set changes). **Both are handled globally** via `TuiHost.AddGlobalKeyDown` (the `Application.KeyDown` event), NOT the focused view: this is required because (1) it fires *before* the focused view, so it pre-empts stock `ListView` first-letter type-ahead — the Settings list's rows start with letters, so a bubble-up handler would lose `A`–`F` to type-ahead — and (2) it works regardless of where focus currently sits (e.g. after an operation moves focus to the status pane). `Application.KeyBindings` would be too late (it runs *after* the focused view). The handler stands down when `TuiHost.IsTopRunnable(shell)` is false (a modal owns the screen) so dialog typing/keys are untouched. Item activation is deferred through `TuiHost.Invoke` so the key event unwinds before the action opens a modal / starts a long op.

**Reusable components — do NOT re-create these per tab/dialog:**
- `ActionMenuTab` — base for action-list tabs (`AddAction(label, action)`); hover + single-click + Enter. Item-key accelerators are driven by the shell's global handler via the tab's `ItemCount` / `RunItem(index)` — the list itself does NOT register a key handler for them (so they don't depend on it holding focus). `SelectDialog` popups are the exception: they run inside a modal (global handler stands down), so they keep their own list-level accelerator via `MenuListView.OnActivate(onActivate, numbered: true)`.
- `MenuListView` / `LogView` — focusable list with hover + auto scrollbar; `LogView` adds `\r`/`\n` terminal-style output (used by `StatusPane` and `PluginRunModal`).
- Dialogs (`src/tui/Dialogs/`): `ChecklistDialog` (marking list + type-ahead filter + optional category/dropdown filter), `SelectDialog`, `CoreSelectorDialog`, `DisplayModeSelectorDialog`, `SubMenuDialog` (groups actions behind one `…` entry), `PluginRunModal`, `SupportModal`, `ThemePickerDialog`; plus `TuiPrompts` (Confirm / PromptText). A `TextField` loses focus to a dialog button-bar in this Terminal.Gui build — prefer type-ahead on a list or field-handles-its-own-keys over adding a text field next to buttons.

**Theming:** `TuiTheme` applies a built-in Terminal.Gui theme by name (loaded from library/hard-coded config only, deterministically). The choice is the `tui_theme` setting (default `Dark`), changeable live via F9 (`ThemePickerDialog` → `ConfigurationManager.Apply` + `TuiHost.Refresh`).

**Backward-compatibility pattern:** methods that wrote to `Console` directly take an optional `Action<string> log = null` (default `Console.WriteLine`); the TUI passes `TuiApp.PostStatus`. Plugin output/prompts route through `PluginService.OutputHandler/ChoiceHandler/TextHandler` (default to Console). **Never call a method that writes raw `Console.*` — or spawns a child process inheriting stdout — from a TUI tab**; it corrupts the canvas. Redirect via the `log` param / handlers (e.g. `Program.BuildGameAndWatchRoms` redirects its generator's stdout through `log`). `internal static` helpers in `Program`/services are exposed so the TUI can reuse the classic logic with a `log` sink.

## Key Conventions

- **Namespace root:** `Pannella.*`
- **JSON serialization:** Newtonsoft.Json; model property names use `snake_case` to match device file formats
- **Settings file:** `pupdate_settings.json` next to the executable; managed by `SettingsService`
- **Analogue Pocket file layout on device:**
  ```
  {InstallPath}/Cores/{author}.{core}/
  {InstallPath}/Assets/{platform_id}/{core_identifier}/
  {InstallPath}/Platforms/
  ```
- **Archive types:** `internet_archive`, `custom`, `core_specific` — configured in settings; `AssetsService` dispatches to the right handler
- **Jotego cores** have special rename handling in `CoresService.Jotego.cs`
- The `tests/pupdate.Tests/` directory has stale `obj/`/`bin/` artifacts but no `.csproj` or source files. `pupdate.csproj` has `<Compile Remove="tests/**" />` to prevent those generated `.cs` files from being picked up by the main project's `**/*.cs` glob (which would cause duplicate assembly attribute errors and a missing Xunit reference). Do not remove that exclusion.

## Settings Pattern (Critical)

The settings menu (`src/partials/Program.Menus.Settings.cs`) auto-discovers settings by reflecting over `Config` properties. **It casts every property that has a `[Description]` attribute directly to `bool`.** This means:
- Add `[Description("...")]` only to `bool` properties — they appear as toggles in the menu automatically
- String/other-type settings must NOT have `[Description]` or they will throw `InvalidCastException` at runtime
- See `temp_directory`, `backup_saves_location`, `archive_cache_location`, `github_token`, `patreon_session_cookie`, `tui_theme` as examples of string settings with no description

Use `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]` to hide internal/debug settings from the JSON file.

The TUI `SettingsTab` (`src/tui/Tabs/SettingsTab.cs`) reflects the **same** `[Description]` bool properties into a marking list, so the rule above applies identically there. It has an explicit Save button (pressing Enter on the list also saves). Non-bool settings consumed only by the TUI (e.g. `tui_theme`, default `"Dark"`) live as plain string properties with no `[Description]`.

## Adding Constructor Parameters to Services

When adding params to a service constructor (e.g., `ArchiveService`), you must update **both** `Initialize()` and `ReloadSettings()` in `src/helpers/ServiceHelper.cs` — both methods independently construct `ArchiveService`. `ReloadSettings()` is called after the user changes settings in the interactive menu.

`ServiceHelper` exposes computed directory paths as public static properties (`TempDirectory`, `CacheDirectory`) derived from settings with OS-appropriate defaults. Add new ones here rather than recomputing them in callers.

## Checksum Utilities

`Util.CompareChecksum(filepath, hash, Util.HashTypes.MD5)` in `src/helpers/Util.cs` — supports `HashTypes.CRC32` (default) and `HashTypes.MD5`. The `ArchiveFile` model (`src/models/Archive/File.cs`) has both `crc32` and `md5` fields from the archive index; `md5` may be null for some archive types.

## Core Version Pinning

`CoreSettings` has a `pinned_version` (nullable string, hidden from JSON when null) that locks a core to a specific release tag. During `RunUpdates`, if set, `mostRecentRelease` is overridden with the pinned version and `CoresService.GetDownloadUrlForVersion` resolves the download URL for that tag (trying the tag as-is, then with a "v" prefix). This enables both skipping (already at pinned version) and downgrading (local version differs from pinned).

- Managed via interactive menu: Pocket Maintenance → Pin/Unpin Core Version
- Cores without a `repository` (local/manual) are excluded from the pin menu and silently bypass pinning
- `pinned_version` is a string field on `CoreSettings` and must NOT have `[Description]` (same rule as all non-bool `CoreSettings` fields)

## Bundled JSON Data Files

These are embedded in the build output and consumed at runtime:
- `blacklist.json` — cores excluded from updates
- `image_packs.json` — available platform image packs
- `pocket_extras.json` — extra downloadable content definitions
- `display_modes.json` — video display mode configurations
- `ignore_instance.json` — instance JSON generation exclusions
