#!/usr/bin/env bash
set -euo pipefail

project_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
package_dir="$project_dir/Packages/com.beetrootlab.nodin"
editor_version=$(awk '$1 == "m_EditorVersion:" { print $2 }' "$project_dir/ProjectSettings/ProjectVersion.txt")
editor_path=$(unity editors path "$editor_version" --format tsv | awk -F '\t' '$1 == "path" { print $2 }')
scripting_dir="$editor_path/Editor/Data"
if [[ -d "$editor_path/Unity.app" ]]; then
    scripting_dir="$editor_path/Unity.app/Contents/Resources"
fi
scripting_dir="$scripting_dir/Scripting"

build_dir=$(mktemp -d)
trap 'rm -rf -- "$build_dir"' EXIT
"$scripting_dir/NetCoreRuntime/dotnet" "$scripting_dir/DotNetSdkRoslyn/csc.dll" \
    -nologo -noconfig -nostdlib -target:library -optimize+ -deterministic \
    "-reference:$scripting_dir/NetStandard/ref/2.1.0/netstandard.dll" \
    "-reference:$scripting_dir/Managed/UnityEngine/UnityEngine.CoreModule.dll" \
    "-out:$build_dir/Nodin.dll" "$package_dir/Source~/InspectorAttributes.cs"

if ! cmp -s "$build_dir/Nodin.dll" "$package_dir/Runtime/Nodin.dll"; then
    cp "$build_dir/Nodin.dll" "$package_dir/Runtime/Nodin.dll"
fi
