"""Install the Supper IDA plugin into the local IDA user plugin directory."""

from __future__ import annotations

import argparse
import os
import platform
from pathlib import Path


LOADER_NAME = "supper_ida_mcp_plugin.py"
PLUGIN_ID = "supper-ida-mcp-plugin"
PLUGIN_VERSION = "0.1.0"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plugins-dir", help="Explicit IDA user plugins directory.")
    parser.add_argument("--dry-run", action="store_true", help="Print target paths without writing files.")
    args = parser.parse_args(argv)

    plugin_root = Path(__file__).resolve().parent
    source_root = plugin_root / "src"
    plugins_dir = Path(args.plugins_dir).expanduser() if args.plugins_dir else default_plugins_dir()
    loader_path = plugins_dir / LOADER_NAME
    loader = build_loader(source_root)

    print(f"IDA plugins directory: {plugins_dir}")
    print(f"Loader path: {loader_path}")
    print(f"Source root: {source_root}")

    if args.dry_run:
        return 0

    plugins_dir.mkdir(parents=True, exist_ok=True)
    loader_path.write_text(loader, encoding="utf-8")
    print("Installed Supper IDA MCP plugin loader.")
    return 0


def default_plugins_dir() -> Path:
    ida_user = os.environ.get("IDAUSR")
    if ida_user:
        return Path(ida_user).expanduser() / "plugins"

    system = platform.system().lower()
    home = Path.home()
    if system == "darwin":
        return home / "Library" / "Application Support" / "Hex-Rays" / "IDA Pro" / "plugins"
    if system == "windows":
        appdata = os.environ.get("APPDATA")
        if appdata:
            return Path(appdata) / "Hex-Rays" / "IDA Pro" / "plugins"
        return home / "AppData" / "Roaming" / "Hex-Rays" / "IDA Pro" / "plugins"
    return home / ".idapro" / "plugins"


def build_loader(source_root: Path) -> str:
    source = str(source_root)
    return f'''"""IDA loader for Supper IDA MCP Tools."""

from __future__ import annotations

import sys

SOURCE_ROOT = {source!r}
SUPPER_IDA_MCP_PLUGIN_ID = {PLUGIN_ID!r}
SUPPER_IDA_MCP_PLUGIN_VERSION = {PLUGIN_VERSION!r}

if SOURCE_ROOT not in sys.path:
    sys.path.insert(0, SOURCE_ROOT)

from supper_ida_plugin.entry import PLUGIN_ENTRY  # noqa: E402,F401
'''


if __name__ == "__main__":
    raise SystemExit(main())
