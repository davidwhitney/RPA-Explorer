#!/usr/bin/env bash
#
# Builds distributable RPA Explorer binaries for all supported platforms into ./dist.
#
# Every target is cross-compiled from a single machine, so this runs identically on a
# developer laptop and on CI. Builds are self-contained by default: the .NET runtime is
# bundled, so users do not need to install anything.
#
# Usage:
#   ./build.sh                                  # all platforms, version from git
#   ./build.sh --version 1.2.3                  # explicit version
#   ./build.sh --rids "osx-arm64 win-x64"       # subset of platforms
#   ./build.sh --framework-dependent            # smaller, requires .NET on the target
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT/RPA Explorer/RPA Explorer.csproj"
DIST="$ROOT/dist"
STAGE="$ROOT/build"

ALL_RIDS="osx-arm64 osx-x64 win-x64 linux-x64 linux-arm64"
RIDS="$ALL_RIDS"
VERSION=""
SELF_CONTAINED="true"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version) VERSION="$2"; shift 2 ;;
        --rids) RIDS="$2"; shift 2 ;;
        --framework-dependent) SELF_CONTAINED="false"; shift ;;
        -h|--help) sed -n '2,20p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Unknown option: $1" >&2; exit 2 ;;
    esac
done

# Version: explicit flag, else the current git tag, else a dev version.
if [[ -z "$VERSION" ]]; then
    if git -C "$ROOT" describe --tags --exact-match >/dev/null 2>&1; then
        VERSION="$(git -C "$ROOT" describe --tags --exact-match)"
    elif git -C "$ROOT" describe --tags --abbrev=0 >/dev/null 2>&1; then
        VERSION="$(git -C "$ROOT" describe --tags --abbrev=0)-dev"
    else
        VERSION="0.0.1-dev"
    fi
fi
# Strip a leading "v" so the value is a valid assembly version prefix.
NUMERIC_VERSION="$(printf '%s' "${VERSION#v}" | sed 's/[^0-9.].*$//')"
[[ -z "$NUMERIC_VERSION" ]] && NUMERIC_VERSION="0.0.1"

echo "==> RPA Explorer build"
echo "    version        : $VERSION (assembly $NUMERIC_VERSION)"
echo "    self-contained : $SELF_CONTAINED"
echo "    platforms      : $RIDS"
echo

rm -rf "$DIST" "$STAGE"
mkdir -p "$DIST"

# Writes the .app bundle macOS users expect, instead of a bare folder of files.
make_macos_bundle() {
    local publish_dir="$1" bundle_dir="$2"
    mkdir -p "$bundle_dir/Contents/MacOS" "$bundle_dir/Contents/Resources"
    cp -R "$publish_dir/." "$bundle_dir/Contents/MacOS/"

    # Convert the Windows icon if the macOS tooling is available; purely cosmetic.
    local icon_src="$ROOT/RPA Explorer/Assets/RPA Explorer.ico"
    local icon_entry=""
    if command -v sips >/dev/null 2>&1 && [[ -f "$icon_src" ]]; then
        if sips -s format icns "$icon_src" --out "$bundle_dir/Contents/Resources/AppIcon.icns" >/dev/null 2>&1; then
            icon_entry="
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>"
        fi
    fi

    cat > "$bundle_dir/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>RPA Explorer</string>
    <key>CFBundleDisplayName</key>
    <string>RPA Explorer</string>
    <key>CFBundleIdentifier</key>
    <string>com.universedevel.rpaexplorer</string>
    <key>CFBundleVersion</key>
    <string>$NUMERIC_VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$NUMERIC_VERSION</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>RPA_Explorer</string>$icon_entry
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key>
            <string>RenPy Archive</string>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>rpa</string>
                <string>rpi</string>
            </array>
        </dict>
    </array>
</dict>
</plist>
PLIST
    chmod +x "$bundle_dir/Contents/MacOS/RPA_Explorer" 2>/dev/null || true
}

for rid in $RIDS; do
    echo "==> Publishing $rid"
    publish_dir="$STAGE/$rid"

    dotnet publish "$PROJECT" \
        --configuration Release \
        --runtime "$rid" \
        --self-contained "$SELF_CONTAINED" \
        --output "$publish_dir" \
        -p:Version="$NUMERIC_VERSION" \
        -p:InformationalVersion="$VERSION" \
        -p:DebugType=none \
        -p:PublishTrimmed=false \
        --nologo \
        --verbosity quiet

    # Debug symbols are not useful in a release download.
    rm -f "$publish_dir"/*.pdb

    name="RPA-Explorer-$VERSION-$rid"
    pack_dir="$STAGE/pack/$rid"
    mkdir -p "$pack_dir"

    case "$rid" in
        osx-*)
            make_macos_bundle "$publish_dir" "$pack_dir/RPA Explorer.app"
            cp "$ROOT/README.md" "$ROOT/LICENSE" "$pack_dir/" 2>/dev/null || true
            ( cd "$pack_dir" && zip -qry "$DIST/$name.zip" . )
            ;;
        win-*)
            # VideoLAN.LibVLC.Windows ships win-x86 and win-x64 natives side by side
            # (~200MB combined). A process can only ever load one of them, so drop the
            # architecture this build cannot use. There is no arm64 native build, and an
            # arm64 process cannot load x64 DLLs, so the arm64 download carries none -
            # run the x64 build under emulation if you need media preview on Windows ARM.
            case "$rid" in
                win-x86) keep_arch="win-x86" ;;
                win-x64) keep_arch="win-x64" ;;
                *)       keep_arch="" ;;
            esac
            if [[ -d "$publish_dir/libvlc" ]]; then
                for archdir in "$publish_dir"/libvlc/*; do
                    [[ -d "$archdir" ]] || continue
                    [[ "$(basename "$archdir")" == "$keep_arch" ]] || rm -rf "$archdir"
                done
                rmdir "$publish_dir/libvlc" 2>/dev/null || true
            fi
            cp -R "$publish_dir/." "$pack_dir/"
            cp "$ROOT/README.md" "$ROOT/LICENSE" "$pack_dir/" 2>/dev/null || true
            ( cd "$pack_dir" && zip -qry "$DIST/$name.zip" . )

            # Secondary, much smaller download without the bundled VLC natives, for users
            # who already have VLC installed. The app falls back to a system-wide install
            # on Windows, and prompts with a download link when there is not one.
            if [[ -d "$pack_dir/libvlc" ]]; then
                lite_dir="$STAGE/pack/$rid-novlc"
                mkdir -p "$lite_dir"
                cp -R "$pack_dir/." "$lite_dir/"
                rm -rf "$lite_dir/libvlc"
                ( cd "$lite_dir" && zip -qry "$DIST/$name-novlc.zip" . )
            fi
            ;;
        linux-*)
            cp -R "$publish_dir/." "$pack_dir/"
            cp "$ROOT/README.md" "$ROOT/LICENSE" "$pack_dir/" 2>/dev/null || true
            chmod +x "$pack_dir/RPA_Explorer" 2>/dev/null || true
            ( cd "$pack_dir" && tar -czf "$DIST/$name.tar.gz" . )
            ;;
    esac
done

# Checksums so downloads can be verified. Globbing only the archives keeps the
# checksum file itself out of the list.
( cd "$DIST" && for f in *.zip *.tar.gz; do
    [[ -e "$f" ]] || continue
    if command -v shasum >/dev/null 2>&1; then shasum -a 256 "$f"; else sha256sum "$f"; fi
  done > SHA256SUMS ) || true

echo
echo "==> Artifacts in ./dist"
ls -lh "$DIST"
