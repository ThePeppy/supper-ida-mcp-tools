"""Uninstall the Supper IDA plugin from the local IDA user plugin directory."""

from __future__ import annotations

import argparse
from pathlib import Path

from install import LOADER_NAME, default_plugins_dir


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plugins-dir", help="Explicit IDA user plugins directory.")
    parser.add_argument("--dry-run", action="store_true", help="Print target paths without deleting files.")
    args = parser.parse_args(argv)

    plugins_dir = Path(args.plugins_dir).expanduser() if args.plugins_dir else default_plugins_dir()
    loader_path = plugins_dir / LOADER_NAME
    print(f"Loader path: {loader_path}")

    if args.dry_run:
        return 0

    if loader_path.exists():
        loader_path.unlink()
        print("Uninstalled Supper IDA MCP plugin loader.")
    else:
        print("Plugin loader is not installed.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
