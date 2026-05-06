"""Central registry MCP server for multiple IDA Pro MCP plugin instances.

The registry is intentionally pure Python: it never imports IDA modules and
never calls the IDA SDK. GUI IDA instances register their own local MCP HTTP
endpoint here, then this server routes MCP tool calls to the requested target.
"""

from __future__ import annotations

import argparse
import copy
import datetime as _dt
import hashlib
import http.client
import json
import os
import re
import sys
import threading
import time
import traceback
from collections import OrderedDict
from typing import Annotated, Any, NotRequired, TypedDict
from urllib.parse import parse_qs, urlparse

# Do not import ida_pro_mcp.ida_mcp as a package here. Its __init__ imports IDA
# modules. Load the pure-Python vendored zeromcp/discovery modules directly.
_IDA_MCP_DIR = os.path.join(os.path.dirname(__file__), "ida_mcp")
sys.path.insert(0, _IDA_MCP_DIR)
try:
    from discovery import probe_instance  # type: ignore
    from zeromcp import (  # type: ignore
        EXTERNAL_BASE_HEADER,
        McpHttpRequestHandler,
        McpServer,
        get_current_request_external_base_url,
    )
finally:
    sys.path.pop(0)


REGISTRY_HOST = "127.0.0.1"
REGISTRY_PORT = 9399
REGISTER_PATH = "/registry/register"
UNREGISTER_PATH = "/registry/unregister"
TARGETS_PATH = "/registry/targets"
PROXY_HEADER = "X-MCP-Proxied"
TARGET_ARGUMENT = "target"
TARGET_TTL_SEC = 60.0
SESSION_TARGET_TTL_SEC = 24 * 60 * 60
SESSION_TARGET_MAX_SIZE = 4096
OUTPUT_PROXY_CACHE_MAX_SIZE = 256
REMOTE_LOCAL_TOOL_NAMES = {"list_instances", "select_instance"}
_OUTPUT_PATH_RE = re.compile(r"^/output/([a-f0-9-]+)\.(\w+)$")


class RegistryTarget(TypedDict, total=False):
    target_id: str
    alias: str
    host: str
    connect_host: NotRequired[str]
    port: int
    pid: int
    backend: str
    binary: str
    idb_path: str
    input_path: str
    registered_at: str
    last_seen: str
    reachable: bool
    active: bool
    metadata: dict[str, Any]
    # Internal monotonic timestamp; stripped from public responses.
    last_seen_monotonic: float


class RegistryTargetList(TypedDict):
    targets: list[RegistryTarget]
    count: int


class RegistryHealth(TypedDict):
    status: str
    registry: str
    host: str
    port: int
    targets: int


class TargetSelectionResult(TypedDict, total=False):
    success: bool
    target: RegistryTarget | None
    message: str
    error: str


class TargetToolsResult(TypedDict, total=False):
    target: RegistryTarget
    tools: list[dict[str, Any]]
    count: int
    error: str


class TargetResourcesResult(TypedDict, total=False):
    target: RegistryTarget
    resources: list[dict[str, Any]]
    count: int
    error: str


mcp = McpServer("ida-mcp-registry")
_dispatch_original = mcp.registry.dispatch

_targets: OrderedDict[str, RegistryTarget] = OrderedDict()
_targets_lock = threading.RLock()
_session_target_ids: OrderedDict[str, str] = OrderedDict()
_session_target_last_seen: dict[str, float] = {}
_session_targets_lock = threading.Lock()
_default_target_id: str | None = None
_output_proxy_targets: OrderedDict[str, tuple[str, int]] = OrderedDict()
_output_proxy_lock = threading.Lock()


def _utcnow() -> str:
    return _dt.datetime.now(_dt.timezone.utc).isoformat()


def _target_key(host: str, port: int) -> str:
    return f"{host}:{int(port)}"


def _is_loopback_host(host: str) -> bool:
    return host in {"127.0.0.1", "::1", "localhost"}


