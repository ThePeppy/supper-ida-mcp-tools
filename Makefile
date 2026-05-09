SHELL := /bin/bash
.SHELLFLAGS := -euo pipefail -c

DOTNET ?= dotnet
PYTHON ?= python3
PWSH ?= pwsh
CONFIGURATION ?= Release
VERSION ?= $(shell awk '/^\#\# / { print $$2; exit }' CHANGELOG.md)
RID_MACOS ?= osx-arm64
RID_WINDOWS ?= win-x64

SOLUTION := mcp-center/SupperIdaMcpTools.sln
DESKTOP_PROJECT := mcp-center/src/SupperIdaMcp.Center.Desktop/SupperIdaMcp.Center.Desktop.csproj
APP_PROJECT := mcp-center/src/SupperIdaMcp.Center.App/SupperIdaMcp.Center.App.csproj
BRIDGE_PROJECT := mcp-center/src/SupperIdaMcp.Center.Bridge/SupperIdaMcp.Center.Bridge.csproj
DESKTOP_BIN := mcp-center/src/SupperIdaMcp.Center.Desktop/bin/$(CONFIGURATION)/net10.0
PLUGIN_ENTRY := $(DESKTOP_BIN)/PluginBundle/ida-plugin/src/supper_ida_plugin/entry.py
PLUGIN_TCP := $(DESKTOP_BIN)/PluginBundle/ida-plugin/src/supper_ida_plugin/transport/tcp_client.py
MACOS_DMG := artifacts/macos/SupperIdaMcpCenter-$(VERSION)-$(RID_MACOS).dmg
WINDOWS_ZIP := artifacts/windows/SupperIdaMcpCenter-$(VERSION)-$(RID_WINDOWS).zip

.DEFAULT_GOAL := help

.PHONY: help
help:
	@printf '%s\n' 'Supper IDA MCP Tools developer commands'
	@printf '\n'
	@printf '%s\n' 'Common:'
	@printf '  %-24s %s\n' 'make check' 'Run the same core checks as CI.'
	@printf '  %-24s %s\n' 'make build' 'Restore and build the .NET solution.'
	@printf '  %-24s %s\n' 'make run' 'Run the desktop center.'
	@printf '  %-24s %s\n' 'make install-plugin' 'Install or repair the local IDA plugin.'
	@printf '  %-24s %s\n' 'make package-macos' 'Build signed macOS DMG locally.'
	@printf '\n'
	@printf '%s\n' 'Validation:'
	@printf '  %-24s %s\n' 'make restore' 'Restore .NET dependencies.'
	@printf '  %-24s %s\n' 'make check-python' 'Compile plugin Python and dry-run installer.'
	@printf '  %-24s %s\n' 'make check-bundle' 'Verify Desktop output contains PluginBundle resources.'
	@printf '  %-24s %s\n' 'make diff-check' 'Run git whitespace conflict checks.'
	@printf '  %-24s %s\n' 'make doctor' 'Print local toolchain versions.'
	@printf '\n'
	@printf '%s\n' 'Packaging:'
	@printf '  %-24s %s\n' 'make package-windows' 'Build Windows portable ZIP with PowerShell.'
	@printf '  %-24s %s\n' 'make verify-macos-package' 'Verify the local macOS app and DMG signatures.'
	@printf '\n'
	@printf '%s\n' 'Runtime:'
	@printf '  %-24s %s\n' 'make run-minimized' 'Run the desktop center minimized to tray/menu bar.'
	@printf '  %-24s %s\n' 'make run-app' 'Run the console center app.'
	@printf '  %-24s %s\n' 'make run-bridge' 'Run the stdio MCP bridge.'
	@printf '\n'
	@printf '%s\n' 'Cleanup:'
	@printf '  %-24s %s\n' 'make clean' 'Clean build outputs and local artifacts.'
	@printf '\n'
	@printf '%s\n' 'Variables:'
	@printf '  %-24s %s\n' 'CONFIGURATION=Debug' 'Override build configuration. Default: Release.'
	@printf '  %-24s %s\n' 'VERSION=0.1.2' 'Override package version. Default: latest CHANGELOG heading.'
	@printf '  %-24s %s\n' 'RID_MACOS=osx-x64' 'Override macOS runtime identifier.'
	@printf '  %-24s %s\n' 'RID_WINDOWS=win-arm64' 'Override Windows runtime identifier.'

