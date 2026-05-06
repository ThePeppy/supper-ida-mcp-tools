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
