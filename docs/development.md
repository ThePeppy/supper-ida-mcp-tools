# Development

Runtime code is intentionally separated by language:

- Python code only under `ida-plugin/`.
- .NET code only under `mcp-center/`.
- Shared protocol is documented under `docs/`; each side implements its own language-specific protocol types.

Do not add runtime code under `docs/reference/`; it is archived reference only.

Packaged desktop builds copy Python plugin sources into `PluginBundle/` during
.NET publish. This is distribution content only; Python source still lives under
`ida-plugin/`.

Release and CI workflow details live in `docs/release-workflow.md`.
Maintainer checklists and release guardrails live in `docs/maintainer-handbook.md`.
