"""Protocol message builders used by the IDA executor."""

from __future__ import annotations

from typing import Any


def hello(instance_id: str, metadata: dict[str, Any]) -> dict[str, Any]:
    return {
        "type": "hello",
        "instanceId": instance_id,
        "payload": metadata,
    }


def heartbeat(instance_id: str) -> dict[str, Any]:
    return {"type": "heartbeat", "instanceId": instance_id}


def tool_result(
    instance_id: str,
    request_id: str | None,
    *,
    ok: bool,
    result: Any | None = None,
    error: str | None = None,
) -> dict[str, Any]:
    return {
        "type": "tool_result",
        "id": request_id,
        "instanceId": instance_id,
        "payload": {
            "ok": ok,
            "result": result,
            "error": error,
        },
    }
