"""Length-prefixed JSON message framing."""

from __future__ import annotations

import json
import struct
from typing import Any, BinaryIO

_HEADER = struct.Struct(">I")


def encode_message(message: dict[str, Any]) -> bytes:
    payload = json.dumps(message, separators=(",", ":")).encode("utf-8")
    return _HEADER.pack(len(payload)) + payload


def read_message(stream: BinaryIO) -> dict[str, Any] | None:
    header = stream.read(_HEADER.size)
    if not header:
        return None
    if len(header) != _HEADER.size:
        raise EOFError("incomplete message header")
    (length,) = _HEADER.unpack(header)
    payload = stream.read(length)
    if len(payload) != length:
        raise EOFError("incomplete message payload")
    value = json.loads(payload.decode("utf-8"))
    if not isinstance(value, dict):
        raise ValueError("protocol message must be a JSON object")
    return value
