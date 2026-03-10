# Plan: No Platform Data Loss When Switching to v3 API

This document ensures platform information is preserved when migrating from v2 to v3. It covers what the app uses, where loss can happen, and concrete steps to prevent it.

---

## 1. What platform data exists and where it’s used

### 1.1 V2 Platform shape (current)

- **`category`** – e.g. "Handheld", "Console"
- **`name`** – e.g. "Arduboy", "Game Boy"
- **`manufacturer`** – e.g. "Nintendo"
- **`year`** – e.g. 1989

### 1.2 Where the app uses inventory `core.platform`

| Location | Field(s) used | Purpose |
|----------|----------------|--------|
| **V2 Core.ToString()** | `platform.name` | Display: `"{platform.name} ({identifier})"` |
| **CoreUpdaterService** | `core.platform.name` | Summary after install/update |
| **CoreUpdaterService (Jotego)** | `platform.name == core.platform_id` | Rename check |

**Note:** `Program.PrintOpenFpgaFolders.cs` uses **ReadPlatformJson(core.identifier)** (installed core’s platform from device), not the inventory `core.platform`. So that flow is unchanged by v2→v3.

### 1.3 What v2 gives today

- Every core has **`platform_id`** and a full **`platform`** object (category, name, manufacturer, year) inline. No lookup.

### 1.4 What v3 gives

- **cores.json:** Each core has **`releases[]`**; each release has **`core.metadata.platform_ids[]`** (e.g. first element = primary platform id). No inline platform object.
- **platforms.json:** Separate list of platforms with **`id`**, `category`, `name`, `manufacturer`, `year`. Must be fetched and joined by `id` to get the same richness as v2.

---

## 2. Where data loss can happen

| Risk | Cause | Result |
|------|--------|--------|
| **No platforms fetch** | Skipping `platforms.json` | Every core gets fallback: `name = platform_id`, `category`/`manufacturer`/`year` null or zero. Loss of category, manufacturer, year. |
| **Missing id in platforms.json** | A core’s `platform_id` not present in platforms.json | Same fallback for that core; loss of full platform for that id. |
| **Lookup key mismatch** | Casing or format difference between `platform_ids` and platform `id` | Lookup fails → fallback → data loss. |
| **Null category in fallback** | Code that uses `platform.category` as key or in UI | Possible NRE or broken grouping if any code paths use it. |

---

## 3. Prevention plan

### 3.1 Always fetch platforms.json when using v3

- **Do not** treat platforms as optional when the cores source is v3.
- In **CoresService.Cores** getter (v3 path): always request `platforms.json`, deserialize to **PlatformsResponseWrapper**, and build **Dictionary<string, V3.Platform>** keyed by **`id`**.
- Pass this dictionary into **CoreMapper.MapToCore** so every core gets a full platform when the id exists in the catalog.

**Rationale:** v2 had no separate platforms list; every core had full platform. To avoid regressing, we must use the v3 catalog whenever we use v3 cores.

### 3.2 Fallback when id is missing from platforms.json

- Keep the existing **ResolvePlatform** fallback for unknown `platform_id`:
  - **`name`** = `platform_id` (so ToString and summaries still show something).
  - **`category`** = use a sentinel (e.g. **`"Other"`** or **`""`**) instead of null so any code that groups or displays by category is safe.
  - **`manufacturer`** = null.
  - **`year`** = 0.

- **Concrete change:** In **CoreMapper.ResolvePlatform**, when creating the fallback **Platform**, set **`category = "Other"`** (or `""`) instead of `null` so there is no null key if something does `platform.category` grouping.

### 3.3 Consistent lookup key (no casing/format loss)

- Build the platforms dictionary using the **exact** `id` as returned by the v3 API (typically lowercase, e.g. `"arduboy"`).
- Use **`platform_ids[0]`** from the release metadata as-is for the lookup (no arbitrary lowercasing/uppercasing) so we match how platforms.json is keyed.
- If the v3 API ever uses different casing for the same platform in cores vs platforms, add a single normalization (e.g. compare by `StringComparison.OrdinalIgnoreCase` or normalize keys when building the dictionary) and document it. Until then, use exact match.

### 3.4 Validation and observability (optional but recommended)

- **Log or track** when the fallback is used: e.g. when `platform_id` is not in `platformsById`, log a debug message or increment a counter so we can see how many cores lack a catalog platform.
- **Unit or integration test:** For a known v3 cores + platforms snapshot, assert that:
  - Every core’s `platform_id` exists in the platforms dictionary, or
  - Cores that use fallback have a non-null `platform.name` and a non-null `platform.category` (sentinel).

This helps catch regressions (e.g. platforms.json not fetched or key format change).

### 3.5 No dropping cores because of platform

- **CoreMapper.MapToCore** already returns null only when core has no releases or missing required release metadata or missing `platform_ids`; it does **not** return null when the platform id is missing from the platforms dictionary (it uses fallback).
- Keep this behavior: **never drop a core** because its platform_id is missing from platforms.json; always produce a Core with at least the fallback platform.

---

## 4. Checklist before shipping v3 as default

- [ ] **CoresService** (v3 path) always fetches **platforms.json** and builds **platformsById**.
- [ ] **CoreMapper.ResolvePlatform** fallback sets **`category = "Other"`** (or agreed sentinel), not null.
- [ ] Lookup uses exact **platform_id** from release metadata; no unintended case/format changes.
- [ ] Optional: add debug log or metric when fallback is used.
- [ ] Optional: add test that, for a fixed v3 payload, mapped cores have expected platform names/categories and no unexpected nulls.

---

## 5. Summary

- **v2:** Platform was inline on every core → no lookup, no loss.
- **v3:** Platform comes from **platforms.json** keyed by **id** → must fetch and join; use fallback when id is missing; make fallback safe (non-null category).
- **Plan:** Always fetch platforms when using v3; keep and harden fallback (non-null category); use consistent keys; optionally log fallback use and add a test. That keeps platform data loss to a minimum and avoids NREs or broken grouping.