def _candidate_connect_hosts(target: RegistryTarget) -> list[str]:
    host = target.get("host", "127.0.0.1")
    candidates = [host]
    connect_host = target.get("connect_host")
    if connect_host:
        candidates.append(connect_host)
    if _is_loopback_host(host):
        candidates.append(os.environ.get("IDA_MCP_HOST_GATEWAY", "host.docker.internal"))

    result = []
    for candidate in candidates:
        if candidate and candidate not in result:
            result.append(candidate)
    return result


def _safe_str(value: Any) -> str:
    return value if isinstance(value, str) else ""


def _normalize_registration(payload: dict[str, Any]) -> RegistryTarget:
    host = _safe_str(payload.get("host")) or "127.0.0.1"
    connect_host = _safe_str(payload.get("connect_host"))
    port = int(payload.get("port", 0))
    if port < 1 or port > 65535:
        raise ValueError(f"Invalid target port: {port}")

    metadata = payload.get("metadata")
    if not isinstance(metadata, dict):
        metadata = {}

    pid_value = payload.get("pid", 0)
    try:
        pid = int(pid_value)
    except (TypeError, ValueError):
        pid = 0

    return {
        "host": host,
        "connect_host": connect_host,
        "port": port,
        "pid": pid,
        "backend": _safe_str(payload.get("backend")) or "ida",
        "binary": _safe_str(payload.get("binary")),
        "idb_path": _safe_str(payload.get("idb_path")),
        "input_path": _safe_str(payload.get("input_path"))
        or _safe_str(metadata.get("input_path")),
        "metadata": metadata,
    }


def _make_target_id(info: RegistryTarget) -> str:
    seed = "|".join(
        [
            info.get("host", ""),
            str(info.get("port", "")),
            str(info.get("pid", "")),
            info.get("idb_path", ""),
            info.get("input_path", ""),
            info.get("binary", ""),
        ]
    )
    return "ida-" + hashlib.sha1(seed.encode("utf-8")).hexdigest()[:12]


def _alias_base(info: RegistryTarget) -> str:
    name = (
        os.path.basename(info.get("binary", ""))
        or os.path.basename(info.get("input_path", ""))
        or os.path.basename(info.get("idb_path", ""))
        or f"ida_{info.get('port', 'target')}"
    )
    name = os.path.splitext(name)[0] or name
    alias = re.sub(r"[^a-zA-Z0-9_]+", "_", name).strip("_").lower()
    return alias or f"ida_{info.get('port', 'target')}"


def _make_unique_alias(base: str, *, excluding_key: str | None = None) -> str:
    used = {
        target.get("alias", "")
        for key, target in _targets.items()
        if key != excluding_key
    }
    if base not in used:
        return base
    idx = 2
    while f"{base}_{idx}" in used:
        idx += 1
    return f"{base}_{idx}"


def _register_target(payload: dict[str, Any]) -> RegistryTarget:
    info = _normalize_registration(payload)
    key = _target_key(info["host"], info["port"])
    now = _utcnow()
    now_mono = time.monotonic()

    with _targets_lock:
        existing = _targets.get(key)
        target_id = _safe_str(payload.get("target_id")) or _make_target_id(info)
        if existing and existing.get("target_id") == target_id:
            alias = existing.get("alias") or _make_unique_alias(
                _alias_base(info), excluding_key=key
            )
            registered_at = existing.get("registered_at", now)
        else:
            alias = _make_unique_alias(_alias_base(info), excluding_key=key)
            registered_at = now

        target: RegistryTarget = {
            **info,
            "target_id": target_id,
            "alias": alias,
            "registered_at": registered_at,
            "last_seen": now,
            "last_seen_monotonic": now_mono,
        }
        _targets.pop(key, None)
        _targets[key] = target
        return _public_target(target)


def _unregister_target(payload: dict[str, Any]) -> bool:
    try:
        host = _safe_str(payload.get("host")) or "127.0.0.1"
        port = int(payload.get("port", 0))
    except (TypeError, ValueError):
        return False

    pid = payload.get("pid")
    try:
        pid_int = int(pid) if pid is not None else None
    except (TypeError, ValueError):
        pid_int = None

    key = _target_key(host, port)
    with _targets_lock:
        existing = _targets.get(key)
        if existing is None:
            return False
        if pid_int is not None and existing.get("pid") not in (0, pid_int):
            return False
        removed = _targets.pop(key, None) is not None
        if removed:
            _remove_session_target_ids_locked(existing.get("target_id", ""))
        return removed


