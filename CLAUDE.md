# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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

There is also `pupdate_legacy.csproj` targeting .NET Framework — keep changes compatible with it when possible.

## Architecture

**Entry point:** `src/Program.cs` + partial files in `src/partials/`

**Request flow:**
1. `Program.Main` parses CLI args via CommandLineParser into verb-specific option classes (`src/options/`)
2. Initializes `ServiceHelper` (`src/helpers/ServiceHelper.cs`) which wires up all services
3. Routes to the appropriate handler based on the parsed verb (e.g., `UpdateOptions` → `CoreUpdaterService.RunUpdates()`)
4. Default with no verb: interactive menu mode (`DisplayMenu`)

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

**Status updates** use an event pattern: services fire `StatusUpdatedEventArgs` events that Program subscribes to and writes to console.

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
- See `temp_directory`, `backup_saves_location`, `patreon_email_address` as examples of string settings with no description

Use `[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]` to hide internal/debug settings from the JSON file.

## Adding Constructor Parameters to Services

When adding params to a service constructor (e.g., `ArchiveService`), you must update **both** `Initialize()` and `ReloadSettings()` in `src/helpers/ServiceHelper.cs` — both methods independently construct `ArchiveService`. `ReloadSettings()` is called after the user changes settings in the interactive menu.

`ServiceHelper` exposes computed directory paths as public static properties (`TempDirectory`, `CacheDirectory`) derived from settings with OS-appropriate defaults. Add new ones here rather than recomputing them in callers.

## Checksum Utilities

`Util.CompareChecksum(filepath, hash, Util.HashTypes.MD5)` in `src/helpers/Util.cs` — supports `HashTypes.CRC32` (default) and `HashTypes.MD5`. The `ArchiveFile` model (`src/models/Archive/File.cs`) has both `crc32` and `md5` fields from the archive index; `md5` may be null for some archive types.

## Bundled JSON Data Files

These are embedded in the build output and consumed at runtime:
- `blacklist.json` — cores excluded from updates
- `image_packs.json` — available platform image packs
- `pocket_extras.json` — extra downloadable content definitions
- `display_modes.json` — video display mode configurations
- `ignore_instance.json` — instance JSON generation exclusions
