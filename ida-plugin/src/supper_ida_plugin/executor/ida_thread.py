"""Helpers for executing code on IDA's main thread."""

from __future__ import annotations

from collections.abc import Callable
import os
import queue
import sys
import time
from typing import TypeVar

T = TypeVar("T")

_DEFAULT_TOOL_TIMEOUT_SEC = 55.0
_TOOL_TIMEOUT_ENV = "SUPPER_IDA_TOOL_TIMEOUT_SEC"


def _tool_timeout_seconds() -> float:
    value = os.environ.get(_TOOL_TIMEOUT_ENV, "").strip()
    if not value:
        return _DEFAULT_TOOL_TIMEOUT_SEC
    try:
        return float(value)
    except ValueError:
        return _DEFAULT_TOOL_TIMEOUT_SEC


def run_on_ida_thread(callback: Callable[[], T]) -> T:
    try:
        import idaapi  # type: ignore[import-not-found]
        import idc  # type: ignore[import-not-found]
        import ida_kernwin  # type: ignore[import-not-found]
    except ImportError:
        return callback()

    results: queue.Queue[T | BaseException] = queue.Queue(maxsize=1)

    def wrapper() -> int:
        old_batch = idc.batch(1)
        old_profile = sys.getprofile()
        timeout = _tool_timeout_seconds()
        deadline = time.monotonic() + timeout if timeout > 0 else None

        def profilefunc(frame, event, arg):  # noqa: ANN001, ARG001 - CPython tracing callback signature.
            if deadline is not None and time.monotonic() >= deadline:
                raise TimeoutError(f"IDA tool timed out after {timeout:.2f}s")

        sys.setprofile(profilefunc)
        try:
            results.put(callback())
        except BaseException as exc:  # noqa: BLE001 - propagate IDA-side errors to the caller.
            results.put(exc)
        finally:
            sys.setprofile(old_profile)
            idc.batch(old_batch)
        return 1

    ida_kernwin.execute_sync(wrapper, idaapi.MFF_WRITE)
    result = results.get()
    if isinstance(result, BaseException):
        raise result
    return result