def _remove_session_target_ids_locked(target_id: str) -> None:
    if not target_id:
        return
    with _session_targets_lock:
        for session_key, selected in list(_session_target_ids.items()):
            if selected == target_id:
                _session_target_ids.pop(session_key, None)
                _session_target_last_seen.pop(session_key, None)


def _prune_targets_locked(now: float | None = None) -> None:
    now = time.monotonic() if now is None else now
    for key, target in list(_targets.items()):
        last_seen = float(target.get("last_seen_monotonic") or 0.0)
        if TARGET_TTL_SEC > 0 and last_seen and now - last_seen > TARGET_TTL_SEC:
            _targets.pop(key, None)
            _remove_session_target_ids_locked(target.get("target_id", ""))


def _public_target(target: RegistryTarget, *, active_target_id: str | None = None) -> RegistryTarget:
    result: RegistryTarget = {
        key: value
        for key, value in target.items()
        if key != "last_seen_monotonic"
    }
    result["reachable"] = _target_reachable(result)
    if active_target_id is not None:
        result["active"] = result.get("target_id") == active_target_id
    return result


def _target_reachable(target: RegistryTarget, *, timeout: float = 0.5) -> bool:
    port = int(target.get("port", 0))
    return any(
        probe_instance(host, port, timeout=timeout)
        for host in _candidate_connect_hosts(target)
    )


def _get_transport_session_key() -> str | None:
    return mcp.get_current_transport_session_id()


def _prune_session_targets_locked(now: float | None = None) -> None:
    now = time.monotonic() if now is None else now
    for session_key in list(_session_target_ids):
        _session_target_last_seen.setdefault(session_key, now)

    if SESSION_TARGET_TTL_SEC > 0:
        cutoff = now - SESSION_TARGET_TTL_SEC
        for session_key, last_seen in list(_session_target_last_seen.items()):
            if last_seen < cutoff:
                _session_target_ids.pop(session_key, None)
                _session_target_last_seen.pop(session_key, None)

    for session_key in list(_session_target_last_seen):
        if session_key not in _session_target_ids:
            _session_target_last_seen.pop(session_key, None)

    if SESSION_TARGET_MAX_SIZE > 0:
        while len(_session_target_ids) > SESSION_TARGET_MAX_SIZE:
            session_key, _ = _session_target_ids.popitem(last=False)
            _session_target_last_seen.pop(session_key, None)


def _get_selected_target_id() -> str | None:
    session_key = _get_transport_session_key()
    if session_key is not None:
        now = time.monotonic()
        with _session_targets_lock:
            _prune_session_targets_locked(now)
            target_id = _session_target_ids.get(session_key)
            if target_id is not None:
                _session_target_ids.move_to_end(session_key)
                _session_target_last_seen[session_key] = now
                return target_id
    return _default_target_id


def _set_selected_target_id(target_id: str | None) -> None:
    global _default_target_id
    session_key = _get_transport_session_key()
    if session_key is not None:
        now = time.monotonic()
        with _session_targets_lock:
            if target_id is None:
                _session_target_ids.pop(session_key, None)
                _session_target_last_seen.pop(session_key, None)
            else:
                _session_target_ids.pop(session_key, None)
                _session_target_ids[session_key] = target_id
                _session_target_last_seen[session_key] = now
            _prune_session_targets_locked(now)
        return
    _default_target_id = target_id


def _list_target_records() -> list[RegistryTarget]:
    with _targets_lock:
        _prune_targets_locked()
        return [copy.deepcopy(target) for target in _targets.values()]


def _list_targets_public() -> list[RegistryTarget]:
    active_target_id = _get_selected_target_id()
    return [
        _public_target(target, active_target_id=active_target_id)
        for target in _list_target_records()
    ]


