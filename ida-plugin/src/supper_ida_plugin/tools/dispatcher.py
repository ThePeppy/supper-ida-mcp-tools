"""Dispatch center-owned tool names to plugin-side executors."""

from __future__ import annotations

from typing import Any

from supper_ida_plugin.executor.ida_thread import run_on_ida_thread
from supper_ida_plugin.ida_runtime.metadata import collect_metadata
from supper_ida_plugin.tools import analysis


_IDA_TOOLS = {
    "analysis.list_functions": analysis.list_functions,
    "analysis.get_function": analysis.get_function,
    "analysis.decompile": analysis.decompile,
    "analysis.disassemble": analysis.disassemble,
    "analysis.xrefs": analysis.xrefs,
    "analysis.list_strings": analysis.list_strings,
    "analysis.list_imports": analysis.list_imports,
    "analysis.get_bytes": analysis.get_bytes,
    "analysis.rename": analysis.rename,
    "analysis.set_comment": analysis.set_comment,
    "analysis.search_text": analysis.search_text,
}


def execute_tool(tool_name: str, arguments: dict[str, Any], instance_id: str) -> Any:
    if tool_name == "target.ping":
        return {
            "status": "ok",
            "instanceId": instance_id,
        }

    if tool_name == "target.get_metadata":
        return run_on_ida_thread(lambda: collect_metadata(instance_id))

    if tool_name in _IDA_TOOLS:
        return run_on_ida_thread(lambda: _IDA_TOOLS[tool_name](arguments))

    raise ValueError(f"unknown plugin tool: {tool_name}")
