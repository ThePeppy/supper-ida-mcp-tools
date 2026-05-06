"""IDA database metadata collection.

This module keeps IDA imports lazy so protocol tests can import the package
outside IDA.
"""

from __future__ import annotations

import os
import platform
import uuid
from typing import Any


def collect_metadata(instance_id: str | None = None) -> dict[str, Any]:
    instance_id = instance_id or str(uuid.uuid4())
    fallback_name = f"ida-{os.getpid()}"
    metadata: dict[str, Any] = {
        "instanceId": instance_id,
        "alias": fallback_name,
        "processId": os.getpid(),
        "binaryName": fallback_name,
        "inputPath": None,
        "databasePath": None,
        "idaVersion": None,
        "platform": platform.system().lower(),
    }

    try:
        import ida_kernwin  # type: ignore
        import ida_nalt  # type: ignore
        import idc  # type: ignore

        binary_name = ida_nalt.get_root_filename() or fallback_name
        metadata.update(
            {
                "alias": _alias_from_name(binary_name),
                "binaryName": binary_name,
                "inputPath": ida_nalt.get_input_file_path() or None,
                "databasePath": idc.get_idb_path() or None,
                "idaVersion": ida_kernwin.get_kernel_version(),
            }
        )
    except Exception:
        # Non-IDA execution or partially initialized IDA. The fallback metadata
        # keeps the connection testable and visible to the center.
        pass

    return metadata


def _alias_from_name(name: str) -> str:
    base = os.path.splitext(os.path.basename(name))[0] or name
    return "".join(ch.lower() if ch.isalnum() else "_" for ch in base).strip("_") or "ida"
