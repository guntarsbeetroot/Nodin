# Nodin

Nodin provides attribute-driven IMGUI inspectors and Editor windows for Unity 6000.3. It uses Unity's serialization for scene objects, assets, lists, prefab overrides, multi-object editing, and Undo. The development project includes Unity Pipeline for CLI verification.

## Install

Add the folder containing this `package.json` through **Window > Package Manager > Install package from disk**, or embed the folder in `Packages/com.beetrootlab.nodin`.

For coding agents working on tools and inspector-configurable UI, link to the [Nodin agent instructions](AGENTS.md) from your project's `AGENTS.md`.

## Examples

Import **Attribute Examples** from Nodin’s **Samples** section in Unity Package Manager. Open **Window > Nodin > Attribute Examples** to try the controls and prefab contexts. The [sample source index](<Samples~/Attribute Examples/README.md>) maps each attribute to its example.

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

## Attribute controls

Use `ShowIn`, `HideIn`, `EnableIn`, and `DisableIn` with a `PrefabKind` bitmask to target prefab assets, variants, nested instances, scene instances, or ordinary objects. `RequiredIn` validates required values in those contexts. `DisallowModificationsIn` also reports existing prefab overrides.

Layout attributes with the same group path share a group. Give a conditional group its own path when its condition is controlled by a separate toggle group.

Use `OnInspectorInit` and `OnInspectorDispose` on parameterless methods or give the attribute a named callback on a field. `OnStateUpdate` can call a method that accepts `NodinPropertyState`. Set `Visible`, `Enabled`, or `Expanded` on that state. `OnCollectionChanged` can pass `CollectionChangeInfo` and the collection to before and after callbacks around inspector edits.

Use `AssetList` and `AssetSelector` to choose project objects by filters. `ChildGameObjectsOnly` restricts scene references to the host hierarchy. Named `ColorPalette` sources can return colors or HTML color strings. Use `TypeFilter` and `PolymorphicDrawerSettings` to customize managed-reference selection for `SerializeReference` fields. Use `ShowInInspector` to expose reflected `System.Type` fields.

`PropertyRange`, `ProgressBar`, and `MinMaxSlider` can resolve bounds from members or expressions. `Unit` can add a suffix or convert using an explicit multiplier. `DictionaryDrawerSettings` and `TableMatrix` can edit reflected collections exposed with `ShowInInspector`. Persistence and Undo for those collections depend on how the host serializes them. Nodin does not add a serialization format.

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
