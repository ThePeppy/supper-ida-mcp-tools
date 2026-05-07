# Maintainer Handbook

这份文档是项目维护专用手册。以后只要涉及版本、打包、发布、插件安装逻辑或 CI/CD 变更，先看这里，避免忘记当前约定。

## 维护原则

- 用户下载成品程序后，必须能直接在 Settings 中安装或修复 IDA 插件，不要求 clone 仓库。
- Python 运行时代码只放在 `ida-plugin/`，.NET 运行时代码只放在 `mcp-center/`。
- 发布包必须同时包含 Desktop、`PluginBundle/` 和 `Bridge/`。
- 日常开发不打正式包，正式发布只通过版本 tag 控制。
- GitHub Release 默认先生成 draft，人工验证后再公开发布。

## 仓库结构职责

- `ida-plugin/`: IDA Pro 内部执行插件源码。
- `mcp-center/`: 桌面管理中心、MCP 服务、TCP Hub、Bridge、打包脚本。
- `docs/`: 架构、协议、使用、发布和维护文档。
- `.github/workflows/`: GitHub Actions CI/CD 工作流。
- `images/`: README 和产品截图资源。
- `artifacts/`: 本地构建产物，已被 `.gitignore` 忽略，不提交。

## 成品包必须包含

每次改打包脚本或安装逻辑，都要确认以下文件存在：

```text
Supper IDA MCP Center.app/Contents/MacOS/PluginBundle/ida-plugin/src/supper_ida_plugin/entry.py
Supper IDA MCP Center.app/Contents/MacOS/PluginBundle/ida-plugin/src/supper_ida_plugin/transport/tcp_client.py
Supper IDA MCP Center.app/Contents/MacOS/Bridge/SupperIdaMcp.Center.Bridge
```

Windows portable ZIP 中也必须存在：

```text
PluginBundle/ida-plugin/src/supper_ida_plugin/entry.py
PluginBundle/ida-plugin/src/supper_ida_plugin/transport/tcp_client.py
Bridge/SupperIdaMcp.Center.Bridge.exe
```

如果这些资源缺失，用户只下载成品程序时会出现两个问题：

- Settings 无法安装 IDA 插件。
- Claude Desktop 等 stdio-only MCP 客户端无法自动配置 Bridge。

## 日常开发流程

适用场景：普通功能开发、UI 修复、协议调整、文档更新。

```bash
dotnet build mcp-center/SupperIdaMcpTools.sln -c Release --no-restore
python3 -m compileall -q ida-plugin/src
python3 ida-plugin/install.py --dry-run
```

提交前检查：

```bash
git diff --check
git status --short
```

推到 `main` 后，GitHub 会运行 `.github/workflows/ci.yml`。CI 只做质量验证，不生成正式发布包。

## 预览包流程

适用场景：

- 给自己或测试用户验证安装包。
- 做截图或 UI QA。
- 验证打包脚本、PluginBundle、Bridge。
- 不希望创建 GitHub Release。

操作：

1. 打开 GitHub Actions。
2. 运行 `Preview Build`。
3. 输入版本，例如 `0.1.0-preview.1`。
4. 下载 workflow artifacts。

输出：

- `SupperIdaMcpCenter-<version>-osx-arm64.dmg`
- `SupperIdaMcpCenter-<version>-win-x64.zip`

预览包不创建 Release，不代表正式版本。

## 正式发布流程

适用场景：

- 功能到达稳定节点。
- README、CHANGELOG、安装路径和设置页流程已经核对。
- macOS 和 Windows 基础安装流程已经通过预览包验证。

发布步骤：

```bash
git status --short
git tag v0.1.0
git push origin v0.1.0
```

GitHub 会运行 `.github/workflows/release.yml`：

1. 构建 macOS DMG。
2. 构建 Windows portable ZIP。
3. 上传 artifacts。
4. 创建或更新 draft GitHub Release。

维护者必须人工检查 draft release：

- Release tag 是否正确。
- DMG 和 ZIP 文件名是否正确。
- macOS 包是否能启动。
- Settings 是否能安装或修复 IDA 插件。
- Claude / Codex 配置说明是否仍然正确。
- CHANGELOG 是否覆盖用户可见变化。

检查通过后，再手动 Publish release。

## 本地 macOS 打包

```bash
mcp-center/packaging/macos/package-dmg.sh
```

可指定版本：

```bash
VERSION=0.1.0 mcp-center/packaging/macos/package-dmg.sh
```

验证：

