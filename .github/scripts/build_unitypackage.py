#!/usr/bin/env python3
"""
Builds a .unitypackage from a folder inside Assets/, without opening Unity.

A .unitypackage is a gzipped tar with one directory per asset, named after the GUID in
the asset's .meta file:
    <guid>/pathname     path of the asset inside the project (e.g. Assets/Tool/A.cs)
    <guid>/asset.meta   contents of the .meta file
    <guid>/asset        contents of the file (folders have no 'asset' entry)

Usage:
    build_unitypackage.py <folder under Assets> <output.unitypackage> [--expect-version X.Y.Z]
"""
import argparse
import io
import json
import os
import re
import sys
import tarfile

GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)


def read_guid(meta_path):
    with open(meta_path, encoding="utf-8") as f:
        match = GUID_RE.search(f.read())
    if not match:
        sys.exit(f"error: no guid found in {meta_path}")
    return match.group(1).lower()


def collect_assets(root):
    """The folder itself plus every file and subfolder in it (except .meta files), sorted."""
    paths = [root]
    for current, dirs, files in os.walk(root):
        dirs.sort()
        for name in dirs:
            paths.append(os.path.join(current, name))
        for name in sorted(files):
            if not name.endswith(".meta"):
                paths.append(os.path.join(current, name))
    return paths


def add_entry(tar, name, data):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mode = 0o644
    info.mtime = 0  # Reproducible output: same input, same package.
    tar.addfile(info, io.BytesIO(data))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("folder", help="Folder to package, e.g. Assets/UnityGitTool")
    parser.add_argument("output", help="Output .unitypackage file")
    parser.add_argument("--expect-version", help="Fail unless package.json in the folder has this version")
    args = parser.parse_args()

    root = os.path.normpath(args.folder).replace(os.sep, "/")
    if not root.startswith("Assets/") or not os.path.isdir(root):
        sys.exit(f"error: '{root}' must be an existing folder under Assets/ (run from the project root)")

    if args.expect_version:
        manifest = os.path.join(root, "package.json")
        version = json.load(open(manifest, encoding="utf-8")).get("version") if os.path.exists(manifest) else None
        if version != args.expect_version:
            sys.exit(f"error: package.json version is '{version}' but the release is '{args.expect_version}'. "
                     "Update package.json before publishing the release.")

    assets = collect_assets(root)
    missing = [p for p in assets if not os.path.exists(p + ".meta")]
    if missing:
        sys.exit("error: these assets have no .meta file (open the project in Unity once):\n  " + "\n  ".join(missing))

    seen = {}
    with tarfile.open(args.output, "w:gz") as tar:
        for path in assets:
            unity_path = path.replace(os.sep, "/")
            guid = read_guid(path + ".meta")
            if guid in seen:
                sys.exit(f"error: duplicated guid {guid} in {unity_path} and {seen[guid]}")
            seen[guid] = unity_path

            add_entry(tar, f"{guid}/pathname", unity_path.encode("utf-8"))
            with open(path + ".meta", "rb") as f:
                add_entry(tar, f"{guid}/asset.meta", f.read())
            if os.path.isfile(path):
                with open(path, "rb") as f:
                    add_entry(tar, f"{guid}/asset", f.read())

    print(f"{args.output}: {len(assets)} assets from {root}")


if __name__ == "__main__":
    main()
