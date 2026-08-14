#!/bin/bash
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
WORKSPACE="$(cd "$SCRIPT_DIR/.." && pwd)"
LUBAN_DLL="$WORKSPACE/Tools/Luban/Luban.dll"
CONF_ROOT="$SCRIPT_DIR"

cd "$CONF_ROOT" || exit 1
dotnet "$LUBAN_DLL" -t client -c cs-simple-json -d json --conf "$CONF_ROOT/luban.conf"
echo "[JojoP] OK code -> Assets/Script/LubanCode/Gen"
echo "[JojoP] OK data -> Assets/Bundle/LubanConfig"
echo "bye~"