.PHONY: doctor
doctor:
	@printf 'DOTNET: '; $(DOTNET) --version
	@printf 'PYTHON: '; $(PYTHON) --version
	@if command -v $(PWSH) >/dev/null 2>&1; then printf 'PWSH: '; $(PWSH) -NoLogo -NoProfile -Command '$$PSVersionTable.PSVersion.ToString()'; else printf '%s\n' 'PWSH: not found'; fi
	@printf 'CONFIGURATION: %s\n' '$(CONFIGURATION)'
	@printf 'VERSION: %s\n' '$(VERSION)'

.PHONY: restore
restore:
	$(DOTNET) restore $(SOLUTION)

.PHONY: build
build: restore
	$(DOTNET) build $(SOLUTION) --configuration $(CONFIGURATION) --no-restore

.PHONY: build-no-restore
build-no-restore:
	$(DOTNET) build $(SOLUTION) --configuration $(CONFIGURATION) --no-restore

.PHONY: check-python
check-python:
	$(PYTHON) -m compileall -q ida-plugin/src
	$(PYTHON) ida-plugin/install.py --dry-run

.PHONY: check-bundle
check-bundle:
	test -f "$(PLUGIN_ENTRY)"
	test -f "$(PLUGIN_TCP)"

.PHONY: diff-check
diff-check:
	git diff --check

.PHONY: check
check: build check-python check-bundle diff-check

.PHONY: test
test: check

.PHONY: run
run:
	$(DOTNET) run --project $(DESKTOP_PROJECT) --configuration $(CONFIGURATION)

.PHONY: run-minimized
run-minimized:
	$(DOTNET) run --project $(DESKTOP_PROJECT) --configuration $(CONFIGURATION) -- --start-minimized

.PHONY: run-app
run-app:
	$(DOTNET) run --project $(APP_PROJECT) --configuration $(CONFIGURATION)

.PHONY: run-bridge
run-bridge:
	$(DOTNET) run --project $(BRIDGE_PROJECT) --configuration $(CONFIGURATION) -- --endpoint http://127.0.0.1:9401/mcp

.PHONY: install-plugin
install-plugin:
	$(PYTHON) ida-plugin/install.py

.PHONY: uninstall-plugin
uninstall-plugin:
	$(PYTHON) ida-plugin/uninstall.py

.PHONY: dry-run-plugin
dry-run-plugin:
	$(PYTHON) ida-plugin/install.py --dry-run

.PHONY: package-macos
package-macos:
	VERSION="$(VERSION)" RID="$(RID_MACOS)" CONFIGURATION="$(CONFIGURATION)" mcp-center/packaging/macos/package-dmg.sh

.PHONY: package-windows
package-windows:
	$(PWSH) -NoLogo -NoProfile -ExecutionPolicy Bypass -File mcp-center/packaging/windows/package-zip.ps1 -Version "$(VERSION)" -RuntimeIdentifier "$(RID_WINDOWS)" -Configuration "$(CONFIGURATION)"

.PHONY: verify-macos-package
verify-macos-package:
	test -f "artifacts/macos/Supper IDA MCP Center.app/Contents/MacOS/PluginBundle/ida-plugin/src/supper_ida_plugin/entry.py"
	test -x "artifacts/macos/Supper IDA MCP Center.app/Contents/MacOS/Bridge/SupperIdaMcp.Center.Bridge"
	codesign --verify --deep --strict --verbose=2 "artifacts/macos/Supper IDA MCP Center.app"
	codesign --verify --verbose=2 "$(MACOS_DMG)"

.PHONY: package
package:
	@if [[ "$$(uname -s)" == "Darwin" ]]; then \
		$(MAKE) package-macos; \
	elif command -v $(PWSH) >/dev/null 2>&1; then \
		$(MAKE) package-windows; \
	else \
		printf '%s\n' 'No local package target available. Use make package-macos on macOS or install pwsh for package-windows.' >&2; \
		exit 1; \
	fi

.PHONY: clean
clean:
	$(DOTNET) clean $(SOLUTION) --configuration $(CONFIGURATION)
	rm -rf artifacts
	find ida-plugin -type d -name __pycache__ -prune -exec rm -rf {} +
	find ida-plugin -type f -name '*.pyc' -delete
