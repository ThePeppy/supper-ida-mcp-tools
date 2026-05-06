# Development

Runtime code is intentionally separated by language:

- Python code only under `ida-plugin/`.
- .NET code only under `mcp-center/`.
- Shared protocol is documented under `docs/`; each side implements its own language-specific protocol types.

Do not add runtime code under `docs/reference/`; it is archived reference only.
