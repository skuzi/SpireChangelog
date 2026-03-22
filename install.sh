set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
MOD_ID="SpireChangelog"

GAME_DIR="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS"
MODS_DIR="$GAME_DIR/mods/$MOD_ID"

echo "Building $MOD_ID..."
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"
cd "$SCRIPT_DIR"
dotnet build

echo ""
echo "Installing to: $MODS_DIR"
mkdir -p "$MODS_DIR"

cp "$SCRIPT_DIR/build/$MOD_ID.dll" "$MODS_DIR/$MOD_ID.dll"

cp "$SCRIPT_DIR/mod_manifest.json" "$MODS_DIR/$MOD_ID.json"

echo ""
echo "Installed! Files:"
ls -la "$MODS_DIR"
echo ""
echo "To debug, add this Steam launch parameter:"
echo "  --remote-debug tcp://127.0.0.1:6007"
