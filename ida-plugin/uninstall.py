"""Uninstall the Supper IDA plugin from the local IDA user plugin directory."""

from __future__ import annotations

import argparse
from pathlib import Path

from install import LOADER_NAME, PACKAGE_NAME, PLUGIN_ID, default_plugins_dir


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plugins-dir", help="Explicit IDA user plugins directory.")
    parser.add_argument("--dry-run", action="store_true", help="Print target paths without deleting files.")
    args = parser.parse_args(argv)

    plugins_dir = Path(args.plugins_dir).expanduser() if args.plugins_dir else default_plugins_dir()
    loader_path = plugins_dir / LOADER_NAME
    package_path = plugins_dir / PACKAGE_NAME
    print(f"Loader path: {loader_path}")
    print(f"Package path: {package_path}")

    if args.dry_run:
        return 0

    if loader_path.exists():
        content = loader_path.read_text(encoding="utf-8")
        if PLUGIN_ID not in content and "supper_ida_plugin" not in content:
            raise SystemExit(f"Refusing to remove a loader that is not ours: {loader_path}")

        loader_path.unlink()
        print("Uninstalled Supper IDA MCP plugin loader.")
    else:
        print("Plugin loader is not installed.")

    if package_path.exists():
        import shutil

        shutil.rmtree(package_path)
        print("Uninstalled Supper IDA MCP plugin package.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