def _find_target_locked(target_ref: str) -> RegistryTarget:
    ref = target_ref.strip()
    if not ref:
        raise ValueError("Target is empty")
    ref_lower = ref.lower()

    def exact_matches() -> list[RegistryTarget]:
        matches: list[RegistryTarget] = []
        for target in _targets.values():
            if ref in (target.get("target_id"), target.get("alias")):
                matches.append(target)
            elif ref_lower == str(target.get("alias", "")).lower():
                matches.append(target)
            elif ref_lower == _target_key(target.get("host", ""), int(target.get("port", 0))).lower():
                matches.append(target)
        return matches

    matches = exact_matches()
    if not matches:
        for target in _targets.values():
            values = [
                str(target.get("port", "")),
                target.get("binary", ""),
                target.get("idb_path", ""),
                target.get("input_path", ""),
                os.path.basename(target.get("idb_path", "")),
                os.path.basename(target.get("input_path", "")),
            ]
            if any(ref_lower == str(value).lower() for value in values if value):
                matches.append(target)

    if not matches:
        available = ", ".join(
            f"{target.get('alias')}({target.get('target_id')})"
            for target in _targets.values()
        )
        raise ValueError(f"Unknown target '{target_ref}'. Available: {available or 'none'}")
    if len(matches) > 1:
        aliases = ", ".join(
            f"{target.get('alias')}({target.get('target_id')})" for target in matches
        )
        raise ValueError(f"Ambiguous target '{target_ref}'. Matches: {aliases}")
    return copy.deepcopy(matches[0])


def _resolve_target(target_ref: str | None = None) -> RegistryTarget:
    with _targets_lock:
        _prune_targets_locked()
        if target_ref:
            return _find_target_locked(str(target_ref))

        selected_id = _get_selected_target_id()
        if selected_id:
            try:
                return _find_target_locked(selected_id)
            except ValueError:
                _set_selected_target_id(None)

        if len(_targets) == 1:
            return copy.deepcopy(next(iter(_targets.values())))

        if not _targets:
            raise ValueError(
                "No IDA targets are registered. Start IDA MCP in at least one IDA window."
            )
        aliases = ", ".join(
            f"{target.get('alias')}({target.get('target_id')})"
            for target in _targets.values()
        )
        raise ValueError(
            "Multiple IDA targets are registered. Pass the 'target' argument or call "
            f"select_target first. Available: {aliases}"
        )


def _extract_output_id(response: dict[str, Any]) -> str | None:
    result = response.get("result")
    if not isinstance(result, dict):
        return None
    meta = result.get("_meta")
    if not isinstance(meta, dict):
        return None
    ida_meta = meta.get("ida_mcp")
    if not isinstance(ida_meta, dict):
        return None
    output_id = ida_meta.get("output_id")
    return output_id if isinstance(output_id, str) else None


def _remember_output_proxy_target(output_id: str, host: str, port: int) -> None:
    with _output_proxy_lock:
        _output_proxy_targets.pop(output_id, None)
        _output_proxy_targets[output_id] = (host, port)
        while len(_output_proxy_targets) > OUTPUT_PROXY_CACHE_MAX_SIZE:
            _output_proxy_targets.popitem(last=False)


def _get_output_proxy_target(output_id: str) -> tuple[str, int] | None:
    with _output_proxy_lock:
        target = _output_proxy_targets.get(output_id)
        if target is None:
            return None
        _output_proxy_targets.move_to_end(output_id)
        return target


def _remember_output_proxy_target_from_response(
    target: RegistryTarget, response: dict[str, Any], *, connect_host: str | None = None
) -> None:
    output_id = _extract_output_id(response)
    if output_id:
        _remember_output_proxy_target(
            output_id,
            connect_host or target.get("host", "127.0.0.1"),
            int(target.get("port", 0)),
        )


def _get_proxy_request_path() -> str:
    enabled = sorted(getattr(mcp._enabled_extensions, "data", set()))
    if enabled:
        return f"/mcp?ext={','.join(enabled)}"
    return "/mcp"


