"""Install the Supper IDA plugin into the local IDA user plugin directory."""

from __future__ import annotations

import argparse
import os
import platform
import shutil
from pathlib import Path


LOADER_NAME = "supper_ida_mcp_plugin.py"
PACKAGE_NAME = "supper_ida_plugin"
PLUGIN_ID = "supper-ida-mcp-plugin"
PLUGIN_VERSION = "0.1.2"
LEGACY_PLUGIN_NAMES = ("ida_mcp.py", "ida_mcp", "mcp-plugin.py")
LEGACY_BACKUP_DIR = ".supper_ida_mcp_legacy_backup"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plugins-dir", help="Explicit IDA user plugins directory.")
    parser.add_argument("--dry-run", action="store_true", help="Print target paths without writing files.")
    parser.add_argument("--keep-legacy", action="store_true", help="Do not archive legacy ida-mcp plugin files.")
    args = parser.parse_args(argv)

    plugin_root = Path(__file__).resolve().parent
    source_root = plugin_root / "src"
    package_source = source_root / PACKAGE_NAME
    plugins_dir = Path(args.plugins_dir).expanduser() if args.plugins_dir else default_plugins_dir()
    loader_path = plugins_dir / LOADER_NAME
    package_path = plugins_dir / PACKAGE_NAME
    loader = build_loader()

    print(f"IDA plugins directory: {plugins_dir}")
    print(f"Loader path: {loader_path}")
    print(f"Package path: {package_path}")

    if args.dry_run:
        if not args.keep_legacy:
            for legacy_path in find_legacy_plugins(plugins_dir):
                print(f"Legacy plugin would be archived: {legacy_path}")
        return 0

    if not package_source.is_dir():
        raise SystemExit(f"Plugin package source not found: {package_source}")

    plugins_dir.mkdir(parents=True, exist_ok=True)
    if not args.keep_legacy:
        for archived in archive_legacy_plugins(plugins_dir):
            print(f"Archived legacy plugin: {archived}")

    if package_path.exists():
        shutil.rmtree(package_path)
    shutil.copytree(package_source, package_path, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
    loader_path.write_text(loader, encoding="utf-8")
    print("Installed Supper IDA MCP plugin. Restart IDA Pro to load it.")
    return 0


def default_plugins_dir() -> Path:
    ida_user = os.environ.get("IDAUSR")
    if ida_user:
        return Path(ida_user).expanduser() / "plugins"

    system = platform.system().lower()
    home = Path.home()
    if system == "darwin":
        return home / ".idapro" / "plugins"
    if system == "windows":
        appdata = os.environ.get("APPDATA")
        if appdata:
            return Path(appdata) / "Hex-Rays" / "IDA Pro" / "plugins"
        return home / "AppData" / "Roaming" / "Hex-Rays" / "IDA Pro" / "plugins"
    return home / ".idapro" / "plugins"


def find_legacy_plugins(plugins_dir: Path) -> list[Path]:
    return [
        plugins_dir / name
        for name in LEGACY_PLUGIN_NAMES
        if (plugins_dir / name).exists() or (plugins_dir / name).is_symlink()
    ]


def archive_legacy_plugins(plugins_dir: Path) -> list[Path]:
    legacy_paths = find_legacy_plugins(plugins_dir)
    if not legacy_paths:
        return []

    backup_root = plugins_dir / LEGACY_BACKUP_DIR
    backup_dir = backup_root / timestamp()
    backup_dir.mkdir(parents=True, exist_ok=True)
    archived: list[Path] = []
    for legacy_path in legacy_paths:
        destination = unique_destination(backup_dir / legacy_path.name)
        shutil.move(str(legacy_path), str(destination))
        archived.append(destination)
    return archived


def unique_destination(path: Path) -> Path:
    if not path.exists():
        return path

    stem = path.stem
    suffix = path.suffix
    parent = path.parent
    index = 1
    while True:
        candidate = parent / f"{stem}-{index}{suffix}"
        if not candidate.exists():
            return candidate
        index += 1


def timestamp() -> str:
    from datetime import datetime

    return datetime.now().strftime("%Y%m%d-%H%M%S")


def build_loader() -> str:
    return f'''"""IDA loader for Supper IDA MCP Tools."""

from __future__ import annotations

import os
import sys

SUPPER_IDA_MCP_PLUGIN_ID = {PLUGIN_ID!r}
SUPPER_IDA_MCP_PLUGIN_VERSION = {PLUGIN_VERSION!r}
PLUGIN_DIR = os.path.dirname(os.path.abspath(__file__))

if PLUGIN_DIR not in sys.path:
    sys.path.insert(0, PLUGIN_DIR)

for module_name in list(sys.modules):
    if module_name == "supper_ida_plugin" or module_name.startswith("supper_ida_plugin."):
        del sys.modules[module_name]

from supper_ida_plugin.entry import PLUGIN_ENTRY  # noqa: E402,F401
'''


if __name__ == "__main__":
    raise SystemExit(main())
