#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

APP_NAME="Supper IDA MCP Center"
EXECUTABLE_NAME="SupperIdaMcp.Center.Desktop"
BUNDLE_ID="com.thepeppy.supper-ida-mcp-center"
VERSION="${VERSION:-0.1.0}"
RID="${RID:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
PROJECT="$REPO_ROOT/mcp-center/src/SupperIdaMcp.Center.Desktop/SupperIdaMcp.Center.Desktop.csproj"
ARTIFACT_ROOT="${ARTIFACT_ROOT:-$REPO_ROOT/artifacts/macos}"
PUBLISH_DIR="$ARTIFACT_ROOT/publish/$RID"
APP_DIR="$ARTIFACT_ROOT/$APP_NAME.app"
DMG_ROOT="$ARTIFACT_ROOT/dmg-root"
DMG_PATH="$ARTIFACT_ROOT/SupperIdaMcpCenter-$VERSION-$RID.dmg"
INFO_PLIST_TEMPLATE="$SCRIPT_DIR/Info.plist.template"
ENTITLEMENTS="$SCRIPT_DIR/SupperIdaMcp.entitlements"
ICON_SOURCE="$REPO_ROOT/mcp-center/src/SupperIdaMcp.Center.Desktop/Assets/AppIcon/app-icon.icns"

select_signing_identity() {
  if [[ -n "${MACOS_CODESIGN_IDENTITY:-}" ]]; then
    printf '%s\n' "$MACOS_CODESIGN_IDENTITY"
    return
  fi

  local developer_id
  developer_id="$(security find-identity -v -p codesigning 2>/dev/null | sed -n 's/.*"\(Developer ID Application:[^"]*\)".*/\1/p' | head -n 1)"
  if [[ -n "$developer_id" ]]; then
    printf '%s\n' "$developer_id"
    return
  fi

  local apple_development
  apple_development="$(security find-identity -v -p codesigning 2>/dev/null | sed -n 's/.*"\(Apple Development:[^"]*\)".*/\1/p' | head -n 1)"
  if [[ -n "$apple_development" ]]; then
    printf '%s\n' "$apple_development"
    return
  fi

  printf '%s\n' "-"
}

sign_file() {
  local path="$1"
  local identity="$2"
  codesign --force --timestamp=none --options runtime --entitlements "$ENTITLEMENTS" --sign "$identity" "$path"
}

sign_app_payload_files() {
  local identity="$1"
  local main_executable="$APP_DIR/Contents/MacOS/$EXECUTABLE_NAME"

  while IFS= read -r -d '' file_path; do
    if [[ "$file_path" == "$main_executable" ]]; then
      continue
    fi

    sign_file "$file_path" "$identity"
  done < <(find "$APP_DIR/Contents/MacOS" -type f -print0)

  sign_file "$main_executable" "$identity"
}

echo "[1/7] Cleaning packaging directories"
rm -rf "$PUBLISH_DIR" "$APP_DIR" "$DMG_ROOT" "$DMG_PATH"
mkdir -p "$PUBLISH_DIR" "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources" "$DMG_ROOT"

echo "[2/7] Publishing $RID $CONFIGURATION"
dotnet publish "$PROJECT" \
  -c "$CONFIGURATION" \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  -o "$PUBLISH_DIR"

echo "[3/7] Creating .app bundle"
ditto "$PUBLISH_DIR" "$APP_DIR/Contents/MacOS"
cp "$ICON_SOURCE" "$APP_DIR/Contents/Resources/app-icon.icns"
sed \
  -e "s/__VERSION__/$VERSION/g" \
  -e "s/__BUNDLE_ID__/$BUNDLE_ID/g" \
  "$INFO_PLIST_TEMPLATE" > "$APP_DIR/Contents/Info.plist"
chmod +x "$APP_DIR/Contents/MacOS/$EXECUTABLE_NAME"

SIGNING_IDENTITY="$(select_signing_identity)"
echo "[4/7] Signing with identity: $SIGNING_IDENTITY"
sign_app_payload_files "$SIGNING_IDENTITY"
codesign --force --timestamp=none --options runtime --entitlements "$ENTITLEMENTS" --sign "$SIGNING_IDENTITY" "$APP_DIR"
codesign --verify --deep --strict --verbose=2 "$APP_DIR"

echo "[5/7] Creating DMG staging"
cp -R "$APP_DIR" "$DMG_ROOT/"
ln -s /Applications "$DMG_ROOT/Applications"

echo "[6/7] Building DMG"
hdiutil create \
  -volname "$APP_NAME" \
  -srcfolder "$DMG_ROOT" \
  -ov \
  -format UDZO \
  "$DMG_PATH"

echo "[7/7] Signing DMG"
codesign --force --timestamp=none --sign "$SIGNING_IDENTITY" "$DMG_PATH"
codesign --verify --verbose=2 "$DMG_PATH"

echo "DMG: $DMG_PATH"