def _get_proxy_request_headers(*, host_header: str | None = None) -> dict[str, str]:
    headers = {
        "Content-Type": "application/json",
        PROXY_HEADER: "1",
    }
    if host_header:
        headers["Host"] = host_header
    transport_session_id = mcp.get_current_transport_session_id()
    if transport_session_id and transport_session_id.startswith("http:"):
        session_id = transport_session_id.split(":", 1)[1]
        if session_id and session_id != "anonymous":
            headers["Mcp-Session-Id"] = session_id
    external_base_url = get_current_request_external_base_url()
    if external_base_url:
        headers[EXTERNAL_BASE_HEADER] = external_base_url
    return headers


def _proxy_host_header(target: RegistryTarget, port: int) -> str | None:
    host = target.get("host", "")
    if _is_loopback_host(host):
        return f"127.0.0.1:{port}"
    return None


def _proxy_to_target(target: RegistryTarget, payload: bytes | str | dict[str, Any]) -> dict[str, Any]:
    if isinstance(payload, dict):
        payload = json.dumps(payload)
    if isinstance(payload, str):
        payload = payload.encode("utf-8")

    port = int(target.get("port", 0))
    last_error: Exception | None = None
    for host in _candidate_connect_hosts(target):
        conn = http.client.HTTPConnection(host, port, timeout=30)
        try:
            conn.request(
                "POST",
                _get_proxy_request_path(),
                payload,
                _get_proxy_request_headers(host_header=_proxy_host_header(target, port)),
            )
            response = conn.getresponse()
            raw_data = response.read().decode("utf-8")
            if response.status >= 400:
                raise RuntimeError(f"HTTP {response.status} {response.reason}: {raw_data}")
            parsed = json.loads(raw_data)
            if isinstance(parsed, dict):
                _remember_output_proxy_target_from_response(
                    target, parsed, connect_host=host
                )
                return parsed
            raise RuntimeError("Remote target returned a non-object JSON-RPC response")
        except (ConnectionError, OSError, TimeoutError) as e:
            last_error = e
        finally:
            conn.close()
    if last_error is not None:
        raise last_error
    raise RuntimeError("No target connection candidates available")


def _proxy_output_download(host: str, port: int, path: str) -> tuple[int, str, list[tuple[str, str]], bytes]:
    conn = http.client.HTTPConnection(host, port, timeout=30)
    try:
        headers = {PROXY_HEADER: "1"}
        if host == os.environ.get("IDA_MCP_HOST_GATEWAY", "host.docker.internal"):
            headers["Host"] = f"127.0.0.1:{port}"
        conn.request("GET", path, headers=headers)
        response = conn.getresponse()
        return response.status, response.reason, response.getheaders(), response.read()
    finally:
        conn.close()


def _remote_tools_for_target(target: RegistryTarget) -> list[dict[str, Any]]:
    request = {"jsonrpc": "2.0", "method": "tools/list", "id": "registry-tools-list"}
    response = _proxy_to_target(target, request)
    result = response.get("result")
    if not isinstance(result, dict):
        return []
    tools = result.get("tools", [])
    return [tool for tool in tools if isinstance(tool, dict)]


def _remote_resources_for_target(target: RegistryTarget) -> list[dict[str, Any]]:
    request = {"jsonrpc": "2.0", "method": "resources/list", "id": "registry-resources-list"}
    response = _proxy_to_target(target, request)
    result = response.get("result")
    if not isinstance(result, dict):
        return []
    resources = result.get("resources", [])
    return [resource for resource in resources if isinstance(resource, dict)]


def _wrap_remote_tool_schema(tool: dict[str, Any]) -> dict[str, Any]:
    wrapped = copy.deepcopy(tool)
    schema = wrapped.setdefault("inputSchema", {})
    if not isinstance(schema, dict):
        schema = {"type": "object"}
        wrapped["inputSchema"] = schema
    schema["type"] = "object"
    properties = schema.setdefault("properties", {})
    if not isinstance(properties, dict):
        properties = {}
        schema["properties"] = properties
    properties[TARGET_ARGUMENT] = {
        "type": "string",
        "description": (
            "IDA target id, alias, host:port, port, binary name, or path. "
            "Omit when only one target is registered or after select_target."
        ),
    }
    required = schema.get("required")
    if not isinstance(required, list):
        schema["required"] = []
    elif TARGET_ARGUMENT in required:
        schema["required"] = [item for item in required if item != TARGET_ARGUMENT]
    description = wrapped.get("description") or f"Call {wrapped.get('name', 'IDA tool')}"
    wrapped["description"] = (
        f"{description}\n\nRegistry-routed tool. Add target=... to choose an IDA window."
    )
    return wrapped


