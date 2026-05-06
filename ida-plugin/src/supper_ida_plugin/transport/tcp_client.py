"""TCP client used by the IDA plugin executor."""

from __future__ import annotations

import socket
import select
import threading
import time
import uuid
from types import TracebackType
from typing import Self

from supper_ida_plugin.config.constants import DEFAULT_CENTER_HOST, DEFAULT_CENTER_PORT
from supper_ida_plugin.ida_runtime.metadata import collect_metadata
from supper_ida_plugin.protocol.framing import encode_message, read_message
from supper_ida_plugin.protocol.messages import heartbeat, hello


class CenterTcpClient:
    def __init__(
        self,
        host: str = DEFAULT_CENTER_HOST,
        port: int = DEFAULT_CENTER_PORT,
        *,
        heartbeat_interval_sec: float = 5.0,
        reconnect_delay_sec: float = 3.0,
    ) -> None:
        self.host = host
        self.port = port
        self.heartbeat_interval_sec = heartbeat_interval_sec
        self.reconnect_delay_sec = reconnect_delay_sec
        self.instance_id = str(uuid.uuid4())
        self._stop_event = threading.Event()
        self._thread: threading.Thread | None = None

    def start(self) -> None:
        if self._thread and self._thread.is_alive():
            return
        self._stop_event.clear()
        self._thread = threading.Thread(
            target=self._run,
            name="supper-ida-center-client",
            daemon=True,
        )
        self._thread.start()

    def stop(self) -> None:
        self._stop_event.set()
        if self._thread and self._thread.is_alive():
            self._thread.join(timeout=2.0)
        self._thread = None

    def __enter__(self) -> Self:
        self.start()
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        traceback: TracebackType | None,
    ) -> None:
        self.stop()

    def _run(self) -> None:
        while not self._stop_event.is_set():
            try:
                self._connect_and_pump()
            except OSError as exc:
                print(f"[Supper IDA MCP] center connection failed: {exc}")
            except Exception as exc:
                print(f"[Supper IDA MCP] center client error: {exc}")

            self._stop_event.wait(self.reconnect_delay_sec)

    def _connect_and_pump(self) -> None:
        with socket.create_connection((self.host, self.port), timeout=5.0) as sock:
            sock.settimeout(None)
            stream = sock.makefile("rwb", buffering=0)
            metadata = collect_metadata(self.instance_id)
            sock.sendall(encode_message(hello(self.instance_id, metadata)))
            print(f"[Supper IDA MCP] connected to center {self.host}:{self.port}")

            next_heartbeat = 0.0
            while not self._stop_event.is_set():
                now = time.monotonic()
                if now >= next_heartbeat:
                    sock.sendall(encode_message(heartbeat(self.instance_id)))
                    next_heartbeat = now + self.heartbeat_interval_sec

                readable, _, _ = select.select([sock], [], [], 0.25)
                if not readable:
                    continue

                message = read_message(stream)
                if message is None:
                    raise ConnectionError("center closed the connection")
                self._handle_message(message)

    def _handle_message(self, message: dict) -> None:
        message_type = message.get("type")
        if message_type == "shutdown":
            self._stop_event.set()
