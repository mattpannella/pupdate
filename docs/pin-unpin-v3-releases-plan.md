# Pin/Unpin Core Version — Using v3 Releases

## Current behavior

- **Settings:** `CoreSettings.pinned_version` is a nullable string (e.g. `"1.2.3"` or `"v1.2.3"`). When set, the core is locked to that version.
- **Menu:** Pocket Maintenance → Pin/Unpin Core Version. User picks an installed core, then types a version (or leaves blank to unpin). No list of available versions.
- **Update path:** When updating, `CoreUpdaterService` sets `mostRecentRelease = coreSettings.pinned_version` and calls `CoresService.GetDownloadUrlForVersion(core, pinned_version)`, which uses the **GitHub API** to resolve the tag to a release and returns the .zip asset URL. That URL is assigned to `core.download_url` for the rest of the flow.

## V3 data we have

- Each **Core** from the inventory has:
  - `releases` — `List<Release>` from the openFPGA Library v3 API.
  - After loading, we also set `release` (singular), `download_url`, `version`, `release_date` from the **latest** release via `TrySetReleaseAndPlatform` / `GetLatestRelease`.
- Each **Release** has:
  - `download_url` — direct download URL (no GitHub API needed).
  - `core.metadata.version` — version string (e.g. `"1.2.3"`).
  - `core.metadata.date_release` — release date (for display/sorting).

So we can drive pin/unpin entirely from `core.releases` when available: show a list of known versions and resolve the download URL from the inventory instead of calling GitHub.

---

## Goals

1. **Pin menu:** Let the user choose a version from the core’s v3 `releases` list (with optional manual entry for tags not in the inventory).
2. **Update path:** Resolve `pinned_version` only from `core.releases` (use `release.download_url`). If not found, display a message and move on to the next core (no GitHub call).

---

## Plan

### 1. Resolve pinned version from v3 in the update path

**Where:** `CoresService.GetDownloadUrlForVersion` (and/or a small helper used from `CoreUpdaterService`).

**Behavior:**

- If `core.releases` is non-empty, look for a release whose version matches `pinned_version`:
  - Compare with `release.core.metadata.version`, normalizing for a leading `"v"` (e.g. `"1.2.3"` matches `"v1.2.3"` and vice versa).
- If a match is found, return that release's `download_url` (no GitHub call).
- If no match (or `releases` null/empty): do **not** call GitHub. Display a message that the pinned version was not found in the releases list and move on to the next core (skip this core for the update).

**Result:** Pinned updates use only v3 data. If the pinned version is not in the core's releases list, the user sees a message and the updater continues with the next core.

### 2. Pin menu: list versions from v3 releases

**Where:** `Program.Menus.cs` — `PinCoreVersionMenu()` and the per-core action.

**Current flow:** User selects a core → prompt “Enter version to pin to, or leave blank to remove pin” → free text.

**New flow: two-step menu**

1. User selects a core (unchanged; only cores with `repository != null` are shown).
2. For that core, if `core.releases` is non-empty, show **step 1 — action menu** with three choices:
   - **Unpin** — clear `pinned_version`, save, done.
   - **Select from releases list** — open **step 2** (the release list menu; see below).
   - **Enter version manually** — current free-text prompt (for tags not in the inventory).
   - **Go Back** — return to the core list.
3. **Step 2 — release list menu** (shown only when "Select from releases list" is chosen): a separate menu similar to the core selector, with a paginated list of releases from the v3 API. Use the same pattern as `PinCoreVersionMenu()` / `Program.Menus.Cores.cs` (see **Pagination** below). Menu order: Next Page (if more pages), one item per release for the current page (e.g. `"1.2.3 (2024-01-15)"` from `metadata.version` and `metadata.date_release`), Next Page again, Prev Page (if `offset > 0`), Go Back (returns to step 1). Selecting a release sets `pinned_version` to that `metadata.version`, save, then exit or return to core list.
4. If `core.releases` is null or empty (e.g. local-only core or failed load):
   - Fall back to current behavior: "Enter version to pin to, or leave blank to remove pin" + optional "Unpin" at the top.

**Pagination (step 2 — release list menu, follow the core selector pattern):**

Use the same pattern as the existing core selector in `PinCoreVersionMenu()` and `Program.Menus.Cores.cs` (e.g. `RunCoreSelector`). This applies only to the **second** menu (the release list), not the first action menu.