def _mcp_error_response(request_id: Any, message: str, data: Any = None) -> dict[str, Any]:
    error: dict[str, Any] = {"code": -32000, "message": message}
    if data is not None:
        error["data"] = data
    return {"jsonrpc": "2.0", "error": error, "id": request_id}


def _handle_tools_list(request: dict[str, Any] | str | bytes | bytearray) -> dict[str, Any] | None:
    local_result = _dispatch_original(request)
    if not local_result or "result" not in local_result:
        return local_result

    local_tools = local_result["result"].get("tools", [])
    local_names = {tool.get("name") for tool in local_tools if isinstance(tool, dict)}
    seen = set(local_names)
    remote_tools: list[dict[str, Any]] = []

    for target in _list_target_records():
        if not _target_reachable(target):
            continue
        try:
            for tool in _remote_tools_for_target(target):
                name = tool.get("name")
                if not isinstance(name, str):
                    continue
                if name in seen or name in REMOTE_LOCAL_TOOL_NAMES:
                    continue
                seen.add(name)
                remote_tools.append(_wrap_remote_tool_schema(tool))
        except Exception:
            continue

    local_result["result"]["tools"] = local_tools + remote_tools
    return local_result


def _handle_remote_tool_call(request_obj: dict[str, Any]) -> dict[str, Any] | None:
    params = request_obj.get("params", {})
    if not isinstance(params, dict):
        return _mcp_error_response(request_obj.get("id"), "Invalid tools/call params")

    arguments = params.get("arguments")
    if arguments is None:
        arguments = {}
    if not isinstance(arguments, dict):
        return _mcp_error_response(request_obj.get("id"), "Tool arguments must be an object")

    target_ref = arguments.get(TARGET_ARGUMENT)
    forwarded_request = copy.deepcopy(request_obj)
    forwarded_params = forwarded_request.setdefault("params", {})
    forwarded_args = copy.deepcopy(arguments)
    forwarded_args.pop(TARGET_ARGUMENT, None)
    forwarded_params["arguments"] = forwarded_args

    try:
        target = _resolve_target(str(target_ref) if target_ref else None)
        return _proxy_to_target(target, forwarded_request)
    except Exception as e:
        if request_obj.get("id") is None:
            return None
        return _mcp_error_response(
            request_obj.get("id"),
            "Failed to route tool call through IDA registry",
            {
                "error": str(e),
                "traceback": traceback.format_exc(),
            },
        )


def _dispatch_registry(request: dict[str, Any] | str | bytes | bytearray) -> dict[str, Any] | None:
    if not isinstance(request, dict):
        request_obj = json.loads(request)
    else:
        request_obj = request

    method = request_obj.get("method", "")
    if method == "initialize" or str(method).startswith("notifications/"):
        return _dispatch_original(request)

    if method == "tools/list":
        return _handle_tools_list(request)

    if method == "tools/call":
        params = request_obj.get("params", {})
        tool_name = params.get("name", "") if isinstance(params, dict) else ""
        if tool_name in LOCAL_TOOLS:
            return _dispatch_original(request)
        return _handle_remote_tool_call(request_obj)

    return _dispatch_original(request)


@mcp.tool
def registry_health() -> RegistryHealth:
    """Return health information for the local IDA MCP registry."""
    return {
        "status": "ok",
        "registry": "ida-mcp-registry",
        "host": REGISTRY_HOST,
        "port": REGISTRY_PORT,
        "targets": len(_list_target_records()),
    }


@mcp.tool
def list_targets() -> RegistryTargetList:
    """List all IDA databases currently registered with the central registry."""
    targets = _list_targets_public()
    return {"targets": targets, "count": len(targets)}


