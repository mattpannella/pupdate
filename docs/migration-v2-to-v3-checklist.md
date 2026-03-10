# Migration from openFPGA Library API v2 to v3

This checklist covers everything that needs to be updated to switch from the v2 API to the v3 API ([openfpga-library/analogue-pocket](https://github.com/openfpga-library/analogue-pocket)), with the application automatically choosing the **latest release** per core.

---

## 1. API endpoints and fetching

### 1.1 `src/services/CoresService.cs`

- **Change `CORES_END_POINT`** from:
  - `https://openfpga-library.github.io/analogue-pocket/api/v2/cores.json`  
  to:
  - `https://openfpga-library.github.io/analogue-pocket/api/v3/cores.json`
- **Add optional `PLATFORMS_END_POINT`** (v3 exposes platforms separately):
  - `https://openfpga-library.github.io/analogue-pocket/api/v3/platforms.json`
  - Used to resolve `platform_id` → `Platform` (name, category, manufacturer, year) for display and `Core.ToString()`.
- **Parsing**: Replace the current deserialization of `Dictionary<string, List<Core>>` with:
  - Deserialize v3 response into v3-specific DTOs (see Models below).
  - Run a **mapper** that, for each inventory core, picks the **latest release** (e.g. first in list if API returns newest first, or sort by `date_release` descending) and builds one instance of the existing **`Core`** model per core (so the rest of the app still uses `List<Core>`).
- **Local inventory** (`use_local_cores_inventory` + `CORES_FILE`): Decide behavior when loading from `cores.json`:
  - **Option A**: Support only v3-shaped JSON when using local file (document that local file must be v3 format).
  - **Option B**: Detect format (e.g. check if first item has `releases` array) and support both v2 and v3 local files with the same mapper used for remote v3.
- **Error messages**: Update any message that refers to “the cores inventory” or “cores file” if you want to mention v3; otherwise leave generic.

---

## 2. New v3 API models (DTOs)

Add models under `src/models/OpenFPGA_Cores_Inventory/` (or a subfolder like `V3/`) for deserializing the v3 JSON only. Do **not** change the existing `Core`/`Repository`/`Platform`/`Sponsor` usage across the app; keep those as the unified in-memory model.

### 2.1 Cores response

- **`CoresResponseWrapper`** (or reuse name): `{ "data": [ InventoryCore, ... ] }`
- **`InventoryCore`**: `id`, `repository` (with `funding`), `releases` (array of `Release`).
- **`Release`**: `download_url`, `requires_license`, `core` (metadata, framework, dock, hardware), `data` (e.g. `data_slots`), `updaters`.
- **Nested**: `AnalogueCore`/`Metadata` (e.g. `platform_ids[]`, `version`, `date_release`), `Framework`, `Dock`, `Hardware`, `AnalogueData`/`DataSlot`, `Updaters`/`License`/`Previous` as needed. Reuse existing `Pannella.Models.Updater` types where the JSON shape matches (`Updaters`, `License`, `Substitute`/Previous).

### 2.2 Repository and funding (v3)

- **`Repository`** (v3): Same `platform`, `owner`, `name` as today; add **`funding`** (v3 type).
- **`Funding`** (v3): `github` (array of string), `patreon` (string), `custom` (array of string). v3 does not use the extra Sponsor fields (community_bridge, issuehunt, ko_fi, etc.); those can remain null when mapping.

### 2.3 Platforms response (v3)

- **`PlatformsResponseWrapper`**: `{ "data": [ Platform, ... ] }`
- **`Platform`** (v3): `id`, `category`, `name`, `manufacturer`, `year`. Map into existing `Pannella.Models.OpenFPGA_Cores_Inventory.Platform` (category, name, manufacturer, year) keyed by `id` for lookup.

---

## 3. Mapper: v3 → existing `Core`

Implement a single place that turns one `InventoryCore` + (optionally) a `Dictionary<string, Platform>` from platforms.json into one `Core`:

- **`identifier`** ← `InventoryCore.id`
- **`repository`** ← v3 `repository` (map to existing `Repository`; set `funding` → `sponsor` in mapping step below).
- **`download_url`** ← chosen release’s `download_url` (latest release).
- **`platform_id`** ← chosen release’s `core.metadata.platform_ids[0]` (or single platform_id if that’s what v3 has).
- **`version`** ← chosen release’s `core.metadata.version`
- **`release_date`** ← chosen release’s `core.metadata.date_release`
- **`platform`** ← lookup in platforms dictionary by `platform_id`; if missing, consider a fallback (e.g. minimal Platform with `name = platform_id`) so `Core.ToString()` and `core.platform.name` never NRE.
- **`sponsor`** ← map v3 `repository.funding` to existing `Sponsor` (e.g. `github` → list, `patreon` → string, `custom` → list; other Sponsor properties null).
- **`updaters`** ← chosen release’s `updaters` (same shape as current `Updaters`).
- **`requires_license`** ← chosen release’s `requires_license`.
- **`license_slot_*`** — leave unset from API; still filled later by `RequiresLicense()` / installed core data.

“Latest release” rule: use the first element of `releases[]` if the API guarantees newest-first, otherwise sort by `date_release` descending and take the first.

---

## 4. Existing model and call-site changes

### 4.1 `src/models/OpenFPGA_Cores_Inventory/Core.cs`

- **No breaking changes.** Keep `identifier`, `repository`, `platform`, `platform_id`, `download_url`, `version`, `release_date`, `sponsor`, `updaters`, `requires_license`, and the license_slot fields. All of these are populated by the v3 mapper.
- **`Core.ToString()`**: Uses `platform.name`. Ensure the mapper always sets `platform` (from platforms.json or fallback) so this never throws.

### 4.2 `src/models/OpenFPGA_Cores_Inventory/Sponsor.cs`

- **No structural change.** Mapper converts v3 `Funding` → `Sponsor`; extra fields can remain null. `ToString(string padding)` already tolerates nulls.

### 4.3 `src/models/OpenFPGA_Cores_Inventory/Repository.cs`

- **Optional**: If v3 DTO uses a different type for repository (e.g. with `funding`), keep the existing `Repository` as the in-memory type and map from the DTO when building `Core`.

### 4.4 `src/models/OpenFPGA_Cores_Inventory/Platform.cs`

- **No change.** Used as the in-memory type; v3 platforms.json is mapped into this by `id`.

### 4.5 `src/models/Updater/Updaters.cs`, `License.cs`, `Substitute.cs`

- **No change.** v3 release’s `updaters` matches this shape; reuse for the chosen release.

---

## 5. CoresService behavior

### 5.1 Loading and caching

- **`Cores` getter**: After fetching v3 JSON (and optionally platforms.json), run the mapper for each inventory core → `List<Core>`, then apply existing logic (e.g. `no_analogizer_variants` filter, `GetLocalCores()`, ordering). Keep static `CORES` cache as `List<Core>`.
- **Platforms**: If using platforms.json, fetch and cache it (e.g. once per load, or alongside cores) and pass the lookup into the mapper. Consider where to clear cache (e.g. when `CORES` is cleared or on settings reload).

### 5.2 `GetLocalCores()`

- **No change.** Local cores are still built from filesystem + `ReadPlatformJson`; they don’t come from the API.

### 5.3 `RefreshInstalledCores()`

- **No change.** Iterates over `CORES` (now populated from v3) and uses `core.identifier`, `core.sponsor`, etc. Author for sponsors still comes from `ReadCoreJson(core.identifier).metadata.author`.

### 5.4 `Install(Core core, ...)`, `GetDownloadUrlForVersion`, `CheckLicenseFile`

- **No change** as long as the mapper sets `core.repository` (owner, name) and `core.updaters` from the latest release. Pinned versions still use `GetDownloadUrlForVersion(core, pinned_version)` and GitHub API.

---

## 6. CoreUpdaterService

### 6.1 `RunUpdates`

- **`core.version`**: Already set by mapper from latest release; “no releases” and “most recent release” logic stays the same.
- **`core.repository`**: Set by mapper; pinning and GitHub fetch unchanged.
- **`core.platform_id`**, **`core.platform`**: Set by mapper; Jotego rename and `core.platform.name` in summary dictionaries unchanged.
- **`core.download_url`**: Set by mapper for latest; overridden when pinned via `GetDownloadUrlForVersion`.
- **`core.requires_license`**, **`core.updaters`**: From latest release; license checks unchanged.

No changes required in `CoreUpdaterService.cs` if the mapper correctly fills the existing `Core` type.

### 6.2 `DeleteCore`, `JotegoRename`

- **No change.** They only use `Core` properties that the mapper already sets.

---

## 7. Settings and config

### 7.1 `src/services/SettingsService.cs`

- **`InitializeCoreSettings(List<Core> cores)`**: Uses `core.identifier`, `core.requires_license`. No change.
- **`EnableCore` / `DisableCore`**: Use identifier only. No change.

### 7.2 `src/models/Settings/Config.cs`

- **`use_local_cores_inventory`**: Document (in code or README) that when enabled, the local `cores.json` must be v3 format, or implement Option B in §1.1.

---

## 8. Partials and UI

### 8.1 `src/partials/Program.Sponsors.cs`

- **No change.** Uses `InstalledCoresWithSponsors`, `core.sponsor`, `core.identifier`. Mapper sets `sponsor` from v3 `repository.funding`.

### 8.2 `src/partials/Program.Menus.cs`

- Uses `InstalledCores`, `Cores`, `core.identifier`, `core.repository`, `core.platform_id`, `core.platform.name`, pin menu. No change if `Core` is fully populated by mapper.

### 8.3 `src/partials/Program.Menus.Cores.cs`

- **No change.** Works with `List<Core>` and `core.identifier`, `core.requires_license`.

### 8.4 `src/partials/Program.MissingCores.cs`, `Program.PrintOpenFpgaFolders.cs`, `Program.Helpers.cs`, `Program.DisplayModes.cs`, `Program.Menus.DisplayModes.cs`

- **No change.** They only use `Core` and list properties already provided by the mapper.

### 8.5 `src/Program.cs`

- **No change.** Still passes `ServiceHelper.CoresService.Cores` into `CoreUpdaterService` and uses `GetCore(options.CoreName)`.

---

## 9. Other services

### 9.1 `CoresService.Replace.cs`

- **No change.** Replace logic builds a minimal `Core` from `Substitute` (identifier, platform_id); not from API.

### 9.2 `CoresService.License.cs`, `CoresService.Helpers.cs`, `CoresService.Download.cs`, `CoresService.Extras.cs`, `CoresService.Json.cs`, `CoresService.Video.cs`, `CoresService.Jotego.cs`, `CoresService.IgnoreInstanceJson.cs`, `CoresService.DisplayModes.cs`

- **No change** as long as `Core` has the same fields and they are set by the mapper (identifier, repository, platform_id, platform, download_url, version, updaters, requires_license, license_slot_* where applicable).

---

## 10. Documentation

### 10.1 `CLAUDE.md`

- In the “Architecture” or “Key Conventions” section, state that the cores inventory is loaded from the **openFPGA Library API v3** (and optionally platforms.json), and that the app uses the **latest release** per core unless the user pins a version.
- If you document the settings pattern, mention that local cores inventory file (when used) should be v3 format.

### 10.2 `README.md`

- No mandatory change. Current links are to openfpga-cores-inventory (separate from API version). Optionally add a short note that pupdate uses the openFPGA Library API v3 for the cores list.

---

## 11. Tests and tooling

- **Tests**: The repo excludes `tests/**` from the main project; if any tests or fixtures ever parse `cores.json`, they should use v3-shaped JSON and the same mapper.
- **`.github/workflows/updater.php`**: References `pocket_updater_cores.json`; that appears to be a different workflow. No change needed for the v2→v3 migration unless that script is updated to consume the same inventory.

---

## 12. Summary table

| Area | Action |
|------|--------|
| **CoresService.cs** | Switch to v3 URL; add platforms URL; deserialize v3 DTOs; implement v3→Core mapper; apply latest-release rule; handle local file format |
| **New v3 DTOs** | Add models for cores response (InventoryCore, Release, Metadata, etc.) and platforms response; add Funding and map to Sponsor |
| **Existing Core/Sponsor/Platform/Repository** | Keep as-is; mapper fills them from v3 |
| **Updater models** | Reuse for release.updaters |
| **CoreUpdaterService** | No change |
| **SettingsService, Config** | No change (document local file format if needed) |
| **Partials / Program** | No change |
| **Other CoresService partials** | No change |
| **CLAUDE.md** | Note v3 API and latest-release behavior |
| **README.md** | Optional note about v3 |

---

## 13. Implementation order suggestion

1. Add v3 DTOs and platforms response model under `src/models/OpenFPGA_Cores_Inventory/` (or `V3/`).
2. Implement the mapper: InventoryCore + platforms lookup → `Core` (latest release only).
3. In `CoresService.Cores` getter: add fetch of platforms.json (if used), then change fetch + parse to v3 and run mapper; keep rest of pipeline (filter, GetLocalCores, sort).
4. Update `CORES_END_POINT` (and optionally add platforms endpoint and caching).
5. Decide and implement local-file behavior (v3-only or v2/v3 detection).
6. Sanity-check all usages of `core.platform` (and `platform_id`) and ensure mapper and fallback cover edge cases (e.g. missing platform in platforms.json).
7. Update CLAUDE.md (and optionally README), then run full regression (update all, install core, pin version, sponsors menu, etc.).

This keeps the public type of the cores list as `List<Core>` everywhere and confines v3 specifics to fetching, DTOs, and one mapper.
