#!/usr/bin/env bash
set -e

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"
BRANDING_DIR="$REPO_ROOT/tools/branding"
VERSION="3.0.0-alpha"
VOL_NAME="Spotnet 3.0 Alpha"

FORMAT="${1:-all}"
TARGET_ARCH="${2:-auto}"

# Parse architecture
if [ "$TARGET_ARCH" = "auto" ]; then
    HOST_ARCH="$(uname -m)"
    if [ "$HOST_ARCH" = "x86_64" ]; then
        ARCH_TAG="osx-x64"
        SHORT_ARCH="x64"
    else
        ARCH_TAG="osx-arm64"
        SHORT_ARCH="arm64"
    fi
elif [ "$TARGET_ARCH" = "x64" ] || [ "$TARGET_ARCH" = "intel" ]; then
    ARCH_TAG="osx-x64"
    SHORT_ARCH="x64"
elif [ "$TARGET_ARCH" = "arm64" ] || [ "$TARGET_ARCH" = "apple-silicon" ]; then
    ARCH_TAG="osx-arm64"
    SHORT_ARCH="arm64"
else
    echo "Unknown architecture: $TARGET_ARCH. Use 'x64', 'arm64', or 'auto'."
    exit 1
fi

echo "========================================================="
echo "  Spotnet 3.0 (macOS Alpha) Installer Generator"
echo "  Target Architecture: $ARCH_TAG ($SHORT_ARCH)"
echo "  Output Format:       $FORMAT"
echo "========================================================="

# 1. Build .app bundle
echo ""
echo "[1/4] Building macOS App Bundle..."
"$REPO_ROOT/tools/make_app_bundle.sh" "$SHORT_ARCH"

APP_BUNDLE="$ARTIFACTS_DIR/Spotnet.app"
if [ ! -d "$APP_BUNDLE" ]; then
    echo "Error: App bundle was not created at $APP_BUNDLE"
    exit 1
fi

DMG_FILE="$ARTIFACTS_DIR/Spotnet-3.0.0-Alpha-$SHORT_ARCH.dmg"
PKG_FILE="$ARTIFACTS_DIR/Spotnet-3.0.0-Alpha-$SHORT_ARCH.pkg"
DMG_SYMLINK="$ARTIFACTS_DIR/Spotnet-3.0.0-Alpha.dmg"
PKG_SYMLINK="$ARTIFACTS_DIR/Spotnet-3.0.0-Alpha.pkg"

# 2. Build DMG
if [ "$FORMAT" = "dmg" ] || [ "$FORMAT" = "all" ]; then
    echo ""
    echo "[2/4] Building DMG Installer ($DMG_FILE)..."
    rm -f "$DMG_FILE" "$DMG_SYMLINK"

    STAGING_DIR="/tmp/spotnet_dmg_staging_$$"
    rm -rf "$STAGING_DIR"
    mkdir -p "$STAGING_DIR"

    # Copy .app bundle into staging
    cp -R "$APP_BUNDLE" "$STAGING_DIR/Spotnet.app"

    # Create symlink to /Applications
    ln -s /Applications "$STAGING_DIR/Applications"

    # Copy background if available
    if [ -f "$BRANDING_DIR/dmg_background.tiff" ]; then
        mkdir -p "$STAGING_DIR/.background"
        cp "$BRANDING_DIR/dmg_background.tiff" "$STAGING_DIR/.background/background.tiff"
    fi

    # Create compressed DMG image
    hdiutil create \
        -volname "$VOL_NAME" \
        -srcfolder "$STAGING_DIR" \
        -ov \
        -format UDZO \
        -imagekey zlib-level=9 \
        "$DMG_FILE"

    rm -rf "$STAGING_DIR"

    # Ad-hoc sign DMG
    echo "Ad-hoc signing DMG..."
    codesign --force -s - "$DMG_FILE" 2>/dev/null || true

    # Create convenience symlink
    ln -sf "$(basename "$DMG_FILE")" "$DMG_SYMLINK"
    echo "✓ Created DMG: $DMG_FILE ($(ls -lh "$DMG_FILE" | awk '{print $5}'))"
fi

# 3. Build PKG
if [ "$FORMAT" = "pkg" ] || [ "$FORMAT" = "all" ]; then
    echo ""
    echo "[3/4] Building PKG Installer ($PKG_FILE)..."
    rm -f "$PKG_FILE" "$PKG_SYMLINK"

    TEMP_COMPONENT_PKG="/tmp/spotnet_component_$$.pkg"
    rm -f "$TEMP_COMPONENT_PKG"

    # Build component package installing to /Applications
    pkgbuild \
        --component "$APP_BUNDLE" \
        --install-location "/Applications" \
        --identifier "nl.spotnet.desktop" \
        --version "$VERSION" \
        "$TEMP_COMPONENT_PKG"

    # Synthesize distribution package with productbuild
    productbuild \
        --package "$TEMP_COMPONENT_PKG" \
        "$PKG_FILE"

    rm -f "$TEMP_COMPONENT_PKG"

    # Create convenience symlink
    ln -sf "$(basename "$PKG_FILE")" "$PKG_SYMLINK"
    echo "✓ Created PKG: $PKG_FILE ($(ls -lh "$PKG_FILE" | awk '{print $5}'))"
fi

# 4. Checksums
echo ""
echo "[4/4] Computing SHA256 Checksums..."
cd "$ARTIFACTS_DIR"
SUMS_FILE="$ARTIFACTS_DIR/SHA256SUMS.txt"
rm -f "$SUMS_FILE"
for f in Spotnet-3.0.0-Alpha*.dmg Spotnet-3.0.0-Alpha*.pkg; do
    if [ -f "$f" ] && [ ! -L "$f" ]; then
        shasum -a 256 "$f" >> "$SUMS_FILE"
    fi
done
cat "$SUMS_FILE"

echo ""
echo "========================================================="
echo "  Build Completed Successfully!"
echo "  Artifacts directory: $ARTIFACTS_DIR"
echo "========================================================="
ls -lh "$ARTIFACTS_DIR"/Spotnet-3.0.0-Alpha*