@mcp.tool
def get_target(
    target: Annotated[str, "Target id, alias, host:port, port, binary name, or path"],
) -> RegistryTarget:
    """Return metadata for one registered IDA target."""
    return _public_target(_resolve_target(target), active_target_id=_get_selected_target_id())


@mcp.tool
def select_target(
    target: Annotated[
        str | None,
        "Target id, alias, host:port, port, binary name, or path. Use null or empty string to clear.",
    ] = None,
) -> TargetSelectionResult:
    """Set the default IDA target for subsequent registry-routed tool calls."""
    if not target:
        _set_selected_target_id(None)
        return {"success": True, "target": None, "message": "Cleared selected target"}

    try:
        selected = _resolve_target(target)
        _set_selected_target_id(selected["target_id"])
        return {
            "success": True,
            "target": _public_target(selected, active_target_id=selected["target_id"]),
            "message": f"Selected target {selected.get('alias')} ({selected.get('target_id')})",
        }
    except Exception as e:
        return {"success": False, "target": None, "error": str(e)}


@mcp.tool
def list_target_tools(
    target: Annotated[
        str | None,
        "Target id, alias, host:port, port, binary name, or path. Optional when one target is registered.",
    ] = None,
) -> TargetToolsResult:
    """List raw MCP tools exposed by one registered IDA target."""
    try:
        resolved = _resolve_target(target)
        tools = _remote_tools_for_target(resolved)
        return {
            "target": _public_target(resolved, active_target_id=_get_selected_target_id()),
            "tools": tools,
            "count": len(tools),
        }
    except Exception as e:
        return {"error": str(e)}


@mcp.tool
def call_target_tool(
    name: Annotated[str, "Remote IDA MCP tool name"],
    arguments: Annotated[dict[str, Any] | None, "Remote tool arguments"] = None,
    target: Annotated[
        str | None,
        "Target id, alias, host:port, port, binary name, or path. Optional when one target is registered.",
    ] = None,
) -> dict[str, Any]:
    """Call a remote IDA MCP tool by name on a specific target."""
    request = {
        "jsonrpc": "2.0",
        "method": "tools/call",
        "params": {"name": name, "arguments": arguments or {}},
        "id": "registry-call-tool",
    }
    resolved = _resolve_target(target)
    response = _proxy_to_target(resolved, request)
    if "error" in response:
        return {"target": _public_target(resolved), "error": response["error"]}
    result = response.get("result", {})
    if isinstance(result, dict):
        return {
            "target": _public_target(resolved),
            "result": result.get("structuredContent", result),
            "_meta": result.get("_meta"),
        }
    return {"target": _public_target(resolved), "result": result}


@mcp.tool
def list_target_resources(
    target: Annotated[
        str | None,
        "Target id, alias, host:port, port, binary name, or path. Optional when one target is registered.",
    ] = None,
) -> TargetResourcesResult:
    """List MCP resources exposed by one registered IDA target."""
    try:
        resolved = _resolve_target(target)
        resources = _remote_resources_for_target(resolved)
        return {
            "target": _public_target(resolved, active_target_id=_get_selected_target_id()),
            "resources": resources,
            "count": len(resources),
        }
    except Exception as e:
        return {"error": str(e)}


@mcp.tool
def read_target_resource(
    uri: Annotated[str, "Remote MCP resource URI, for example ida://functions"],
    target: Annotated[
        str | None,
        "Target id, alias, host:port, port, binary name, or path. Optional when one target is registered.",
    ] = None,
) -> dict[str, Any]:
    """Read an MCP resource from one registered IDA target."""
    resolved = _resolve_target(target)
    request = {
        "jsonrpc": "2.0",
        "method": "resources/read",
        "params": {"uri": uri},
        "id": "registry-read-resource",
    }
    response = _proxy_to_target(resolved, request)
    if "error" in response:
        return {"target": _public_target(resolved), "error": response["error"]}
    return {"target": _public_target(resolved), "result": response.get("result")}


LOCAL_TOOLS = set(mcp.tools.methods)
mcp.registry.dispatch = _dispatch_registry


