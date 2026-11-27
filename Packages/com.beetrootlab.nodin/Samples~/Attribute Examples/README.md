# Attribute Examples

Import **Attribute Examples** from the **Samples** section of Nodin in Unity Package Manager. Choose **Window > Nodin > Attribute Examples** and select a page. **Reset page** restores the scratch values for that page. Changing pages also resets scratch values. Prefab assets and scene objects use Unity’s normal Inspector and Undo.

## Exercise the contexts

- Use **Select asset** on the base prefab and the variant to compare asset-only fields and the variant-only control.
- Use **Add scene instance** to try the instance-only validation and restrictions. Set **Instance Label** to clear the requirement.
- Open the nested example in Prefab Mode and select the **Signal Station** child to see the nested-instance field.
- Add an ordinary scene object and try assigning an asset or a scene object to **Scene Target**, then compare the host and its **Aerial** child in **Child Target**.

`HideNetworkBehaviourFields` is shown on the component declaration. Its visual effect requires a legacy networking host, which this Unity sample does not include.

## Source index

### Numbers

Change Ceiling to update the range and meter. Press Enter after editing Channel Name to commit it. [Source](Editor/NumericExamples.cs)

`PropertyRange`, `Range`, `MinValue`, `Title`, `ReadOnly`, `ShowInInspector`, `OnValueChanged`, `ToggleLeft`, `MaxValue`, `ProgressBar`, `MinMaxSlider`, `Unit`, `SuffixLabel`, `Wrap`, `DelayedProperty`, `EnumPaging`, `EnumToggleButtons`

### Layout

Switch the equipment tab, toggle Capture, and narrow the window to wrap its shortcuts. [Source](Editor/LayoutExamples.cs)

`LabelWidth`, `TitleGroup`, `BoxGroup`, `PropertyOrder`, `FoldoutGroup`, `HorizontalGroup`, `TabGroup`, `VerticalGroup`, `PropertySpace`, `InlineProperty`, `ToggleGroup`, `InlineButton`, `Button`, `ButtonGroup`, `ResponsiveButtonGroup`

### Conditions

Switch Advanced and Locked and compare visibility and editability in Edit and Play modes. [Source](Editor/ConditionalExamples.cs)

`ShowIf`, `HideIf`, `EnableIf`, `DisableIf`, `ShowIfGroup`, `HideIfGroup`, `HideInPlayMode`, `HideInEditorMode`, `DisableInPlayMode`, `DisableInEditorMode`, `Toggle`

### Presentation

Hover Call Sign, expand the dispatch help, and compare the text fields and custom signal slider. [Source](Editor/PresentationExamples.cs)

`PropertyTooltip`, `TypeInfoBox`, `LabelText`, `DisplayAsString`, `HideLabel`, `GUIColor`, `EnableGUI`, `Indent`, `InfoBox`, `DetailedInfoBox`, `MultiLineProperty`, `Multiline`, `Space`, `DrawWithUnity`, `TextArea`, `CustomValueDrawer`, `FilePath`, `FolderPath`

### Validation

Set a run identifier, change the port and array length, and compare the diagnostic controls. [Source](Editor/ValidationExamples.cs)

`Required`, `ValidateInput`, `RequiredListLength`, `ListDrawerSettings`, `ShowDrawerChain`, `ShowPropertyResolver`, `SuppressInvalidAttributeError`

### Collections

Search and page the routes, resize table columns, then edit a matrix or dictionary entry. [Source](Editor/CollectionExamples.cs)

`TableColumnWidth`, `HideInTables`, `Searchable`, `TableList`, `ValueDropdown`, `DictionaryDrawerSettings`, `TableMatrix`

### Callbacks

Edit the name or collection and watch the event log. Right-click Layout Name for its custom action. [Source](Editor/CallbackExamples.cs)

`CustomContextMenu`, `OnCollectionChanged`, `DisableContextMenu`, `OnStateUpdate`, `OnInspectorGUI`, `OnInspectorInit`, `OnInspectorDispose`

### References

Pick a material, color or antenna type. Select the inline target directly to compare its fields. [Source](Editor/ReferenceExamples.cs) · [Inline target](Editor/InlineDetailExample.cs)

`ShowInInlineEditors`, `HideInInlineEditors`, `DisableInInlineEditors`, `AssetList`, `AssetSelector`, `PreviewField`, `AssetsOnly`, `InlineEditor`, `ColorPalette`, `TypeFilter`, `PolymorphicDrawerSettings`, `HideReferenceObjectPicker`, `HideDuplicateReferenceBox`, `TypeDrawerSettings`, `TypeSelectorSettings`, `TypeRegistryItem`

### Prefab and scene contexts

Inspect the supplied base prefab, variant, nested instance and scene examples. [Source](Runtime/ContextExample.cs)

`HideMonoScript`, `HideNetworkBehaviourFields`, `RequiredIn`, `ShowIn`, `HideIn`, `EnableIn`, `DisableIn`, `DisallowModificationsIn`, `SceneObjectsOnly`, `ChildGameObjectsOnly`