- **Constants:** `const int pageSize = 12` (releases per page). Some cores have 66+ releases (e.g. Jotego), so pagination is required.
- **State:** `var offset = 0` (current page start index), `bool more = true` (loop control).
- **Loop:** Wrap the release-list menu in `while (more) { ... menu.Show(); }`. When the user chooses **Next Page** or **Prev Page**, run `offset += pageSize` or `offset -= pageSize`, call `thisMenu.CloseMenu()`, and let the loop re-run so the menu is rebuilt with the new page. **Go Back** sets `more = false` and closes the menu, returning to step 1.
- **Next Page:** Add at top and again after the release items when `offset + pageSize < releases.Count`. Action: `offset += pageSize; thisMenu.CloseMenu();`.
- **Prev Page:** Add when `offset > 0`. Action: `offset -= pageSize; thisMenu.CloseMenu();`.
- **Item window:** Only add menu entries for releases in the range `[offset, offset + pageSize)` (e.g. skip with `if (current < offset || current > offset + pageSize - 1) continue` in the loop over releases).
- **Fixed items:** Only release items and Next/Prev Page/Go Back on this menu. No Unpin or Enter manually here; those live on step 1.

**Display details:**

- Sort releases by `date_release` descending so “newest first” is obvious (or keep API order if it’s already newest-first).
- Label the “current” release if desired: e.g. `core.release != null && release.core.metadata.version == core.release.core.metadata.version` → show “(latest)” next to that row.
- Stored value: `CoreSettings.pinned_version` remains a string; store the same version string that appears in `release.core.metadata.version` so that `GetDownloadUrlForVersion` can match it in step 1.

### 3. Version normalization

**Where:** Shared helper in `Util` (e.g. `NormalizeVersionTag(string version)` and `VersionsMatch(string a, string b)`), used by both resolution and menu.

- **Match logic:** Treat `"1.2.3"` and `"v1.2.3"` as the same when comparing `pinned_version` to `release.core.metadata.version` (strip or add `"v"` for comparison).
- Use this in:
  - `CoresService.GetDownloadUrlForVersion` when searching `core.releases` (call Util).
  - Optional: when showing "current pin" in the menu, highlight the matching release in the list using the same Util helper.

### 4. Edge cases

- **Cores without `releases`:** If the core is pinned, display message that the version wasn't found (no releases list), skip to next core. No GitHub call.
- **Pinned version not in v3 list:** Display message that the pinned version wasn't found in the releases list, skip to next core. No GitHub fallback.
- **Empty `release.core.metadata`:** Skip that release when building the menu list and when resolving; we already skip such releases in `TrySetReleaseAndPlatform`.
- **Many releases:** Use the pagination pattern above (same as core selector); `pageSize = 12` keeps the menu usable for cores with up to 66 releases.

### 5. No change to settings schema

- `CoreSettings.pinned_version` stays a single nullable string. No new fields required. Pinned versions are resolved only from v3; if not in the releases list, a message is shown and the core is skipped.

---

## Implementation order

1. **Version normalization helper in Util** — e.g. `NormalizeVersionTag(string version)` and `VersionsMatch(string a, string b)`.
2. **GetDownloadUrlForVersion** — resolve only from `core.releases` using the Util version helper; if no match, return null so the caller can display a message and skip to the next core.
3. **Pin menu** — two steps: (1) action menu: Unpin, Select from releases list, Enter version manually, Go Back; (2) when "Select from releases list" is chosen, show release list menu with pagination (core-selector pattern). Call existing `PinCoreVersion` / `UnpinCoreVersion` with the chosen version string.

---

## Files to touch

| Area              | File(s) |
|-------------------|--------|
| Resolution        | `src/services/CoresService.Helpers.cs` (and/or `CoresService.cs` if helper lives there) |
| Update path       | `CoreUpdaterService`: when pinned and `GetDownloadUrlForVersion` returns null, display message and continue to next core (no GitHub). |
| Menu              | `src/partials/Program.Menus.cs` — `PinCoreVersionMenu()` and the per-core callback |
| Version compare   | New helper in `Util` (e.g. `src/helpers/Util.cs`); used by `CoresService.GetDownloadUrlForVersion` and optionally by the pin menu |

This keeps the existing pin/unpin contract (one version string per core, same settings file) and uses the v3 releases array for both **choosing** the version (menu) and **resolving** the download URL (update path). If the pinned version is not in the releases list, a message is displayed and the updater moves on to the next core.
