#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
MOD_ID="SpireChangelog"

# Read version from mod_manifest.json
VERSION=$(grep '"version"' "$SCRIPT_DIR/mod_manifest.json" | sed 's/.*: *"\(.*\)".*/\1/')
TAG="v$VERSION"
ARCHIVE="$MOD_ID-$TAG.zip"

echo "Building $MOD_ID $TAG..."
export DOTNET_ROOT="${DOTNET_ROOT:-/opt/homebrew/opt/dotnet/libexec}"
cd "$SCRIPT_DIR"
dotnet build

# Package as SpireChangelog/ folder inside zip
STAGING=$(mktemp -d)
mkdir "$STAGING/$MOD_ID"
cp "build/$MOD_ID.dll" "$STAGING/$MOD_ID/$MOD_ID.dll"
cp "mod_manifest.json" "$STAGING/$MOD_ID/$MOD_ID.json"

(cd "$STAGING" && zip -r "$SCRIPT_DIR/$ARCHIVE" "$MOD_ID/")
rm -rf "$STAGING"

echo ""
echo "Created $ARCHIVE"
echo ""

# Create GitHub release
if command -v gh &> /dev/null; then
    echo "Creating GitHub release $TAG..."
    NOTES=$(cat <<NOTES
## Install
1. In Steam, right-click **Slay the Spire 2** → **Manage** → **Browse Local Files**
2. Extract the zip and copy the \`SpireChangelog/\` folder into \`mods/\` (create it if needed)
3. Launch the game

$(gh api repos/{owner}/{repo}/releases/generate-notes -f tag_name="$TAG" -q .body 2>/dev/null || echo "")
NOTES
)
    gh release create "$TAG" "$ARCHIVE" --title "$TAG" --notes "$NOTES"
    rm "$ARCHIVE"
    echo "Done! https://github.com/$(gh repo view --json nameWithOwner -q .nameWithOwner)/releases/tag/$TAG"
else
    echo "gh CLI not found — upload $ARCHIVE manually to GitHub releases."
fi
