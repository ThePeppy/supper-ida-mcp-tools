"""Core IDA analysis tools executed by the plugin."""

from __future__ import annotations

import re
import time
from typing import Any

from supper_ida_plugin.ida_runtime.addressing import clamp_int, parse_address, parse_function_start


def list_functions(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_funcs  # type: ignore[import-not-found]
    import idautils  # type: ignore[import-not-found]

    offset = clamp_int(arguments.get("offset"), 0, 0, 1_000_000)
    count = clamp_int(arguments.get("count"), 100, 1, 1_000)
    name_filter = str(arguments.get("filter") or "").lower()

    rows: list[dict[str, Any]] = []
    for ea in idautils.Functions():
        func = ida_funcs.get_func(ea)
        if func is None:
            continue
        name = ida_funcs.get_func_name(func.start_ea) or f"sub_{func.start_ea:x}"
        if name_filter and name_filter not in name.lower():
            continue
        rows.append(
            {
                "addr": hex(func.start_ea),
                "name": name,
                "size": func.end_ea - func.start_ea,
                "end": hex(func.end_ea),
            }
        )

    page = rows[offset : offset + count]
    next_offset = offset + count if offset + count < len(rows) else None
    return {"data": page, "nextOffset": next_offset, "total": len(rows)}


def get_function(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_funcs  # type: ignore[import-not-found]

    start = parse_function_start(arguments.get("query"))
    func = ida_funcs.get_func(start)
    if func is None:
        raise ValueError(f"no function at {hex(start)}")
    return {
        "addr": hex(func.start_ea),
        "name": ida_funcs.get_func_name(func.start_ea) or f"sub_{func.start_ea:x}",
        "size": func.end_ea - func.start_ea,
        "end": hex(func.end_ea),
    }


def decompile(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_hexrays  # type: ignore[import-not-found]
    import ida_lines  # type: ignore[import-not-found]

    start = parse_function_start(arguments.get("query"))
    try:
        cfunc = ida_hexrays.decompile(start)
    except Exception as exc:
        return {"addr": hex(start), "code": None, "error": str(exc)}

    if cfunc is None:
        return {"addr": hex(start), "code": None, "error": "Hex-Rays returned no decompilation"}

    code = ida_lines.tag_remove(str(cfunc))
    return {"addr": hex(start), "code": code}


def disassemble(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_funcs  # type: ignore[import-not-found]
    import ida_lines  # type: ignore[import-not-found]
    import idc  # type: ignore[import-not-found]

    start = parse_function_start(arguments.get("query"))
    max_lines = clamp_int(arguments.get("maxLines"), 200, 1, 5_000)
    func = ida_funcs.get_func(start)
    if func is None:
        raise ValueError(f"no function at {hex(start)}")

    lines: list[dict[str, str]] = []
    ea = func.start_ea
    while ea < func.end_ea and len(lines) < max_lines:
        text = ida_lines.tag_remove(idc.generate_disasm_line(ea, 0) or "")
        lines.append({"addr": hex(ea), "text": text})
        next_ea = idc.next_head(ea, func.end_ea)
        if next_ea <= ea:
            break
        ea = next_ea

    return {
        "addr": hex(func.start_ea),
        "lines": lines,
        "truncated": ea < func.end_ea,
    }


def xrefs(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_funcs  # type: ignore[import-not-found]
    import idaapi  # type: ignore[import-not-found]
    import ida_xref  # type: ignore[import-not-found]

    ea = parse_address(arguments.get("query"))
    direction = str(arguments.get("direction") or "both").lower()
    max_items = clamp_int(arguments.get("max"), 200, 1, 5_000)

    rows: list[dict[str, Any]] = []
    if direction in ("to", "both"):
        ref = ida_xref.get_first_cref_to(ea)
        while ref != idaapi.BADADDR and len(rows) < max_items:
            rows.append(_xref_row("to", ref, ea))
            ref = ida_xref.get_next_cref_to(ea, ref)

    if direction in ("from", "both"):
        ref = ida_xref.get_first_cref_from(ea)
        while ref != idaapi.BADADDR and len(rows) < max_items:
            rows.append(_xref_row("from", ea, ref))
            ref = ida_xref.get_next_cref_from(ea, ref)

    return {"addr": hex(ea), "direction": direction, "data": rows, "truncated": len(rows) >= max_items}


def list_strings(arguments: dict[str, Any]) -> dict[str, Any]:
    import idautils  # type: ignore[import-not-found]

    offset = clamp_int(arguments.get("offset"), 0, 0, 1_000_000)
    count = clamp_int(arguments.get("count"), 100, 1, 1_000)
    text_filter = str(arguments.get("filter") or "").lower()

    rows: list[dict[str, Any]] = []
    for item in idautils.Strings():
        if item is None:
            continue
        text = str(item)
        if text_filter and text_filter not in text.lower():
            continue
        rows.append({"addr": hex(item.ea), "text": text})

    page = rows[offset : offset + count]
    next_offset = offset + count if offset + count < len(rows) else None
    return {"data": page, "nextOffset": next_offset, "total": len(rows)}


def list_imports(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_nalt  # type: ignore[import-not-found]

    offset = clamp_int(arguments.get("offset"), 0, 0, 1_000_000)
    count = clamp_int(arguments.get("count"), 100, 1, 1_000)
    name_filter = str(arguments.get("filter") or "").lower()
    rows: list[dict[str, Any]] = []

    for module_index in range(ida_nalt.get_import_module_qty()):
        module_name = ida_nalt.get_import_module_name(module_index) or "<unnamed>"

        def callback(ea: int, name: str | None, ordinal: int) -> bool:
            import_name = name or f"#{ordinal}"
            if not name_filter or name_filter in import_name.lower() or name_filter in module_name.lower():
                rows.append({"addr": hex(ea), "name": import_name, "module": module_name})
            return True

        ida_nalt.enum_import_names(module_index, callback)

    page = rows[offset : offset + count]
    next_offset = offset + count if offset + count < len(rows) else None
    return {"data": page, "nextOffset": next_offset, "total": len(rows)}


def get_bytes(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_bytes  # type: ignore[import-not-found]

    ea = parse_address(arguments.get("address"))
    size = clamp_int(arguments.get("size"), 16, 1, 1_048_576)
    data = ida_bytes.get_bytes(ea, size)
    if data is None:
        raise ValueError(f"unable to read bytes at {hex(ea)}")
    return {
        "addr": hex(ea),
        "size": len(data),
        "hex": data.hex(),
        "ascii": "".join(chr(b) if 32 <= b < 127 else "." for b in data),
    }


def rename(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_name  # type: ignore[import-not-found]

    ea = parse_address(arguments.get("address"))
    new_name = str(arguments.get("newName") or "").strip()
    if not new_name:
        raise ValueError("newName is required")
    ok = ida_name.set_name(ea, new_name, ida_name.SN_CHECK)
    return {"addr": hex(ea), "newName": new_name, "ok": bool(ok)}


def set_comment(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_bytes  # type: ignore[import-not-found]

    ea = parse_address(arguments.get("address"))
    text = str(arguments.get("text") or "")
    repeatable = bool(arguments.get("repeatable") or False)
    ok = ida_bytes.set_cmt(ea, text, repeatable)
    return {"addr": hex(ea), "repeatable": repeatable, "ok": bool(ok)}


def _bool_arg(value: Any, default: bool) -> bool:
    if value is None:
        return default
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        return value.strip().lower() in ("1", "true", "yes", "on")
    return bool(value)


def _search_text_segments(code_only: bool) -> list[tuple[int, int]]:
    import ida_segment  # type: ignore[import-not-found]
    import idaapi  # type: ignore[import-not-found]
    import idautils  # type: ignore[import-not-found]

    ranges: list[tuple[int, int]] = []
    for seg_ea in idautils.Segments():
        seg = idaapi.getseg(seg_ea)
        if seg is None:
            continue
        if code_only and not (seg.perm & idaapi.SEGPERM_EXEC):
            continue
        ranges.append((seg.start_ea, seg.end_ea))
    return ranges


def search_text(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_funcs  # type: ignore[import-not-found]
    import ida_bytes  # type: ignore[import-not-found]
    import ida_lines  # type: ignore[import-not-found]
    import ida_segment  # type: ignore[import-not-found]
    import idaapi  # type: ignore[import-not-found]
    import idautils  # type: ignore[import-not-found]
    import idc  # type: ignore[import-not-found]

    pattern = str(arguments.get("pattern") or "")
    if not pattern:
        return {"pattern": pattern, "n": 0, "hits": [], "cursor": {"done": True}, "error": "pattern is required"}

    limit = clamp_int(arguments.get("limit", arguments.get("max")), 30, 1, 500)
    scan_limit = clamp_int(arguments.get("scanLimit", arguments.get("scan_limit")), 25_000, 100, 250_000)
    time_limit_ms = clamp_int(arguments.get("timeLimitMs", arguments.get("time_limit_ms")), 2_500, 250, 30_000)
    start = str(arguments.get("start") or "")
    regex_mode = _bool_arg(arguments.get("regex"), False)
    case_sensitive = _bool_arg(arguments.get("caseSensitive", arguments.get("case_sensitive")), False)
    include = str(arguments.get("include") or "all").lower()
    code_only = _bool_arg(arguments.get("codeOnly", arguments.get("code_only")), True)

    if include not in ("disasm", "comments", "all"):
        return {
            "pattern": pattern,
            "n": 0,
            "hits": [],
            "cursor": {"done": True},
            "error": f"invalid include: {include!r}",
        }

    want_disasm = include in ("disasm", "all")
    want_comments = include in ("comments", "all")

    if regex_mode:
        try:
            flags = 0 if case_sensitive else re.IGNORECASE
            compiled = re.compile(pattern, flags)
        except re.error as exc:
            return {
                "pattern": pattern,
                "n": 0,
                "hits": [],
                "cursor": {"done": True},
                "error": f"invalid regex: {exc}",
            }

        def matcher(text: str) -> bool:
            return bool(compiled.search(text))

    elif case_sensitive:
        def matcher(text: str) -> bool:
            return pattern in text

    else:
        needle = pattern.lower()

        def matcher(text: str) -> bool:
            return needle in text.lower()

    segments = _search_text_segments(code_only)
    if not segments:
        return {
            "pattern": pattern,
            "n": 0,
            "hits": [],
            "cursor": {"done": True},
            "scanned": 0,
            "partial": False,
        }

    if start:
        try:
            cursor_ea = parse_address(start)
        except Exception as exc:
            return {
                "pattern": pattern,
                "n": 0,
                "hits": [],
                "cursor": {"done": True},
                "error": f"invalid start: {exc}",
            }
    else:
        cursor_ea = segments[0][0]

    hits: list[dict[str, Any]] = []
    next_cursor: int | None = None
    segment_index = 0
    while segment_index < len(segments) and segments[segment_index][1] <= cursor_ea:
        segment_index += 1
    if segment_index < len(segments) and cursor_ea < segments[segment_index][0]:
        cursor_ea = segments[segment_index][0]

    scanned = 0
    deadline = time.monotonic() + (time_limit_ms / 1000.0)
    stop_reason: str | None = None

    while segment_index < len(segments) and len(hits) < limit:
        seg_start, seg_end = segments[segment_index]
        ea = cursor_ea
        for head in idautils.Heads(max(seg_start, ea), seg_end):
            scanned += 1
            if scanned >= scan_limit:
                next_cursor = head
                stop_reason = "scanLimit"
                break
            if time.monotonic() >= deadline:
                next_cursor = head
                stop_reason = "timeLimit"
                break

            lines: list[dict[str, str]] = []
            if want_disasm:
                tagged = idc.generate_disasm_line(head, 0) or ""
                text = ida_lines.tag_remove(tagged) or ""
                if text and matcher(text):
                    lines.append({"kind": "disasm", "text": text})

            if want_comments:
                for comment_text in (
                    ida_bytes.get_cmt(head, False),
                    ida_bytes.get_cmt(head, True),
                ):
                    if comment_text and matcher(comment_text):
                        lines.append({"kind": "comment", "text": comment_text})

            if not lines:
                cursor_ea = head + max(1, idaapi.get_item_size(head))
                continue

            hit = {
                "addr": hex(head),
                "matches": lines,
                "text": "\n".join(line["text"] for line in lines),
            }
            func = ida_funcs.get_func(head)
            if func is not None:
                hit["function"] = ida_funcs.get_func_name(func.start_ea) or f"sub_{func.start_ea:x}"
            seg = idaapi.getseg(head)
            if seg is not None:
                segment_name = ida_segment.get_segm_name(seg)
                if segment_name:
                    hit["segment"] = segment_name
            hits.append(hit)
            cursor_ea = head + max(1, idaapi.get_item_size(head))
            if len(hits) >= limit:
                next_cursor = cursor_ea
                break

        if next_cursor is not None:
            break

        segment_index += 1
        if segment_index < len(segments):
            cursor_ea = segments[segment_index][0]

    partial = next_cursor is not None
    cursor = {"next": hex(next_cursor)} if partial else {"done": True}
    return {
        "pattern": pattern,
        "n": len(hits),
        "hits": hits,
        "cursor": cursor,
        "truncated": partial,
        "partial": partial,
        "stopReason": stop_reason,
        "scanned": scanned,
        "scanLimit": scan_limit,
        "timeLimitMs": time_limit_ms,
    }


def _xref_row(direction: str, source: int, target: int) -> dict[str, Any]:
    import ida_funcs  # type: ignore[import-not-found]
    import ida_name  # type: ignore[import-not-found]

    func = ida_funcs.get_func(source)
    return {
        "direction": direction,
        "from": hex(source),
        "to": hex(target),
        "fromName": ida_name.get_name(source) or None,
        "toName": ida_name.get_name(target) or None,
        "function": ida_funcs.get_func_name(func.start_ea) if func else None,
    }
