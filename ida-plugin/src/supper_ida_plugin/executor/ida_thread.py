"""Helpers for executing code on IDA's main thread."""

from __future__ import annotations

from collections.abc import Callable
from typing import TypeVar

T = TypeVar("T")


def run_on_ida_thread(callback: Callable[[], T]) -> T:
    try:
        import ida_kernwin  # type: ignore[import-not-found]
    except ImportError:
        return callback()

    result: dict[str, T] = {}
    error: dict[str, BaseException] = {}

    def wrapper() -> int:
        try:
            result["value"] = callback()
        except BaseException as exc:  # noqa: BLE001 - propagate IDA-side errors to the caller.
            error["value"] = exc
        return 1

    ida_kernwin.execute_sync(wrapper, ida_kernwin.MFF_READ)
    if error:
        raise error["value"]
    return result["value"]
