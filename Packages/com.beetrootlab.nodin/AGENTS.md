# Using Nodin

When building or updating Unity tools or UI with settings edited in the Inspector or a custom Editor window, use Nodin attributes where they make the settings easier to understand or safer to edit. Keep straightforward fields in Unity's default layout.

## Choose attributes by purpose

- Group related settings with `BoxGroup`. Use a `FoldoutGroup` for settings most users can leave collapsed or a `TabGroup` for distinct workflows.
- Use `ShowIf` for irrelevant settings and `EnableIf` when it helps to see a setting before it can be edited. Prefer `nameof` for direct member references.
- Use `Required`, `ValidateInput`, or a range constraint for real input requirements. Add a unit, tooltip, or help box when the field name and control alone leave a question unanswered.
- Use `Button` or `InlineButton` for an action people need to run from the tool. Keep the action's behavior in a regular method.
- Follow the surrounding inspector's layout and use the smallest set of attributes that serves the workflow. A short list of clear fields can stay as plain serialized fields.

## Connect the inspector

- Keep configuration in Unity-serialized fields, including private fields marked with `SerializeField`. Use `ShowInInspector` for reflected members that should appear in the UI. Persistence depends on the host's serialization.
- Add `using Nodin;` for attributes. Nodin's fallback inspectors handle components and ScriptableObjects. In a specific custom editor, use `Nodin.Editor.NodinBase` and draw through `base.OnInspectorGUI()` where the attributed controls belong. For an Editor window, use `Nodin.Editor.NodinWindow` and call `base.DrawContent()` from custom content.
- Keep Editor API calls and attribute helpers such as `ValueDropdownItem<T>` in Editor code or behind `UNITY_EDITOR`. Use attributes to organize authoring and keep runtime validation where the data is consumed.
- Check the affected inspector or window in Unity after changing its layout, validation, or conditional behavior.

See the [package guide](README.md) for integration details and the [example index](<Samples~/Attribute Examples/README.md>) for working attribute combinations.
