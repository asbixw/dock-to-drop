#!/bin/bash
set -e
ROOT="$(cd "$(dirname "$0")" && pwd)"
UNITY="$ROOT/tools/Unity/Unity.app/Contents/MacOS/Unity"
PROJ="$ROOT/unity-project"
mkdir -p "$ROOT/logs"
"$UNITY" -batchmode -quit -projectPath "$PROJ" -executeMethod ProjectBuilder.BuildWebGL -logFile "$ROOT/logs/build-webgl.log"
echo "WebGL build done: $PROJ/Build/WebGL"
