#!/bin/bash
set -e
ROOT="$(cd "$(dirname "$0")" && pwd)"
UNITY="$ROOT/tools/Unity/Unity.app/Contents/MacOS/Unity"
PROJ="$ROOT/unity-project"
mkdir -p "$ROOT/logs"
"$UNITY" -batchmode -quit -projectPath "$PROJ" -executeMethod ProjectBuilder.BuildMac -logFile "$ROOT/logs/build-mac.log"
echo "Mac build done: $PROJ/Build/Mac/DockToDrop.app"
