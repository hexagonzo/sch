#!/usr/bin/env bash
set -euo pipefail
: "${STS2_DATA_DIR:?Set STS2_DATA_DIR to the folder containing sts2.dll}"
dotnet build "$(dirname "$0")/../sch.csproj" -c Release -p:GameDataDir="$STS2_DATA_DIR"
echo 'Compiled. Copy bin/Release/net9.0/sch.dll and mod_manifest.json into mods/sch/'
