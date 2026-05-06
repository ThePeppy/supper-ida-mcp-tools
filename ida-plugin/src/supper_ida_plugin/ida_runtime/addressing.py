"""Address and function lookup helpers for IDA runtime tools."""

from __future__ import annotations

from typing import Any


def parse_address(query: Any) -> int:
    if isinstance(query, int):
        return query

    if not isinstance(query, str) or not query.strip():
        raise ValueError("address query must be a non-empty string or integer")

    text = query.strip()
    try:
        return int(text, 0)
    except ValueError:
        pass

    try:
        import idaapi  # type: ignore[import-not-found]
        import idc  # type: ignore[import-not-found]
    except ImportError as exc:
        raise ValueError(f"cannot resolve symbolic address outside IDA: {text}") from exc

    if text.startswith("sub_"):
        try:
            return int(text[4:], 16)
        except ValueError:
            pass

    ea = idc.get_name_ea_simple(text)
    if ea == idaapi.BADADDR:
        raise ValueError(f"unable to resolve address: {text}")
    return ea


def parse_function_start(query: Any) -> int:
    ea = parse_address(query)
    try:
        import ida_funcs  # type: ignore[import-not-found]
        import idaapi  # type: ignore[import-not-found]
    except ImportError as exc:
        raise RuntimeError("IDA function lookup is unavailable outside IDA") from exc

    func = ida_funcs.get_func(ea)
    if func is None:
        raise ValueError(f"no function contains address: {hex(ea)}")
    if func.start_ea == idaapi.BADADDR:
        raise ValueError(f"invalid function address: {hex(ea)}")
    return func.start_ea


def clamp_int(value: Any, default: int, minimum: int, maximum: int) -> int:
    try:
        parsed = int(value)
    except (TypeError, ValueError):
        parsed = default
    return max(minimum, min(maximum, parsed))