class RegistryHttpRequestHandler(McpHttpRequestHandler):
    def _send_json(self, status: int, payload: dict[str, Any]) -> None:
        body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_cors_headers()
        self.end_headers()
        self.wfile.write(body)

    def _read_json_payload(self) -> dict[str, Any] | None:
        if not self._check_api_request():
            return None
        body = self._read_body()
        if body is None:
            return None
        try:
            payload = json.loads(body.decode("utf-8") or "{}")
        except json.JSONDecodeError as e:
            self._send_json(400, {"ok": False, "error": f"Invalid JSON: {e}"})
            return None
        if not isinstance(payload, dict):
            self._send_json(400, {"ok": False, "error": "JSON payload must be an object"})
            return None
        return payload

    def do_POST(self):
        path = urlparse(self.path).path
        if path in (REGISTER_PATH, "/register"):
            payload = self._read_json_payload()
            if payload is None:
                return
            try:
                target = _register_target(payload)
                self._send_json(200, {"ok": True, "target": target})
            except Exception as e:
                self._send_json(400, {"ok": False, "error": str(e)})
            return

        if path in (UNREGISTER_PATH, "/unregister"):
            payload = self._read_json_payload()
            if payload is None:
                return
            removed = _unregister_target(payload)
            self._send_json(200, {"ok": True, "removed": removed})
            return

        super().do_POST()

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == TARGETS_PATH:
            if not self._check_api_request():
                return
            self._send_json(200, {"ok": True, **list_targets()})
            return

        output_match = _OUTPUT_PATH_RE.match(parsed.path)
        if output_match:
            if not self._check_api_request():
                return
            output_id = output_match.group(1)
            target = _get_output_proxy_target(output_id)
            if target is None:
                self.send_error(404, "Output not found or expired")
                return
            try:
                status, _, response_headers, body = _proxy_output_download(
                    target[0], target[1], parsed.path
                )
            except Exception as e:
                self.send_error(502, f"Failed to proxy output download: {e}")
                return

            self.send_response(status)
            for header, value in response_headers:
                if header.lower() == "transfer-encoding":
                    continue
                self.send_header(header, value)
            self.send_cors_headers()
            self.end_headers()
            self.wfile.write(body)
            return

        super().do_GET()


def _resolve_registry_transport(transport: str | None) -> tuple[str, int]:
    if not transport:
        return REGISTRY_HOST, REGISTRY_PORT
    parsed = urlparse(transport)
    if parsed.hostname is None or parsed.port is None:
        raise ValueError(f"Invalid registry transport URL: {transport}")
    return parsed.hostname, parsed.port


def main() -> None:
    parser = argparse.ArgumentParser(description="Central IDA Pro MCP registry")
    parser.add_argument(
        "--host",
        default=REGISTRY_HOST,
        help=f"Host to bind for MCP and registration (default: {REGISTRY_HOST})",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=REGISTRY_PORT,
        help=f"Port to bind for MCP and registration (default: {REGISTRY_PORT})",
    )
    parser.add_argument(
        "--transport",
        default=None,
        help=(
            "Optional HTTP URL to bind, for compatibility with other entrypoints. "
            "Example: http://127.0.0.1:9399/mcp"
        ),
    )
    parser.add_argument(
        "--stdio",
        action="store_true",
        help="Run MCP over stdio. Registration HTTP endpoints are not available in this mode.",
    )
    args = parser.parse_args()

    if args.stdio:
        mcp.stdio()
        return

    host, port = _resolve_registry_transport(args.transport)
    if args.transport is None:
        host, port = args.host, args.port

    print(f"[MCP Registry] Listening on http://{host}:{port}/mcp", file=sys.stderr)
    print(f"[MCP Registry] IDA plugin registration endpoint: http://{REGISTRY_HOST}:{REGISTRY_PORT}{REGISTER_PATH}", file=sys.stderr)
    try:
        mcp.serve(host, port, background=True, request_handler=RegistryHttpRequestHandler)
        while True:
            time.sleep(3600)
    except (KeyboardInterrupt, EOFError):
        pass
    finally:
        mcp.stop()


if __name__ == "__main__":
    main()
