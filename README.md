
[![Current Release](https://img.shields.io/github/v/release/mattpannella/pupdate?label=Current%20Release)](https://github.com/mattpannella/pupdate/releases/latest) ![Downloads](https://img.shields.io/github/downloads/mattpannella/pupdate/latest/total?label=Downloads) [![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate/?business=YEERX89E75HQ8&no_recurring=1&currency_code=USD)

A free utility for updating openFPGA cores, firmware, and related assets on your Analogue Pocket. It can run as an **interactive console menu** or from the **command line**.

A browsable list of available cores is here: [openFPGA cores inventory](https://openfpga-cores-inventory.github.io/analogue-pocket/).

Please use the [latest release](https://github.com/mattpannella/pupdate/releases/latest/) before reporting issues; older builds are not supported.

---

## Easy mode

Do **not** clone the repo if you only want to run the tool. Download the latest release, unzip it, place the executable for your OS on the **root of your SD card** (or another folder you use as the install path), and run it.

Use **Settings** from the main menu to walk through toggles, or edit `pupdate_settings.json` for advanced options.

---

## Interactive menu

The full menu tree is documented in **[MENU.md](MENU.md)**.

---

## What Pupdate does (overview)

| Area | What it covers |
|------|----------------|
| **Cores** | Downloads and updates openFPGA cores from the library inventory; optional local inventory files; per-core skip, assets, rename, display modes, extras, and **version pinning** |
| **Firmware** | Checks for and installs Analogue Pocket firmware |
| **Assets** | ROMs, BIOS, and other files from configured archives (archive.org, custom, core-specific) |
| **Pocket setup** | Display modes, image packs, library images, Game Boy palettes, instance JSON (e.g. PC Engine CD), Game & Watch ROM build flow, Super GameBoy aspect ratio, Analogizer config, Patreon email, debug helpers |
| **Maintenance** | Update/install/reinstall/uninstall cores, ROM set archives, prune save states, clear archive cache, pin/unpin core versions |
| **Extras** | Pocket Extras (additional assets, combination platforms, variant cores) from `pocket_extras.json` |

---

## RTFM (quick links)

[Update All](#update-all) · [Update Firmware](#update-firmware) · [Select Cores](#select-cores) · [Download Assets](#download-assets) · [Backup Saves & Memories](#backup-saves--memories) · [Pocket Setup](#pocket-setup) · [Pocket Maintenance](#pocket-maintenance) · [Pocket Extras](#pocket-extras) · [Pin / unpin core version](#pin--unpin-core-version) · [Settings](#settings) · [Additional settings](#additional-settings) · [Asset archives](#asset-source-archives) · [CLI](#cli-commands-and-parameters) · [Jotego beta](#jotego-beta-cores) · [Analogizer](#analogizer-setup) · [Coin-Op beta](#coin-op-collection-beta-cores) · [Troubleshooting](#troubleshooting) · [Developers](#developers)

### Update All

The “do everything” option. Steps marked with * follow your **Settings** toggles.

1. Check for firmware updates *
2. Compress and backup Saves and Memories *
3. Install/update every **selected** core
4. Download missing required assets for selected cores *
5. Remove cores you have not selected (when that option is enabled) *
6. Run the instance JSON builder for supported cores (e.g. PC Engine CD) when enabled *
7. Rename Jotego cores to friendly titles when enabled *

**Pinned versions:** If a core has `pinned_version` set in settings, the updater resolves that version from the **inventory release list** only. If the pin does not match any listed release, that core is skipped with a message and the run continues.

### Update Selected

*(Pocket Maintenance)* — Pick installed cores to update.

### Install Selected

*(Pocket Maintenance)* — Pick cores you do not have installed yet; installs them without running a full Update All.

### Update Firmware

Checks for Pocket firmware updates only, then exits.

### Select Cores

Asks how **new** cores should be handled:

- **Yes** — All current cores selected; new cores auto-install as they appear.
- **No** — New cores are not installed automatically; you are not prompted. You then choose from the full list which cores to manage.
- **Ask** — Each run can prompt for new cores; you still choose from the full list for your baseline.

### Download Assets

Fetches missing assets (ROMs, BIOS, etc.) for **selected** cores from your configured archives.

*You supply your own ROMs for non-arcade cores where the archive does not provide them.*

### Backup Saves & Memories

Zips the Pocket **Saves** and **Memories** folders to the path in settings (default: `Backups` next to the executable unless overridden).

---

## Pocket Setup

### Display modes

- **Enable Recommended Display Modes** — Applies the curated list from [`display_modes.json`](display_modes.json) to the cores that reference those modes.
- **Enable Selected Display Modes** — You pick modes from the full list; they are applied to **all** installed cores that support them.
- **Enable Selected Display Modes for Selected Cores** — Same picker, then you choose which installed cores receive them.
- **Reset All / Selected Customized Display Modes** — Restore core defaults.
- **Change Display Modes Option Setting** — **Merge** (combine with core defaults), **Overwrite** (replace), or **Ask** each time.

To override the hosted `display_modes.json`, place a copy next to the executable and set `use_local_display_modes` to `true`.

### Download Platform Image Packs

Lists image packs (from [`image_packs.json`](image_packs.json)), downloads and extracts to `Platforms/_images`.

### Download Pocket Library Images

Downloads Spiritualized1997’s system library images for Pocket Library.

### Download GameBoy Palettes

Downloads palette packs maintained by [davewongillies](https://github.com/davewongillies/openfpga-palettes) and others (see original palette credits in prior releases); large collection of official and community palettes.

### Generating instance JSON files (PC Engine CD)

- Supported workflow is centered on **PC Engine CD**; put games under `/Assets/{platform}/common`, one game per folder (full game title as folder name).
- Games should be **cue/bin**. The generated JSON uses the **cue file name** as the base name—name the cue with the full title too.
- **Update All** or **Generate Instance JSON Files** walks `common` and builds launch JSONs.
- Disable in Update All with `build_instance_jsons`: `false` in settings.
- Optional: local [`ignore_instance.json`](ignore_instance.json) with `use_local_ignore_instance_json` if you maintain exclusions.

### Generate Game & Watch ROMs

Create:

- `/Assets/gameandwatch/agg23.GameAndWatch/artwork`
- `/Assets/gameandwatch/agg23.GameAndWatch/roms`

Put `[game].zip` in each; run the menu item to build. Game & Watch **asset downloads** are configured under `archives` (core-specific archive for `agg23.GameAndWatch`)—enable/configure there rather than a separate global toggle.

### Super GameBoy aspect ratio

Apply **8:7** to chosen `Spiritualized.SuperGB*` cores, or restore **4:3**.

### Analogizer Config

**Jotego** vs **Standard** wizards for Analogizer JSON. See [Analogizer setup](#analogizer-setup).

### Set Patreon Email Address

Used with **Coin-Op Collection beta** license fetch; also editable here if you change Patreon email.

### Print openFPGA Category Structure

Prints a summary of platform categories and cores (debug / reference).

---

## Pocket Maintenance

- **Update Selected / Install Selected** — See above.
- **Reinstall All / Select Cores** — Clean reinstall of core files (ROMs and saves are not removed).
- **Uninstall Select Cores** — Optional removal of core-specific **Assets**; ROMs/saves untouched.
- **Manage ROM Set Archives** — Sync and manage ROM set definitions ([`romsets.json`](romsets.json)).
- **Prune Save States** — Trims old save states under Memories.
- **Clear Archive Cache** — Deletes cached archive downloads when **cache archive files** is enabled.
- **Pin/Unpin Core Version** — See [next section](#pin--unpin-core-version).

---

## Pin / unpin core version

For installed cores with a GitHub-backed inventory entry:

1. Choose **Unpin**, **Select from releases list**, **Enter version manually**, or **Go Back**.
2. **Select from releases list** opens a **paginated** list of versions from the inventory (newest-first by release date). Pick one to pin.
3. **Enter version manually** stores any string; it must match a **`metadata.version`** value in the inventory’s `releases` array for updates to succeed.

**Update behavior:** The pinned ZIP URL is taken **only** from the inventory release that matches the pin (with simple `v` prefix normalization). There is **no** GitHub API fallback for pins. If the pin is missing from `releases`, the core is **skipped** for that run with a clear message.

---

## Pocket Extras

Third-party bundles defined in [`pocket_extras.json`](pocket_extras.json). Each entry can show a description and links before install (`show_menu_descriptions` in settings).

- **Additional Assets** — Needs the base core; extends ROMs or multi-game support.
- **Combination Platforms** — Multiple cores under one platform entry.
- **Variant Cores** — Alternate core packages alongside the original.

Exact menu labels are built at runtime; see **[MENU.md](MENU.md)** for a current snapshot.

---

## Settings

Toggles exposed in the **Settings** menu (stored in `pupdate_settings.json`):

| Name | Description |
|------|-------------|
| Download Firmware Updates | Firmware check during Update All |
| Download Missing Assets | Asset check during Update All |
| Build game JSON Files | Instance JSON builder during Update All |
| Delete cores, not managed by pupdate | Remove unmanaged cores from SD when enabled |
| Automatically rename Jotego cores | Friendly platform names after install |
| Use CRC check | Verify assets by CRC; re-download if mismatch |
| Preserve Platforms folder | Avoid overwriting `Platforms` on Update All |
| Skip alternative ROMs | Ignore `_alternatives` folders when fetching assets (default on) |
| Compress and backup Saves and Memories | Backup during Update All |
| Show Menu Descriptions | Prompts/descriptions for Pocket Extras and similar |
| Use custom archive | Use the preconfigured “custom” archive entry instead of default archive.org |
| Automatically install updates to Pupdate | Self-update without prompt on startup |
| Use Local Pocket Extras | Use local `pocket_extras.json` |
| Use Local Display Modes | Use local `display_modes.json` |
| Cache downloaded archive files locally | Keep a reusable cache; optional cache path in JSON |
| Coin-Op Collection Beta Access | Enables beta license flow (see [Coin-Op](#coin-op-collection-beta-cores)) |
| Adds a description element to the video.json display modes | Non-breaking extra field in generated video JSON |
| Hide and uninstall Analogizer core variants | Hides Analogizer-specific core variants |

Game & Watch (and other **core-specific** archives) are enabled in the `archives` array in JSON, not via a separate Settings row.

---

## Additional settings

Edit `pupdate_settings.json` for keys that are not bool menu toggles:

| Name | Description |
|------|-------------|
| `config.github_token` | Used for GitHub API calls (rate limits) |
| `config.download_new_cores` | `yes` / `no` / `ask` — set by Select Cores |
| `config.display_modes_option` | `merge` / `overwrite` / `ask` |
| `config.patreon_email_address` | Patreon email for Coin-Op beta license |
| `config.backup_saves_location` | Backup output directory |
| `config.temp_directory` | Override temp extract path (default: under install path) |
| `config.archive_cache_location` | Override archive cache directory when caching is on |
| `config.suppress_already_installed` | Reduce “already installed” console noise |
| `config.use_local_cores_inventory` | Use local **`cores.json`** and **`platforms.json`** (openFPGA Library **v3** format) next to the executable |
| `config.use_local_blacklist` | Use local `blacklist.json` instead of downloading |
| `config.use_local_image_packs` | Use local `image_packs.json` |
| `config.use_local_ignore_instance_json` | Use local `ignore_instance.json` |
| `archives` | Archive definitions: `internet_archive`, `custom_archive`, `core_specific_archive`, `core_specific_custom_archive` (see below) |
| `credentials.internet_archive` | Optional archive.org username/password |
| `core_settings.<core_id>.*` | Per core: `skip`, `download_assets`, `platform_rename`, `pocket_extras`, `pocket_extras_version`, `display_modes`, `original_display_modes`, `selected_display_modes`, `requires_license`, **`pinned_version`** |

### Asset blacklist

[`blacklist.json`](blacklist.json) lists filenames **not** downloaded as assets. Entries support `*` and `?` wildcards; matching applies to the full slot path and to the file name alone (useful for paths like `subfolder/game.bin`).

---

## Asset source archives

The `archives` array in `pupdate_settings.json` defines where assets come from. Stock config includes a default **archive.org** archive and a **Retrodriven** custom archive; you can switch via **Use custom archive** or edit JSON.

**Types**

- **`internet_archive`** — `name`, `archive_name` (archive.org identifier).
- **`custom_archive`** — `name`, `url`, `index` (index format per [archive.org JSON API](https://archive.org/developers/md-read.html)).
- **`core_specific_archive`** — Same as internet_archive but scoped to one core `name`; optional `file_extensions` **or** `files` (not both); optional `one_time`.
- **`core_specific_custom_archive`** — Like core-specific but with `url` + `index`.

Example `core_specific_archive`:

```json
{
    "name": "Spiritualized.2600",
    "type": "core_specific_archive",
    "archive_name": "htgdb-gamepacks",
    "files": [
      "@Atari 2600 2021-04-06.zip"
    ],
    "enabled": true,
    "one_time": true
}
```

---

## Internet Archive credentials

Some items need login. Add to `pupdate_settings.json`:

```json
"credentials": {
  "internet_archive": {
    "username": "me@something.com",
    "password": "12345"
  }
}
```

---

## CLI commands and parameters

```
  menu                     Interactive main menu (default)
    -p, --path                Absolute path to Pocket install / SD root
    -s, --skip-update         Open menu without checking for pupdate self-update

  fund                     List sponsor / funding links
    -p, --path
    -c, --core                Optional core id filter

  update                   Run Update All (respects settings)
    -p, --path
    -c, --core                Optional single core id
    -r, --clean               Clean reinstall cores

  uninstall                Remove a core
    -p, --path
    -c, --core                Required
    -a, --assets              Also remove Assets/{platform}/{core}

  assets                   Download assets only
    -p, --path
    -c, --core                Optional core id

  firmware                 Check firmware
    -p, --path

  images                   Download a platform image pack
    -p, --path
    -o, --owner               Repo owner
    -i, --imagepack           Repo name
    -v, --variant             Optional variant

  instance-generator       Run instance JSON generation (PC Engine CD workflow)
    -p, --path

  backup-saves             Backup Saves & Memories
    -p, --path
    -l, --location            Backup folder (required)
    -s, --save                Persist location in config for Update All

  gameboy-palettes         Download Game Boy palette pack
    -p, --path

  pocket-library-images    Download Pocket library images
    -p, --path

  pocket-extras            Install a Pocket Extra by id
    -p, --path
    -n, --name                Extra id (required for install)
    -i, --info                Show details for name
    -l, --list                List all extras

  display-modes            Apply **recommended** display modes (same as curated menu action)
    -p, --path

  prune-memories           Prune old save states (Memories)
    -p, --path
    -c, --core                Optional core id

  analogizer-setup         Run Analogizer setup wizard
    -p, --path
    -j, --jotego              Jotego-specific wizard

  clear-archive-cache      Clear cached archive downloads (same as Pocket Maintenance → Clear Archive Cache)
    -p, --path
    -y, --yes                Required confirmation flag

  update-self              Check for pupdate updates

  help                     Help for a verb

  version                  Print version
```

**Examples**

```text
pupdate -p /path/to/sdcard/
pupdate update -c boogermann.bankpanic
pupdate assets -c jotego.jtcontra
pupdate images -i pocket-platform-images -o dyreschlock -v home
```

---

## Jotego beta cores

Place **`jtbeta.zip`** from Patreon on the **root of the SD card** (exact name). **Update All** copies the beta key into the folders that need it. Do not rename the file.

---

## Analogizer setup

**Pocket Setup → Analogizer Config** — choose **Standard** ([RndMnkIII](https://github.com/RndMnkIII)) or **Jotego** flow. For problems with generated files, ask the core author. [Supported cores](https://github.com/RndMnkIII/Analogizer/wiki/Supported-Cores-and-How-to-Configure-Them).

---

## Coin-Op Collection beta cores

1. Enable **Coin-Op Collection Beta Access** in Settings.
2. On the next Update All, enter the **Patreon email** tied to your subscription (or set it under **Pocket Setup → Set Patreon Email Address**).
3. Later runs can refresh the license automatically.

---

## Troubleshooting

- **Slow assets** — Try **Use custom archive** or another archive entry in `archives`.
- **`Error in framework RS: bridge not responding`** — Run pupdate on a local disk, then copy results to the SD card.
- **`Missing ROM ID [1]` in `_alternatives`** — Turn **off** “Skip alternative ROMs” if you need those files installed.
- **Pinned core skipped** — Pin must match a version string present in the core’s inventory `releases` list.

---

## Developers

- **.NET** — `dotnet restore`, `dotnet build`, `dotnet run --project pupdate.csproj -- [verb] [options]`.
- **Layout** — `src/Program.cs`, `src/partials/`, `src/services/`, `src/models/`, `src/options/`.
- **Legacy** — `pupdate_legacy.csproj` targets .NET Framework where applicable.

---

## Submitting new cores

[openfpga-cores-inventory / analogue-pocket](https://github.com/openfpga-cores-inventory/analogue-pocket)

---

## Credits

Thanks to [neil-morrison44](https://github.com/neil-morrison44) — this project builds on [his original script](https://gist.github.com/neil-morrison44/34fbb18de90cd9a32ca5bdafb2a812b8).

**With special thanks to**  
[Michael Hallett](https://github.com/hallem) · [RetroDriven](https://github.com/RetroDriven/) · [dyreschlock](https://github.com/dyreschlock/pocket-platform-images/tree/main/arcade/Platforms) · [espiox](https://github.com/espiox)

---

## Other updaters

- [Pocket_Updater](https://github.com/RetroDriven/Pocket_Updater) (GUI, more features)  
- [pocket-sync](https://github.com/neil-morrison44/pocket-sync) (cross-platform)
