"""Dispatch center-owned tool names to plugin-side executors."""

from __future__ import annotations

from typing import Any

from supper_ida_plugin.executor.ida_thread import run_on_ida_thread
from supper_ida_plugin.ida_runtime.metadata import collect_metadata


def execute_tool(tool_name: str, arguments: dict[str, Any], instance_id: str) -> Any:
    if tool_name == "target.ping":
        return {
            "status": "ok",
            "instanceId": instance_id,
        }

    if tool_name == "target.get_metadata":
        return run_on_ida_thread(lambda: collect_metadata(instance_id))

    raise ValueError(f"Unknown plugin tool: {tool_name}")
