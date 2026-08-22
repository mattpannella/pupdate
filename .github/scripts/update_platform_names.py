#!/usr/bin/env python3

import json
import os
import sys
import urllib.request

REPO = "dyreschlock/pocket-platform-images"
DIRECTORY = "arcade/Platforms"
MAPPING_FILE = "platform_names.json"
TOKEN = os.environ.get("GITHUB_TOKEN", "")


def http_get_json(url, authed, retries=3):
    headers = {"User-Agent": "pupdate-platform-name-bot", "Accept-Encoding": "identity"}
    if authed:
        headers["Accept"] = "application/vnd.github+json"
        if TOKEN:
            headers["Authorization"] = f"Bearer {TOKEN}"

    last_error = None

    for _ in range(retries):
        try:
            req = urllib.request.Request(url, headers=headers)
            with urllib.request.urlopen(req, timeout=60) as resp:
                return json.loads(resp.read().decode("utf-8"))
        except Exception as ex:  # noqa: BLE001
            last_error = ex

    raise last_error


def main():
    listing = http_get_json(
        f"https://api.github.com/repos/{REPO}/contents/{DIRECTORY}?per_page=1000", authed=True)

    jt_files = [f for f in listing
                if f.get("type") == "file"
                and f["name"].startswith("jt")
                and f["name"].endswith(".json")]

    with open(MAPPING_FILE, encoding="utf-8") as fh:
        mapping = json.load(fh)

    added = {}

    for f in jt_files:
        platform_id = f["name"][:-len(".json")]

        if platform_id in mapping:
            continue

        try:
            data = http_get_json(f["download_url"], authed=False)
        except Exception as ex:  # noqa: BLE001
            print(f"WARN: could not fetch {f['name']}: {ex}", file=sys.stderr)
            continue

        name = (data.get("platform") or {}).get("name")

        if name:
            mapping[platform_id] = name
            added[platform_id] = name

    if not added:
        print("No new Jotego platforms.")
        return

    ordered = dict(sorted(mapping.items()))

    with open(MAPPING_FILE, "w", encoding="utf-8") as fh:
        json.dump(ordered, fh, indent=2, ensure_ascii=False)
        fh.write("\n")

    print(f"Added {len(added)} new platform name(s):")
    for key, value in added.items():
        print(f"  {key} -> {value}")


if __name__ == "__main__":
    main()
