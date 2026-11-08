#!/bin/bash
set -e
ROOT="$(cd "$(dirname "$0")" && pwd)"
UNITY="$ROOT/tools/Unity/Unity.app/Contents/MacOS/Unity"
PROJ="$ROOT/unity-project"
if [ ! -x "$UNITY" ]; then echo "Unity editor not installed yet: $UNITY"; exit 1; fi
mkdir -p "$ROOT/logs"
if [ ! -d "$PROJ/Assets" ]; then
  "$UNITY" -batchmode -quit -createProject "$PROJ" -logFile "$ROOT/logs/create.log" || true
fi
mkdir -p "$PROJ/Assets/Scripts" "$PROJ/Assets/Editor" "$PROJ/Assets/Scenes" \
         "$PROJ/Assets/Resources/Models" "$PROJ/Assets/Resources/Voice" \
         "$PROJ/Assets/Resources/Audio" "$PROJ/Assets/Resources/Textures" \
         "$PROJ/Assets/Resources/Shaders"
cp -R "$ROOT/unity-assets/Scripts/." "$PROJ/Assets/Scripts/"
cp "$ROOT/unity-assets/Editor/ProjectBuilder.cs" "$PROJ/Assets/Editor/"
cp "$ROOT/unity-assets/Shaders/"*.shader "$PROJ/Assets/Resources/Shaders/"
cp "$ROOT/unity-assets/Models/"*.fbx "$PROJ/Assets/Resources/Models/"
mkdir -p "$PROJ/Assets/Resources/Models/Props" "$PROJ/Assets/Resources/Textures/Props"
cp "$ROOT/unity-assets/Models/Props/"*.fbx "$PROJ/Assets/Resources/Models/Props/"
cp "$ROOT/unity-assets/Textures/Props/"* "$PROJ/Assets/Resources/Textures/Props/"
cp "$ROOT/voice/"*.mp3 "$PROJ/Assets/Resources/Voice/"
cp "$ROOT/unity-assets/Audio/"*.wav "$PROJ/Assets/Resources/Audio/"
cp "$ROOT/unity-assets/Textures/"*.hdr "$PROJ/Assets/Resources/Textures/"
cp "$ROOT/unity-assets/Textures/"*.jpg "$PROJ/Assets/Resources/Textures/"
cp "$ROOT/unity-assets/Textures/"*.png "$PROJ/Assets/Resources/Textures/"
echo "Assets copied."
"$UNITY" -batchmode -quit -projectPath "$PROJ" -executeMethod ProjectBuilder.PrepareScene -logFile "$ROOT/logs/prepare.log"
echo "Scene prepared."
