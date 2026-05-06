# Auto Launch IDA

The center owns IDA discovery and launch. The IDA plugin still only connects to
`127.0.0.1:9399` after IDA starts.

## Discovery Order

`mcp-center` resolves IDA executables in this order:

1. `idaPath` passed to an MCP tool call.
2. `--ida-path` passed to the center process.
3. `SUPPER_IDA_PATH`.
4. `IDA_PATH`.
5. Platform defaults.

Platform defaults:

- macOS: `/Applications/IDA*.app` and `~/Applications/IDA*.app`
- Windows: `Program Files`, `Program Files (x86)`, and local app data folders
- Linux/Unix: `/opt`, `/usr/local`, and the user home directory

The path may point to an executable, an IDA install directory, or a macOS `.app`
bundle. The locator prefers `ida64`, `ida`, `idat64`, and `idat` variants.

## MCP Tools

### `ida_find_installations`

Lists discovered IDA executables.

Arguments:

- `idaPath`: optional explicit executable, app bundle, or install directory.

### `ida_launch_file`

Starts IDA with a selected input file.

Arguments:

- `inputPath`: file to open.
- `idaPath`: optional explicit executable, app bundle, or install directory.
- `arguments`: optional extra IDA CLI arguments placed before `inputPath`.
- `waitSeconds`: optional time to wait for plugin registration. Default is 20.

The result contains the launched process and, if registration completed in time,
the registered target.

### `ida_list_launched_processes`

Lists processes started by the center.

### `ida_close_target`

Closes a registered IDA target by `instanceId`.

Arguments:

- `instanceId`
- `force`: if true, kill the process when normal window close does not exit.
