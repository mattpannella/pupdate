
[![Current Release](https://img.shields.io/github/v/release/mattpannella/pupdate?label=Current%20Release)](https://github.com/mattpannella/pupdate/releases/latest) ![Downloads](https://img.shields.io/github/downloads/mattpannella/pupdate/latest/total?label=Downloads) [![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate/?business=YEERX89E75HQ8&no_recurring=1&currency_code=USD)

A free utility for updating openFPGA cores, firmware, and related assets on your Analogue Pocket. It can run as an **interactive console menu** or from the **command line**.

A browsable list of available cores is here: [openFPGA cores inventory](https://openfpga-cores-inventory.github.io/analogue-pocket/).

Please use the [latest release](https://github.com/mattpannella/pupdate/releases/latest/) before reporting issues; older builds are not supported.

---

## Easy mode

Do **not** clone the repo if you only want to run the tool. Download the latest release, unzip it, place the executable for your OS on the **root of your SD card** (or another folder you use as the install path), and run it.

Use **Settings** from the main menu to walk through toggles, or edit `pupdate_settings.json` for advanced options. In the full-screen UI (`--tui`) the same options live on the **Settings** tab, grouped — see [Settings](#settings).

### macOS specific

In most macOS default installation setups, you will likely encounter issues running the script the "easy way". The following command will remove the quarantine from the pupdate script only. ⚠️ Use sparingly ⚠️

```shell
xattr -d com.apple.quarantine /path/to/pupdate
```
 
---

## Interactive menu

The full menu tree is documented in **[MENU.md](MENU.md)**.

---

## What Pupdate does (overview)

| Area | What it covers |
|------|----------------|
| **Cores** | Downloads and updates openFPGA cores from the library inventory; optional local inventory files; per-core skip, assets, display modes, extras, and **version pinning** |
| **Firmware** | Checks for and installs Analogue Pocket firmware |
| **Assets** | ROMs, BIOS, and other files from configured archives (archive.org, custom, core-specific) |
| **Pocket setup** | Display modes, image packs, library images, Game Boy palettes, instance JSON (e.g. PC Engine CD), Game & Watch ROM build flow, Super GameBoy aspect ratio, **Directory Locations** (backup saves path, archive cache, temp directory), GitHub token (API rate limits), debug helpers |
| **Maintenance** | Update/install/reinstall/uninstall cores, ROM set archives, prune save states, clear archive cache, pin/unpin core versions, **validate/repair core JSON** |
| **Extras** | Pocket Extras (additional assets, combination platforms, variant cores) from `pocket_extras.json` |
| **Plugins** |  Plugins for custom functionality |

---

## RTFM (quick links)

[Update All](#update-all) · [Update Firmware](#update-firmware) · [Select Cores](#select-cores) · [Download Assets](#download-assets) · [Backup Saves & Memories](#backup-saves--memories) · [Pocket Setup](#pocket-setup) · [Pocket Maintenance](#pocket-maintenance) · [Pocket Extras](#pocket-extras) · [Plugins](#plugins) · [Pin / unpin core version](#pin--unpin-core-version) · [Settings](#settings) · [Additional settings](#additional-settings) · [Asset archives](#asset-source-archives) · [CLI](#cli-commands-and-parameters) · [Jotego beta](#jotego-beta-cores) · [Moved to plugins](#features-that-moved-to-plugins) · [Troubleshooting](#troubleshooting) · [Developers](#developers)

### Update All

The “do everything” option. Steps marked with * follow your **Settings** toggles.

1. Check for firmware updates *
2. Compress and backup Saves and Memories *
3. Install/update every **selected** core
4. Download missing required assets for selected cores *
5. Remove cores you have not selected (when that option is enabled) *
6. Run the instance JSON builder for supported cores (e.g. PC Engine CD) when enabled *
7. Check for and install plugin updates *
8. Run any plugins marked **Auto-run during Update All** (before or after the cores, per plugin) *

**Pinned versions:** If a core has `pinned_version` set in settings, the updater resolves that version from the **inventory release list** only. If the pin does not match any listed release, that core is skipped with a message and the run continues.

### Update Selected

*(Pocket Maintenance)* — Pick installed cores to update.

### Install Selected

*(Pocket Maintenance)* — Pick cores you do not have installed yet; installs them without running a full Update All.

### Update Firmware

*(Pocket Maintenance)* — Checks for Pocket firmware updates only. **Update All** already does this when **Download Firmware Updates** is on in Settings. From the command line: `pupdate firmware`.

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

- **Spiritualized1997 (GB, GBC, GBA, GG)** — hardcoded in the app: `Library_Image_Set_v1.0.zip` from your configured archive.
- **All other submenu titles and items** come from [`pocket_library_images.json`](pocket_library_images.json) (bundled next to the executable), same idea as Pocket Extras. Each group has a **`menu_title`** (submenu label) and **`entries`** with **`menu_label`**, **`id`**, **`github_user`**, **`github_repository`**, and **`sources`**: an array of **`release_asset`** (zip on the release), **`path_under_extract`** (folder path inside that zip to `.bin` files, `/`-separated), and **`dest_images_subfolder`** (under `System/Library/Images/` on the device). Optional **`post_install_note`** is printed after a successful install (when any files were copied). Enable **Use a local pocket_library_images.json file** in Settings to read only the local copy instead of fetching the catalog from GitHub (see `use_local_pocket_library_images` in `pupdate_settings.json`).

CLI (like `pocket-extras`): `pocket-library-images` alone installs Spiritualized. **`pocket-library-images -l`** lists catalog entry **`id`** values; **`-n <id>`** installs that entry; **`-n <id> -i`** prints details. Example: `pocket-library-images -n codewario_boxarts`.

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

### Directory Locations

Submenu under **Pocket Setup**.

- **Set Backup Saves Location** — Folder where **Backup Saves & Memories** writes zip files. Default `Backups` is a relative path (resolved from the process **current directory** when the tool runs; use an absolute path if you need a fixed location). Clearing the value when prompted resets to `Backups`. Same as `config.backup_saves_location` in JSON.
- **Set Archive Cache Location** — When **Cache downloaded archive files locally** is enabled, this overrides the default cache folder under your user **LocalApplicationData** `pupdate\cache` (Windows) / equivalent. Leave empty (or save an empty line) to use that default. Same as `config.archive_cache_location`.
- **Set Temp Directory** — Where pupdate extracts downloads and temporary files. Empty means the OS temp folder (`Path.GetTempPath()`). Same as `config.temp_directory`.

### Set GitHub Token

Stores a [personal access token](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens) for GitHub API calls (higher rate limits than anonymous requests). The current value is shown when you open this item. You can also set `config.github_token` in `pupdate_settings.json`.

### Print openFPGA Category Structure

Prints a summary of platform categories and cores (debug / reference).

---

## Pocket Maintenance

- **Update Firmware** — See [Update Firmware](#update-firmware).
- **Update Selected / Install Selected** — See above.
- **Reinstall / Uninstall Cores** — Submenu:
  - **Reinstall All / Select Cores** — Clean reinstall of core files (ROMs and saves are not removed).
  - **Uninstall Select Cores** — Optional removal of core-specific **Assets**; ROMs/saves untouched.
- **Manage ROM Set Archives** — Sync and manage ROM set definitions ([`romsets.json`](romsets.json)).
- **Prune Save States** — Trims old save states under Memories.
- **Clear Archive Cache** — Deletes cached archive downloads when **cache archive files** is enabled.
- **Pin/Unpin Core Version** — See [next section](#pin--unpin-core-version).
- **Archive/Unarchive Platforms** — Moves platform files in and out of `Platforms/_archive` (same as `pupdate platforms`).

---

## Pin / unpin core version

For installed cores with a GitHub-backed inventory entry:

1. Choose **Unpin**, **Select from releases list**, **Enter version manually**, or **Go Back**.
2. **Select from releases list** opens a **paginated** list of versions from the inventory (newest-first by release date). Pick one to pin.
3. **Enter version manually** stores any string; it must match a **`metadata.version`** value in the inventory’s `releases` array for updates to succeed.

**Update behavior:** The pinned ZIP URL is taken **only** from the inventory release that matches the pin (with simple `v` prefix normalization). There is **no** GitHub API fallback for pins. If the pin is missing from `releases`, the core is **skipped** for that run with a clear message.

---

## Pocket Extras

Third-party bundles defined in [`pocket_extras.json`](pocket_extras.json). Each entry can show a description and links before install (`show_menu_descriptions` in settings); the `--tui` **Extras** tab shows them in an **Install** / **Cancel** dialog.

- **Additional Assets** — Needs the base core; extends ROMs or multi-game support.
- **Combination Platforms** — Multiple cores under one platform entry.
- **Variant Cores** — Alternate core packages alongside the original.

Exact menu labels are built at runtime; see **[MENU.md](MENU.md)** for a current snapshot.

---

## Plugins

Pupdate can install and run plugins distributed as GitHub repos. Each plugin is a small program that can drive its own interactive flow inside pupdate. To install ROM packs, configure something on your Pocket, fetch external content, etc. Plugins follow the [pocket-plugin](https://github.com/openfpga-library/pocket-plugin) spec, so the same plugin works in pupdate and Pocket Sync. Several features that used to be built into pupdate are now plugins; see [Features that moved to plugins](#features-that-moved-to-plugins).

A plugin sees your whole SD card (mounted as `pocket/`) and gets a private folder on your computer for its own data. It can only reach the web hosts its `plugin.json` lists.

### Installing a plugin

**Plugins → Browse plugin catalog...** lists the plugins in [`plugins.json`](plugins.json). Pick one to see its description and links, then install it. To get a plugin listed, open a pull request that adds it to `plugins.json`.

**Install from GitHub...** installs anything else. Paste either:

- a short slug like `owner/repo`, or
- a full URL like `https://github.com/owner/repo`

pupdate downloads the latest release of that repo. Example to try: `openfpga-library/pocket-plugin`.

Plugins live in a `plugins` folder on the SD card, next to your settings and cores (the path given with `-p`, or where pupdate runs from). Override it with `config.plugins_directory`.

### Running a plugin

After installing, the plugin appears in the same **Plugins** menu by its name. Select it and follow its prompts. Pick **Cancel** at any prompt to stop the plugin.

### Running plugins during Update All

**Plugins → Auto-run during Update All...** (in the TUI: **Plugins** tab, select the plugin, **Run during Update All**) marks a plugin to run on every **Update All**, in one of two phases:

- **before cores** - runs ahead of the core install loop. Use this for plugins that fetch license keys, so license gated cores aren't skipped.
- **after cores** - runs once cores and assets are done (default). Use this for plugins that build game files or touch installed cores.

Plugins may still ask questions during Update All. With `update -y` (unattended) any question is answered with **Cancel** and the plugin is listed as "skipped (need input)" in the summary. Plugin errors won't stop 
the update from completing.

Turn on **Check for and install plugin updates during 'Update All'** in Settings (off by default) to have every installed plugin checked against its GitHub release and updated at the start of Update All, before any plugin runs.

### Recommended plugins (for core authors)

A core can recommend plugins in its `updaters.json`:

```json
{
  "plugins": ["https://github.com/GuardianSoul/openfpga-PlayStation-plugin"]
}
```

When pupdate installs that core for the first time, it offers to install each listed plugin that isn't installed yet.

### Updating and uninstalling

- **Check for updates** - checks every installed plugin against its source repo and offers to update any that have a newer release.
- pupdate also checks once a day when you open the menu or TUI, and asks before updating (or updates straight away if **Check for and install plugin updates during 'Update All'** is on). `--skip-update` skips it.
- **Uninstall a plugin...** - pick one from the list and confirm.

In the TUI, selecting an installed plugin on the **Plugins** tab opens a menu with **Run now**, **Run during Update All**, **Check for update** and **Uninstall**; **Check for plugin updates** at the bottom of the tab checks all of them.

### CLI

```
pupdate plugins -l                                  list installed plugins
pupdate plugins -b                                  list the catalog
pupdate plugins -i coinop-license                   install
pupdate plugins -r coinop-license                   run a plugin (-y answers prompts with Cancel)
pupdate plugins -u                                  update all plugins (-n limits to one)
pupdate plugins --set-auto coinop-license --phase before
pupdate plugins --unset-auto coinop-license
pupdate plugins --remove coinop-license
```

Anywhere a plugin is expected you can use its catalog id (shown by `-b` and `-l`), its GitHub `owner/repo`, or its name.

### Creating a plugin

See [openfpga-library/pocket-plugin](https://github.com/openfpga-library/pocket-plugin) for the plugin contract, sample code, and release format. Distribute your plugin as a GitHub release that includes `plugin.wasm` and `plugin.json` as release assets. The release tag becomes the version pupdate uses for update checks.

---

## Settings

In the full-screen UI (`--tui`), **↑/↓** stay inside the current list, **←/→** switch tabs, and
**A**–**G** jump straight to one. Those single-key shortcuts stand down while a text box has focus,
so the **Cores** tab's **Filter** box takes every key - press **/** from the core table to jump into
it and **Esc** to clear it.

Toggles exposed in the **Settings** menu (stored in `pupdate_settings.json`):

| Name | Description |
|------|-------------|
| Download Firmware Updates | Firmware check during Update All |
| Download Missing Assets | Asset check during Update All |
| Only check assets for cores updated during 'Update All' | Skip the asset check for cores that are already up to date; only updated/installed cores get their assets checked |
| Build game JSON Files | Instance JSON builder during Update All |
| Delete cores, not managed by pupdate | Remove unmanaged cores from SD when enabled |
| Use CRC check | Verify assets by CRC; re-download if mismatch |
| Preserve Platforms folder | Avoid overwriting `Platforms` on Update All |
| Skip alternative ROMs | Ignore `_alternatives` folders when fetching assets (default on) |
| Compress and backup Saves and Memories | Backup during Update All |
| Show Menu Descriptions | Prompts/descriptions for Pocket Extras and similar |
| Use custom archive | Use the preconfigured “custom” archive entry instead of default archive.org |
| Automatically install updates to Pupdate | Self-update without prompt on startup |
| Check for and install plugin updates during 'Update All' | Update every installed plugin at the start of Update All (off by default; see [Plugins](#plugins)) |
| Use Local Pocket Extras | Use local `pocket_extras.json` |
| Use Local Display Modes | Use local `display_modes.json` |
| Use a local plugins.json file | Use local `plugins.json` for the plugin catalog |
| Cache downloaded archive files locally | Keep a reusable cache; optional cache path in JSON |
| Adds a description element to the video.json display modes | Non-breaking extra field in generated video JSON |
| Hide and uninstall Analogizer core variants | Hides Analogizer-specific core variants |
| Hide and uninstall AI-generated cores (Experimental) | Hides and (during Update All) uninstalls cores whose AI score exceeds your threshold. Toggle this on, then set the cutoff with **Settings → Set AI Filter Threshold** (see [AI-generated core filter](#ai-generated-core-filter-experimental)). Cores not included in the report are left alone. |
| Download files in concurrent chunks | Splits downloads into parallel HTTP range requests for faster transfers on bandwidth-limited servers like archive.org; falls back to a single connection when the server doesn't support ranges (default on). Tune the parallelism with `config.download_chunk_count` |

Game & Watch (and other **core-specific** archives) are enabled in the `archives` array in JSON, not via a separate Settings row.

---

## Additional settings

Edit `pupdate_settings.json` for keys that are not bool menu toggles:

| Name | Description |
|------|-------------|
| `config.github_token` | Used for GitHub API calls (rate limits); also settable under **Pocket Setup → Set GitHub Token** (classic) or **Settings → Accounts → GitHub token** (`--tui`) |
| `config.download_new_cores` | `yes` / `no` / `ask` — set by Select Cores |
| `config.display_modes_option` | `merge` / `overwrite` / `ask`; **Pocket Setup → Manage Display Modes → Change Display Modes Option Setting** (classic) or **Settings → Display Modes → Merge or overwrite default** (`--tui`) |
| `config.backup_saves_location` | Backup output directory; **Pocket Setup → Directory Locations → Set Backup Saves Location** (classic) or **Settings → Update All → Backup saves location** (`--tui`) |
| `config.temp_directory` | Override temp extract path (default: OS temp); **Pocket Setup → Directory Locations → Set Temp Directory** (classic) or **Settings → Assets & Downloads → Temp directory** (`--tui`) |
| `config.archive_cache_location` | Override archive cache directory when caching is on; **Pocket Setup → Directory Locations → Set Archive Cache Location** (classic) or **Settings → Assets & Downloads → Archive cache location** (`--tui`) |
| `config.download_chunk_count` | Number of parallel HTTP range chunks when **Download files in concurrent chunks** is on (default `4`); ignored for files under 1 MB or servers without range support |
| `config.ai_core_threshold` | Percentage `0`–`100` for the **Hide and uninstall AI-generated cores** filter; cores scoring **over** this value are hidden and uninstalled (defaults to `100`, so no cores will ever be marked by default). Set via **Settings → Set AI Filter Threshold** (classic) or **Settings → Core Filtering → AI score threshold** (`--tui`) |
| `config.suppress_already_installed` | Reduce “already installed” console noise |
| `config.use_local_cores_inventory` | Use local **`cores.json`** and **`platforms.json`** (openFPGA Library **v3** format) next to the executable |
| `config.use_local_blacklist` | Use local `blacklist.json` instead of downloading |
| `config.use_local_image_packs` | Use local `image_packs.json` |
| `config.use_local_ignore_instance_json` | Use local `ignore_instance.json` |
| `archives` | Archive definitions: `internet_archive`, `custom_archive`, `core_specific_archive`, `core_specific_custom_archive` (see below) |
| `credentials.internet_archive` | Optional archive.org username/password |
| `core_settings.<core_id>.*` | Per core: `skip`, `download_assets`, `pocket_extras`, `pocket_extras_version`, `display_modes`, `original_display_modes`, `selected_display_modes`, `requires_license`, **`pinned_version`** |
| `config.plugins_directory` | Where plugins are installed (default: `plugins` in the install path, i.e. the SD card) |
| `plugin_settings.<owner.repo>.*` | Per plugin: `repo`, `run_on_update`, `phase` (`before` / `after`); set via **Auto-run during Update All** or `pupdate plugins --set-auto` |

### Asset blacklist

[`blacklist.json`](blacklist.json) lists filenames **not** downloaded as assets. Entries support `*` and `?` wildcards; matching applies to the full slot path and to the file name alone (useful for paths like `subfolder/game.bin`).

### AI generated core filter (Experimental)

pupdate can mark or filter out cores that appear to be AI-generated, using the [openFPGA AI check report](https://openfpga-library.github.io/openfpga-ai-check/ai_report.json) built by [neil-morrison44](https://github.com/neil-morrison44). Each core is given a score from `0` to `1`. The closer to 1, the more likely it is to be AI generated.

**How the score is calculated:** An automated tool ([openfpga-library/openfpga-ai-check](https://github.com/openfpga-library/openfpga-ai-check)) regularly looks through each core's public development history for signs that AI tools were used. For example, how often "co-authored by Claude" appears in commit messages. The more signs detected, the higher the score. A core that looks entirely AI made scores near `1`, one that only had occasional AI help scores lower. Older cores from before AI tools were common are given a 0. Keep in mind this is an educated guess and it can be inaccurate. Core devs will be able to submit requests to fix incorrect detections. Use at your own risk (the risk really just being you will have some cores hidden from you with false positives)

- Set the cutoff with **Settings → Set AI Filter Threshold** (**Settings → Core Filtering → AI score threshold** in `--tui`) - a percentage (`0`–`100`, defaults to `100`). Cores that score **over** the threshold are labeled or hidden. Example: a threshold of `50` targets cores scoring above `0.50`.
- **Filter off (default):** over-threshold cores are left in place but marked as **(AI)** on the **Select Cores** screen, so you can spot them and manually deselect the ones you don't want.
- **Filter on** (turn on **Hide and uninstall AI-generated cores (Experimental)** in the **Settings** menu): over threshold cores are removed from **Select Cores**, updates, and asset downloads, and any that are already installed are uninstalled during the next **Update All**.
- Cores that aren't scored at all are never marked or hidden. Set the threshold to `100` to turn everything off (nothing scores above `1.0`), including the labels.
- In the full-screen UI (`--tui`), the **Cores** tab's details popup (Enter, or click **[view]**) shows the full report for that core: its score against your threshold, each individual check with the evidence it recorded (e.g. "Of the last 100 commit messages 97 mention Claude"), and when it was last analyzed. Cores missing from the report show as **not scored**.

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
    -u, --updated-assets-only Only check/download assets for cores updated this run
    -y, --yes                 Non-interactive: assume defaults for all prompts (CI/cron)

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
    -n, --name                Catalog entry id (omit for Spiritualized archive)
    -i, --info                Show details for -n
    -l, --list                List catalog ids (Spiritualized = no -n)

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

  clear-archive-cache      Clear cached archive downloads (same as Pocket Maintenance → Clear Archive Cache)
    -p, --path
    -y, --yes                Required confirmation flag

  validate-cores           Check installed cores for missing or invalid JSON
    -p, --path
    -f, --fix                Reinstall (clean) any cores with missing or invalid JSON

  update-self              Check for pupdate updates

  help                     Help for a verb

  version                  Print version
```

**Non-interactive use (`-y` / `--yes`):** a global flag. With it, pupdate assumes the default/affirmative answer to every prompt (install new cores, display modes, etc.) and skips the self-update prompt, so it can run unattended (CI, cron, containers). pupdate also returns a **non-zero exit code** when an update fails or an unhandled error occurs, so scripts can detect failures.

**Examples**

```text
pupdate -p /path/to/sdcard/
pupdate update -c boogermann.bankpanic
pupdate update -p /path/to/sdcard/ --yes      # unattended; non-zero exit on failure
pupdate assets -c jotego.jtcontra
pupdate images -i pocket-platform-images -o dyreschlock -v home
pupdate validate-cores
pupdate validate-cores --fix
```

---

## Jotego beta cores

Place **`jtbeta.zip`** from Patreon on the **root of the SD card** (exact name). **Update All** copies the beta key into the folders that need it. Do not rename the file.

To have pupdate download `jtbeta.zip` for you instead, install the **Jotego Beta Key** plugin (see [Features that moved to plugins](#features-that-moved-to-plugins)). A `jtbeta.zip` on the SD root always wins over the plugin.

---

## Features that moved to plugins

A few features that only served one core author or one adapter were moved out of pupdate into [plugins](#plugins), so they can be maintained on their own and shared with Pocket Sync. Each is in the plugin catalog (**Plugins → Browse plugin catalog...**, or `pupdate plugins -i <id>`):

| Was | Plugin | Catalog id | Suggested auto-run |
|-----|--------|------------|--------------------|
| Pocket Setup → Analogizer Config → Standard | [Analogizer Config](https://github.com/mattpannella/pocket-plugin-analogizer-config) | `analogizer-config` | no (run it when you change hardware) |
| Pocket Setup → Analogizer Config → Jotego | [Jotego Analogizer Config](https://github.com/mattpannella/pocket-plugin-jotego-analogizer-config) | `jotego-analogizer-config` | no |
| Setting **Auto-fetch Coin-Op Collection license** | [Coin-Op License](https://github.com/mattpannella/pocket-plugin-coinop-license) | `coinop-license` | before cores |
| Setting **Automatically rename Jotego cores** (and `platform_rename` per core) | [Jotego Platform Names](https://github.com/mattpannella/pocket-plugin-jotego-platform-names) | `jotego-platform-names` | after cores |
| Settings **Auto-fetch Jotego jtbeta.zip from GitHub / Patreon**, Pocket Setup → Patreon Config | [Jotego Beta Key](https://github.com/mattpannella/pocket-plugin-jotego-beta-key) | `jotego-beta-key` | before cores |

Also removed: the `analogizer-setup` CLI verb (use `pupdate plugins -r analogizer-config` or `pupdate plugins -r jotego-analogizer-config`).

**Upgrading:** the first time you launch the menu after upgrading, pupdate checks the retired settings in your `pupdate_settings.json`. For each one that was on it asks once whether to install the replacement plugin, then drops the old key. If you answer no you can install the plugin later from the catalog. With `pupdate update -y` the offer is printed as a one-line hint and asked again on the next interactive launch. The Jotego Platform Names offer only appears if you have a `jotego.*` core installed.

Notes:

- The Coin-Op plugin reads the same `<serial>.ID` file from the SD card root that pupdate used, so nothing to re-enter.
- The Jotego Beta Key plugin asks for your GitHub token or Patreon `session_id` cookie the first time it runs and keeps it in `Plugins/jotego-beta-key/` on the SD card. pupdate no longer stores `patreon_session_cookie`; `github_token` stays in settings for pupdate's own API calls. See the plugin's README for the sponsor / cookie steps.
- Plugins that need input are skipped during `update -y`; mark the Coin-Op and Jotego Beta Key plugins to auto-run **before cores** so license-gated cores aren't skipped once the key is present.
- The Analogizer plugins write the same `Assets/analogizer/common/analogizer.bin` and `Assets/jtpatreon/common/crtcfg.bin` files as before, and mark the current setting when you re-run them.

---

## Troubleshooting

- **Slow assets** — Try **Use custom archive** or another archive entry in `archives`.
- **`Error in framework RS: bridge not responding`** — Run pupdate on a local disk, then copy results to the SD card.
- **`Missing ROM ID [1]` in `_alternatives`** — Turn **off** “Skip alternative ROMs” if you need those files installed.
- **Pinned core skipped** — Pin must match a version string present in the core’s inventory `releases` list.
- **GitHub API rate limit** — Add a token via **Pocket Setup → Set GitHub Token** or `config.github_token` in `pupdate_settings.json`.
- **`Failure processing application bundle` / `Default extraction directory ... is not accessible`** — Happens when running in a container (or any environment) where `$HOME` points to a directory that does not exist or is not writable. Either set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` (or `HOME`) to a writable path, or download `pupdate_linux_container*.zip` from the release instead of `pupdate_linux*.zip`. The container variant places the native libs next to the binary so no runtime extraction is needed. Extract the whole archive and run `pupdate` from there.

---

## Developers

- **.NET** — `dotnet restore`, `dotnet build`, `dotnet run --project pupdate.csproj -- [verb] [options]`.
- **Layout** — `src/Program.cs`, `src/partials/`, `src/services/`, `src/models/`, `src/options/`, `src/tui/` (the `--tui` interface).

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