```bash
test -x "artifacts/macos/Supper IDA MCP Center.app/Contents/MacOS/Bridge/SupperIdaMcp.Center.Bridge"
test -f "artifacts/macos/Supper IDA MCP Center.app/Contents/MacOS/PluginBundle/ida-plugin/src/supper_ida_plugin/entry.py"
codesign --verify --deep --strict --verbose=2 "artifacts/macos/Supper IDA MCP Center.app"
codesign --verify --verbose=2 artifacts/macos/SupperIdaMcpCenter-0.1.0-osx-arm64.dmg
```

本地脚本会优先使用 `Developer ID Application`，其次 `Apple Development`，否则 ad-hoc 签名。公开分发需要 Developer ID + notarization。

## Windows 打包

GitHub Windows runner 使用：

```powershell
.\mcp-center\packaging\windows\package-zip.ps1 -Version "0.1.0"
```

本地如果没有 PowerShell，可以至少在 macOS 上 cross-publish 验证资源：

```bash
dotnet publish mcp-center/src/SupperIdaMcp.Center.Desktop/SupperIdaMcp.Center.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o /tmp/supper-win-publish
dotnet publish mcp-center/src/SupperIdaMcp.Center.Bridge/SupperIdaMcp.Center.Bridge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o /tmp/supper-win-bridge
test -f /tmp/supper-win-publish/PluginBundle/ida-plugin/src/supper_ida_plugin/entry.py
test -f /tmp/supper-win-bridge/SupperIdaMcp.Center.Bridge.exe
```

## 版本更新清单

发布前检查并统一版本：

- `mcp-center/src/SupperIdaMcp.Center.Desktop/Setup/ProductInfo.cs`
- `ida-plugin/install.py`
- `ida-plugin/plugin.json`
- `ida-plugin/pyproject.toml`
- `CHANGELOG.md`
- Git tag，例如 `v0.1.0`

如果插件协议不兼容，必须提升 `ProductInfo.PluginVersion` 和 `ida-plugin` 版本，并确保 Settings 能检测旧版本并提示 reinstall。

## CI/CD 工作流职责

- `ci.yml`: PR 和 `main` push。只验证构建、Python 语法、installer dry-run、PluginBundle 输出。
- `preview-build.yml`: 手动触发。生成测试用 DMG 和 ZIP，不创建 Release。
- `release.yml`: tag 或手动指定已有 tag。生成发布产物并创建 draft Release。

不要把正式发布塞进普通 CI。不要让每次 push 都产生 release artifact。

## 常见维护风险

- 忘记把新 Python 文件纳入 Desktop publish。当前 csproj 包含 `ida-plugin/src/**/*.py`，新增非 `.py` 运行资源时要同步更新。
- 只修了仓库源码路径，忘记 packaged build 的 `PluginBundle` 路径。
- Claude 配置仍指向 `dotnet run --project ...`，导致成品用户没有仓库时无法使用 stdio Bridge。
- macOS 修改打包后没有重新 codesign 深度验证。
- 版本号只改了 .NET，没改 IDA 插件 metadata。
- `artifacts/` 本地文件误提交。该目录应保持 ignored。

## 回滚和热修

如果发布后发现安装包问题：

1. 立刻把 GitHub Release 标为 draft 或删除有问题资产。
2. 修复 `main`。
3. 跑 Preview Build 验证。
4. 对同一 tag 重新运行 Release workflow，或发布新的 patch tag。

如果问题影响已安装 IDA 插件：

- 保持 loader 文件名 `supper_ida_mcp_plugin.py` 不变。
- 保持 package 目录 `supper_ida_plugin/` 不变。
- 用 Settings 的 Reinstall 路径覆盖旧包。
- 必要时增加兼容迁移逻辑，不要要求用户手动删除目录。

## 发布前最终检查

```bash
dotnet build mcp-center/SupperIdaMcpTools.sln -c Release --no-restore
python3 -m compileall -q ida-plugin/src
python3 ida-plugin/install.py --dry-run
git diff --check
```

macOS 本地包再检查：

```bash
mcp-center/packaging/macos/package-dmg.sh
test -f "artifacts/macos/Supper IDA MCP Center.app/Contents/MacOS/PluginBundle/ida-plugin/src/supper_ida_plugin/entry.py"
test -x "artifacts/macos/Supper IDA MCP Center.app/Contents/MacOS/Bridge/SupperIdaMcp.Center.Bridge"
codesign --verify --deep --strict --verbose=2 "artifacts/macos/Supper IDA MCP Center.app"
codesign --verify --verbose=2 artifacts/macos/SupperIdaMcpCenter-0.1.0-osx-arm64.dmg
```

如果以上任一项失败，不发布。
