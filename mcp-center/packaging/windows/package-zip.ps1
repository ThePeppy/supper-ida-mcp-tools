param(
    [string] $Version = $(if ($env:VERSION) { $env:VERSION } else { "0.1.0" }),
    [string] $RuntimeIdentifier = $(if ($env:RID) { $env:RID } else { "win-x64" }),
    [string] $Configuration = $(if ($env:CONFIGURATION) { $env:CONFIGURATION } else { "Release" })
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptDir "../../..")
$Project = Join-Path $RepoRoot "mcp-center/src/SupperIdaMcp.Center.Desktop/SupperIdaMcp.Center.Desktop.csproj"
$BridgeProject = Join-Path $RepoRoot "mcp-center/src/SupperIdaMcp.Center.Bridge/SupperIdaMcp.Center.Bridge.csproj"
$ArtifactRoot = if ($env:ARTIFACT_ROOT) { $env:ARTIFACT_ROOT } else { Join-Path $RepoRoot "artifacts/windows" }
$PublishDir = Join-Path $ArtifactRoot "publish/$RuntimeIdentifier"
$BridgePublishDir = Join-Path $ArtifactRoot "publish/$RuntimeIdentifier-bridge"
$BundleDir = Join-Path $ArtifactRoot "SupperIdaMcpCenter-$Version-$RuntimeIdentifier"
$ZipPath = Join-Path $ArtifactRoot "SupperIdaMcpCenter-$Version-$RuntimeIdentifier.zip"

Write-Host "[1/5] Cleaning packaging directories"
Remove-Item -Recurse -Force $PublishDir, $BridgePublishDir, $BundleDir, $ZipPath -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $PublishDir, $BridgePublishDir, $BundleDir | Out-Null

Write-Host "[2/5] Publishing desktop $RuntimeIdentifier $Configuration"
dotnet publish $Project `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $PublishDir

Write-Host "[3/5] Publishing stdio bridge"
dotnet publish $BridgeProject `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $BridgePublishDir

Write-Host "[4/5] Creating portable bundle"
Copy-Item -Recurse -Force (Join-Path $PublishDir "*") $BundleDir
New-Item -ItemType Directory -Force (Join-Path $BundleDir "Bridge") | Out-Null
Copy-Item -Recurse -Force (Join-Path $BridgePublishDir "*") (Join-Path $BundleDir "Bridge")

Write-Host "[5/5] Creating ZIP"
Compress-Archive -Path (Join-Path $BundleDir "*") -DestinationPath $ZipPath -Force
Write-Host "ZIP: $ZipPath"
