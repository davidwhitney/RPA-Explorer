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
PROJECT="$ROOT/src/RpaExplorer/RpaExplorer.csproj"
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
#
# MinVer derives the version the assembly reports (see Directory.Build.props). It is only
# overridden when the two must agree exactly - a release, or an explicit --version - so an
# untagged development build keeps MinVer's more precise "next patch, N commits on"
# rather than being pinned to a guess made here.
PIN_ASSEMBLY_VERSION="true"

if [[ -z "$VERSION" ]]; then
    if git -C "$ROOT" describe --tags --exact-match >/dev/null 2>&1; then
        VERSION="$(git -C "$ROOT" describe --tags --exact-match)"
    elif git -C "$ROOT" describe --tags >/dev/null 2>&1; then
        VERSION="$(git -C "$ROOT" describe --tags)"
        PIN_ASSEMBLY_VERSION="false"
    else
        VERSION="0.0.1-dev"
        PIN_ASSEMBLY_VERSION="false"
    fi
fi
# MinVer wants a bare SemVer, so drop any leading "v" from the tag.
SEMVER="${VERSION#v}"
VERSION_ARGS=()
if [[ "$PIN_ASSEMBLY_VERSION" == "true" ]]; then
    VERSION_ARGS+=(-p:MinVerVersionOverride="$SEMVER")
fi
# The macOS Info.plist wants strictly numeric fields, with no pre-release suffix.
NUMERIC_VERSION="$(printf '%s' "$SEMVER" | sed 's/[^0-9.].*$//')"
[[ -z "$NUMERIC_VERSION" ]] && NUMERIC_VERSION="0.0.1"

echo "==> RPA Explorer build"
if [[ "$PIN_ASSEMBLY_VERSION" == "true" ]]; then
    echo "    version        : $VERSION (assembly $SEMVER)"
else
    echo "    version        : $VERSION (assembly version left to MinVer)"
fi
echo "    self-contained : $SELF_CONTAINED"
echo "    platforms      : $RIDS"
echo

rm -rf "$DIST" "$STAGE"
mkdir -p "$DIST"

# Set when a macOS bundle could not be signed; fails the build at the end.
MACOS_UNSIGNED="false"

# Writes the .app bundle macOS users expect, instead of a bare folder of files.
make_macos_bundle() {
    local publish_dir="$1" bundle_dir="$2"
    mkdir -p "$bundle_dir/Contents/MacOS" "$bundle_dir/Contents/Resources"
    cp -R "$publish_dir/." "$bundle_dir/Contents/MacOS/"

    # Convert the Windows icon if the macOS tooling is available; purely cosmetic.
    local icon_src="$ROOT/src/RpaExplorer/Assets/RpaExplorer.ico"
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
    <string>RpaExplorer</string>$icon_entry
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
    chmod +x "$bundle_dir/Contents/MacOS/RpaExplorer" 2>/dev/null || true

    # The SDK ad-hoc signs the bare executable, but that signature seals no bundle: once the
    # binary is inside a .app with an Info.plist and resources, macOS sees a signature that
    # promises resources it cannot find and refuses to launch it as "damaged". Signing the
    # assembled bundle seals what is actually there.
    #
    # Ad-hoc (-) rather than a Developer ID: it is enough to launch, though without
    # notarization the first run still needs right-click -> Open. codesign is macOS-only, so
    # a bundle built anywhere else cannot be made to work and says so rather than shipping
    # something that fails on the user's machine.
    if command -v codesign >/dev/null 2>&1; then
        codesign --force --deep --sign - --timestamp=none "$bundle_dir"
        echo "    signed: $(basename "$bundle_dir")"
    else
        echo "    WARNING: codesign unavailable - $(basename "$bundle_dir") will be rejected" >&2
        echo "             by macOS as damaged. Build macOS targets on macOS." >&2
        MACOS_UNSIGNED="true"
    fi
}

# Unpacks the archive exactly as a user would and checks the signature survived, because
# that is the state that decides whether macOS calls the download damaged.
verify_macos_archive() {
    local archive="$1"
    command -v codesign >/dev/null 2>&1 || return 0

    local check_dir="$STAGE/verify/$(basename "$archive")"
    rm -rf "$check_dir"
    mkdir -p "$check_dir"

    if command -v ditto >/dev/null 2>&1; then
        ditto -x -k "$archive" "$check_dir"
    else
        unzip -q "$archive" -d "$check_dir"
    fi

    if codesign --verify --deep --strict "$check_dir/RPA Explorer.app" 2>/dev/null; then
        echo "    signature verified after archiving"
    else
        echo "ERROR: $(basename "$archive") loses its signature when unpacked;" >&2
        echo "       macOS will refuse to open it as damaged." >&2
        exit 1
    fi

    rm -rf "$check_dir"
}

for rid in $RIDS; do
    echo "==> Publishing $rid"
    publish_dir="$STAGE/$rid"

    dotnet publish "$PROJECT" \
        --configuration Release \
        --runtime "$rid" \
        --self-contained "$SELF_CONTAINED" \
        --output "$publish_dir" \
        ${VERSION_ARGS[@]+"${VERSION_ARGS[@]}"} \
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

            # ditto, not zip: a .app's signature depends on metadata that plain zip drops,
            # so a zipped bundle verifies fine before archiving and fails once unpacked.
            # This is also what Finder uses, so it is what the user's download goes through.
            if command -v ditto >/dev/null 2>&1; then
                ditto -c -k --sequesterRsrc "$pack_dir" "$DIST/$name.zip"
            else
                ( cd "$pack_dir" && zip -qry "$DIST/$name.zip" . )
            fi

            verify_macos_archive "$DIST/$name.zip"
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
            chmod +x "$pack_dir/RpaExplorer" 2>/dev/null || true
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

if [[ "$MACOS_UNSIGNED" == "true" ]]; then
    echo
    echo "ERROR: macOS bundles were produced without a signature and will not launch." >&2
    exit 1
fi
