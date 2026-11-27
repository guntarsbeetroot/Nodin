# Nodin

Nodin provides attribute-driven IMGUI inspectors and Editor windows for Unity 6000.3. It uses Unity's serialization for scene objects, assets, lists, prefab overrides, multi-object editing, and Undo. The development project includes Unity Pipeline for CLI verification.

## Install

Add the folder containing this `package.json` through **Window > Package Manager > Install package from disk**, or embed the folder in `Packages/com.beetrootlab.nodin`.

## Annotate a component

```csharp
using Nodin;
using UnityEngine;

public sealed class Trail : MonoBehaviour
{
    [BoxGroup("Routing"), ToggleLeft]
    public bool isOpen;

    [BoxGroup("Routing"), ShowIf(nameof(isOpen)), MinValue(1), SuffixLabel("km")]
    [SerializeField]
    private float distance = 4;

    [Button("$GetActionLabel", ButtonSizes.Medium), EnableIf("@distance > 0")]
    private void SetDistance(float kilometers = 4) => distance = kilometers;

    private string GetActionLabel() => $"Update {name}";
}
```

Use Nodin imports and attributes directly alongside native Unity attributes. `Nodin.dll` is auto-referenced. If your assembly definition uses **Override References**, add `Nodin.dll` under **Assembly References**. If upgrading from a source-based Nodin package, remove the old `Nodin` assembly definition reference.

Nodin marks its attributes with `Conditional("UNITY_EDITOR")`, so Unity's player compiler drops the annotations while preserving serialization attributes such as `SerializeField`. Nodin excludes its precompiled attribute assembly from the player after compilation. Application code can use Nodin types as attribute arguments, for example `ButtonSizes.Medium`. Keep code that instantiates or refers to Nodin types at runtime, such as a dropdown provider returning `ValueDropdownItem<T>`, inside an Editor guard or an Editor assembly.

Attributes participate automatically in fallback inspectors for MonoBehaviour and ScriptableObject. Existing specific Unity custom editors retain precedence. Derive an existing custom editor from `Nodin.Editor.NodinBase` and call `base.OnInspectorGUI()` where its attributed controls should appear. Derive an Editor window from `NodinWindow` and override `DrawContent()` to extend its scrollable layout. Call `base.DrawContent()` where its serialized and attributed members should appear.

## Embed properties and register extensions

```csharp
using var tree = NodinPropertyTree.Create(serializedObject);
tree.BeginDraw(true);
try
{
    tree.GetPropertyAtUnityPath("entries.Array.data[0].distance")?.Draw();
}
finally
{
    tree.EndDraw();
}
```

For a window or inspector that repaints frequently, retain and dispose the tree with the owning inspector. `Create(SerializedObject)` borrows the supplied object. `Create(UnityEngine.Object)` owns its serialization wrapper. `RootProperty.Children` enumerates serialized and explicitly exposed members, and each node has `Name`, `Path`, `Children`, `Owner`, `SerializedProperty`, `Value`, and `GetAttribute<T>()`. `BeginDraw(false)` lets the caller apply pending serialized changes and suppresses immediate reflection mutations and callbacks.

Register extensions from an Editor assembly, for example in an `[InitializeOnLoad]` class:

- `NodinInspectorExtensions.RegisterDrawer<T>(draw, priority)` runs an IMGUI renderer for the attribute. It receives the property node, typed attribute, and label. Return true after drawing a replacement control. Change `Value` or the supplied `SerializedProperty` from the control's change check.
- `NodinInspectorExtensions.RegisterInjection<T>(beforeMember, draw, groups)` adds a root inspector panel before a named member. Explicit groups place it in a specific tab or group. Omit them to inherit the next member's grouping.
- `NodinInspectorGUI.BeginBox`, `EndBox`, and `HorizontalLineSeparator` compose with existing IMGUI code.

The build validator rejects runtime references to Nodin and Nodin package assets in player builds. The explicit `PlayerIsolationTests.CompiledPlayerScriptsExcludeNodin` test compiles scripts for the active target and checks the managed assemblies selected for the player.
