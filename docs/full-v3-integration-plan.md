# Plan: Full v3 API Integration

The app will use the openFPGA Library v3 API types directly. The Core type is the v3 Core, with the current release and platform set when we load the cores list. No separate in-memory model and no conversion layer.

---

## 1. Current state vs target state

| Aspect | Current | Target |
|--------|--------|--------|
| **Core type** | Separate in-memory Core (different shape) built from v3 via a conversion layer | v3 Core; when we load the list we set each core's current `release`, `platform_id`, and `platform` from the API and catalog |
| **Repository / Platform / funding** | Separate in-memory types converted from v3 | v3 Repository, v3 Platform; use `repository.funding` (Funding) for sponsor display |
| **Cores list** | `List<Core>` where Core is the separate model | `List<Core>` where Core is the v3 Core type |

---

## 2. Design: v3 Core as the app’s Core type

Use the v3 Core type everywhere. When we load the cores list we set each core’s current release and platform so the rest of the app can use a single release and platform without touching `releases[]` or doing lookups.

### 2.1 v3 Core shape (single source of truth)

**From the v3 API (unchanged):**

- `id`
- `repository` (v3 Repository: platform, owner, name, funding)
- `releases` (list of Release)

**Set when we load the cores list:**

- `Release release` – the release we use for this core (e.g. latest by `date_release`)
- `string platform_id` – from `release.core.metadata.platform_ids[0]`
- `Platform platform` – from the platforms catalog by `platform_id` (required; throw if missing)

**Convenience (from the current release):**

- `string download_url` → `release.download_url`
- `string version` → `release.core.metadata.version`
- `string release_date` → `release.core.metadata.date_release`
- `bool requires_license` → `release.requires_license`
- `Updaters updaters` → `release.updaters`

**App state (set later by CoresService / CoreUpdaterService):**

- `string license_slot_id`
- `int license_slot_platform_id_index`
- `string license_slot_filename`

**Naming conventions:**

- Use **`id`** everywhere (v3 name). No `identifier`; all call sites use `core.id`.
- **`release`** is the release we use for this core (latest). No “selected” or “resolved” in the name.
- **`platform_id`** and **`platform`** are the platform for this core (from catalog). No “resolved” prefix.
- **Sponsor display**: use `repository.funding` (Funding). Add `Funding.ToString()` and `ToString(string padding)` for the sponsor menu.

**Repository / Platform**

- Use v3 `Repository` and v3 `Platform` only. Same types from the v3 API and catalog.

**Core.ToString()**

- Use `platform.name` and `id`, e.g. `$"{platform.name} ({id})"`.

### 2.2 Loading the cores list

When building the cores list (remote or local v3 JSON):

1. Deserialize v3 cores and platforms as today.
2. For each core:
   - Choose the release to use (e.g. latest by `date_release`; same rule as today).
   - Set `core.release`.
   - Set `core.platform_id` from `release.core.metadata.platform_ids[0]`.
   - Look up `core.platform` in the platforms catalog by `platform_id`; throw if missing.
   - Set convenience properties from `core.release` (download_url, version, etc.).
3. Cores with no releases or no platform_id are skipped (same as today).

This logic lives in one place (e.g. CoresService when building the list). No separate conversion type or layer.

---

## 3. Call-site and type changes

### 3.1 Property names (v3-native)

- **`core.id`** – use everywhere; remove all `core.identifier` references.
- **`core.platform_id`**, **`core.platform`** – set when loading the list; call sites unchanged except for identifier → id.
- **`core.download_url`**, **`core.version`**, **`core.release_date`**, **`core.requires_license`**, **`core.updaters`** – from the current release; call sites unchanged.
- **`core.repository`** – v3 Repository; `core.repository.owner` / `core.repository.name` unchanged.
- **`core.sponsor`** – remove; use **`core.repository?.funding`** and **`Funding.ToString(string padding)`** for sponsor display.

### 3.2 Files to update (by area)

**Models**

- **V3/Core.cs** – Add: `release`, `platform_id`, `platform`, `download_url`, `version`, `release_date`, `requires_license`, `updaters`, `license_slot_id`, `license_slot_platform_id_index`, `license_slot_filename`. Keep `id`, `repository`, `releases`. Add `ToString()` using `platform.name` and `id`.
- **V3/Funding.cs** – Add `ToString()` and `ToString(string padding)` for sponsor-style display.
- Remove the old in-memory Core/Repository/Platform/Sponsor types and the conversion layer (no separate model, no conversion step that produces another type).

