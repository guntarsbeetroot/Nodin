# Nodin

Nodin is a Unity 6.3 package for building IMGUI inspectors and Editor windows from C# attributes. Add attributes to the fields and methods you already have to organize components, surface validation feedback, and put common actions in the Inspector.

## What you can build

- Group fields into boxes, foldouts, tabs, rows, and columns. Show or disable fields based on other values.
- Add validation, dropdowns, asset previews, searchable lists, editable tables, and buttons that invoke methods.
- Draw Nodin controls in custom inspectors and Editor windows. Register custom attribute drawers and insert panels alongside existing fields.

Nodin uses Unity serialization for serialized fields, preserving Undo, prefab overrides, and multi-object editing for compatible fields. Attribute annotations are omitted during player compilation, and Nodin assemblies are excluded from player builds.

## Install

In Unity 6.3, open **Window → Package Manager**, choose **Install package from git URL** from the **+** menu, and enter the URL below. See Unity's [Git package installation guide](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/managing-packages-window/upm-ui-actions/upm-ui-giturl).

```text
https://github.com/guntarsbeetroot/Nodin.git?path=/Packages/com.beetrootlab.nodin
```

For a local checkout, choose **Install package from disk** and select [the package manifest](Packages/com.beetrootlab.nodin/package.json).

## Quick start

Save this script as `NodinBuildExample.cs`, attach it to a GameObject, and select the object. The Inspector shows a **Movement** box with a speed field labeled in **m/s** and a **Reset speed** button.

```csharp
using Nodin;
using UnityEngine;

public sealed class NodinBuildExample : MonoBehaviour
{
    [SerializeField, BoxGroup("Movement"), SuffixLabel("m/s")]
    private float speed = 1;

    public float Speed => speed;

    [Button("Reset speed", ButtonSizes.Medium)]
    private void ResetSpeed() => speed = 1;
}
```

Nodin applies automatically in fallback inspectors for `MonoBehaviour` and `ScriptableObject`. For an existing custom inspector or Editor window, see the [package guide](Packages/com.beetrootlab.nodin/README.md).

## Develop and test

Open the repository root in Unity **6000.3.20f1**. The project loads Nodin as an embedded package, and the default standalone build uses an empty scene to verify player isolation.

Track user-facing changes under **Unreleased** in the [package changelog](Packages/com.beetrootlab.nodin/CHANGELOG.md). When releasing, group those changes under the version in `package.json`.

After changing [InspectorAttributes.cs](Packages/com.beetrootlab.nodin/Source~/InspectorAttributes.cs), run `bash Tools/build-nodin-attributes.sh` from the repository root. Commit the rebuilt `Runtime/Nodin.dll` alongside the source. The script uses the compiler bundled with the project's Unity version.

To run the tests, open **Window → General → Test Runner → EditMode**, or run these Unity CLI commands from the repository root with the Editor open. The CLI command also runs the explicit player-isolation test.

```sh
unity status --project-path "$PWD" --format json
unity command --project-path "$PWD"
unity recompile --project-path "$PWD"
unity command test run --mode editor --filter Nodin.Editor.Tests --filter-type Assembly --include-explicit --project-path "$PWD"
```

**Project author** · Guntars Polis

[MIT License](LICENSE) · [Changelog](Packages/com.beetrootlab.nodin/CHANGELOG.md)
