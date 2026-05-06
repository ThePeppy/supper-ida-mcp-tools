"""Core IDA analysis tools executed by the plugin."""

from __future__ import annotations

import re
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


def search_text(arguments: dict[str, Any]) -> dict[str, Any]:
    import ida_funcs  # type: ignore[import-not-found]
    import ida_lines  # type: ignore[import-not-found]
    import idautils  # type: ignore[import-not-found]
    import idc  # type: ignore[import-not-found]

    pattern = str(arguments.get("pattern") or "")
    if not pattern:
        raise ValueError("pattern is required")
    max_items = clamp_int(arguments.get("max"), 100, 1, 1_000)
    regex = re.compile(pattern, re.IGNORECASE)

    hits: list[dict[str, Any]] = []
    for start in idautils.Functions():
        func = ida_funcs.get_func(start)
        if func is None:
            continue
        ea = func.start_ea
        while ea < func.end_ea:
            text = ida_lines.tag_remove(idc.generate_disasm_line(ea, 0) or "")
            if regex.search(text):
                hits.append(
                    {
                        "addr": hex(ea),
                        "function": ida_funcs.get_func_name(func.start_ea) or f"sub_{func.start_ea:x}",
                        "text": text,
                    }
                )
                if len(hits) >= max_items:
                    return {"pattern": pattern, "hits": hits, "truncated": True}
            next_ea = idc.next_head(ea, func.end_ea)
            if next_ea <= ea:
                break
            ea = next_ea

    return {"pattern": pattern, "hits": hits, "truncated": False}


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