**CoresService**

- **CoresService.cs** – Deserialize v3; when building the list, for each core set `release`, `platform_id`, `platform`, and convenience props. Build `List<Core>` with the v3 Core type. Remove any references to the old model and conversion.
- **CoresService.Helpers.cs** – Use `core.repository.owner` / `core.repository.name`; Core is v3 Core.
- **CoresService.Download.cs** – `core.id`, `core.platform_id`, `core.license_slot_*`.
- **CoresService.Extras.cs** – `core.id`, `core.platform_id`, `core.requires_license`.
- **CoresService.License.cs** – `core.license_slot_*`.
- **CoresService.Replace.cs** – Build v3 Core with `id` and `platform_id` set for replace/uninstall.
- **CoresService.Jotego.cs** – Core is v3 Core.
- **CoresService.Video.cs** – Uses ReadCoreJson (installed core); check for any inventory Core usage.
- **CoresService.Json.cs** – Same.

**CoreUpdaterService.cs**

- `core.id`, `core.platform_id`, `core.platform.name`, `core.download_url`, `core.version`, `core.requires_license`, `core.repository`, `core.license_slot_*`. Pinning still overrides `core.download_url` via GetDownloadUrlForVersion.

**SettingsService.cs**

- `InitializeCoreSettings(List<Core> cores)`; use `core.id`, `core.requires_license`.

**Partials**

- **Program.Menus.Cores.cs** – `core.id`, `core.requires_license`.
- **Program.Menus.cs** – `core.id`, `core.platform.name`, `GetCoreSettings(core.id)`, pin menu.
- **Program.Sponsors.cs** – `core.repository?.funding` and `Funding.ToString(padding)`.
- **Program.MissingCores.cs** – `core.id`.
- **Program.DisplayModes.cs** – `core.id`.
- **Program.Helpers.cs** – `core.id`.
- **Program.PrintOpenFpgaFolders.cs** – `core.id`.

**Program.cs**

- `List<Core>` / GetCore; Core is the v3 Core type.

### 3.3 Replace flow

- **CoresService.Replace.cs** – Build v3 Core with `id` and `platform_id` set for replace/uninstall; same Core type as elsewhere.

---

## 4. Implementation order

1. **Extend v3 Core** – Add `release`, `platform_id`, `platform`, convenience properties (`download_url`, `version`, etc.), `license_slot_*`, and `ToString()` to the v3 Core type.
2. **Funding display** – Add `Funding.ToString()` and `ToString(string padding)`; update Program.Sponsors to use `repository.funding`.
3. **Loading the list** – In CoresService, after deserializing v3 cores and platforms, for each core set `release` (latest), `platform_id`, `platform` (from catalog), and convenience props. Skip cores with no releases or no platform_id.
4. **CoresService** – Build the list using the logic above; use only the v3 Core type; remove references to the old model and conversion layer.
5. **CoreUpdaterService** – Use v3 Core; ensure `id`, `license_slot_*`, `platform`, `repository`.
6. **CoresService partials** – Same Core type; sponsor → `repository.funding` where needed.
7. **SettingsService** – `List<Core>` with v3 Core; `core.id`, `core.requires_license`.
8. **Partials** – `core.id` everywhere; Funding for sponsor.
9. **CoresService.Replace** – Build v3 Core for replace flow with `id` and `platform_id`.
10. **Remove old model and conversion** – Delete the old Core/Repository/Platform/Sponsor types and the conversion layer; remove all usings and references.
11. **Tests / docs** – Update tests and docs; run full regression.

---

## 5. Risk and rollback

- **Risk**: Large touch surface; do changes in phases and run tests after each.
- **Rollback**: Revert to current main until full integration is verified.

---

## 6. Summary

- **Single Core type**: v3 Core with `release`, `platform_id`, `platform`, and convenience properties set when we load the list. Use **`id`** everywhere (no `identifier`).
- **No separate model**: Remove the old Core/Repository/Platform/Sponsor types and any conversion step that produced them.
- **Loading the list**: When building the cores list we set each core’s `release`, `platform_id`, and `platform` from the API and catalog; no separate “mapper” or “resolved” types.
- **Sponsor**: Use `repository.funding` (Funding) and Funding.ToString for display.
- **Naming**: v3-native only — `id`, `release`, `platform_id`, `platform`; no “resolved”, “selected”, or “identifier”.
