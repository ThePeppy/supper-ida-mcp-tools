"""Client helpers for registering an IDA MCP plugin instance with the registry."""

from __future__ import annotations

import http.client
import json
from typing import Any


REGISTRY_HOST = "127.0.0.1"
REGISTRY_PORT = 9399
REGISTER_PATH = "/registry/register"
UNREGISTER_PATH = "/registry/unregister"


def _post_json(path: str, payload: dict[str, Any], *, timeout: float = 2.0) -> tuple[bool, dict[str, Any]]:
    body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    conn = http.client.HTTPConnection(REGISTRY_HOST, REGISTRY_PORT, timeout=timeout)
    try:
        conn.request(
            "POST",
            path,
            body,
            {"Content-Type": "application/json"},
        )
        response = conn.getresponse()
        raw = response.read().decode("utf-8", errors="replace")
        try:
            parsed = json.loads(raw) if raw else {}
        except json.JSONDecodeError:
            parsed = {"raw": raw}
        if response.status >= 400:
            return False, {
                "error": f"HTTP {response.status} {response.reason}",
                "response": parsed,
            }
        return bool(parsed.get("ok", True)), parsed
    except Exception as e:
        return False, {"error": str(e)}
    finally:
        conn.close()


def register_instance(payload: dict[str, Any], *, timeout: float = 2.0) -> tuple[bool, dict[str, Any]]:
    """Register or refresh one IDA MCP plugin instance."""
    return _post_json(REGISTER_PATH, payload, timeout=timeout)


def unregister_instance(payload: dict[str, Any], *, timeout: float = 2.0) -> tuple[bool, dict[str, Any]]:
    """Remove one IDA MCP plugin instance registration."""
    return _post_json(UNREGISTER_PATH, payload, timeout=timeout)
