using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nodin.Editor
{
    public sealed partial class NodinPropertyNode
    {
        private const string ArrayElementMarker = ".Array.data[";

        private bool HasReferenceObjectOptions() => GetAttribute<AssetListAttribute>() != null ||
            GetAttribute<AssetSelectorAttribute>() != null || GetAttribute<ChildGameObjectsOnlyAttribute>() != null;

        internal bool HasReferenceCollectionOptions() => HasReferenceObjectOptions() ||
            GetAttribute<ColorPaletteAttribute>() != null || GetAttribute<HideReferenceObjectPickerAttribute>() != null ||
            GetAttribute<PolymorphicDrawerSettingsAttribute>() != null || GetAttribute<TypeDrawerSettingsAttribute>() != null ||
            GetAttribute<TypeFilterAttribute>() != null || GetAttribute<TypeSelectorSettingsAttribute>() != null;

        internal bool HasAssetCollectionPicker()
        {
            var elementType = NodinMemberPath.ElementType(ValueType);
            return elementType != null && typeof(Object).IsAssignableFrom(elementType) &&
                (GetAttribute<AssetListAttribute>() != null || GetAttribute<AssetSelectorAttribute>() != null);
        }

        private bool DrawReferenceControl(SerializedProperty property, GUIContent label)
        {
            if (property is { isArray: true, isFixedBuffer: false } && HasAssetCollectionPicker())
            {
                DrawAssetCollectionPicker(property);
                NodinCollections.Draw(this, property, label);
                return true;
            }

            if (GetAttribute<ColorPaletteAttribute>() is { } palette &&
                (property?.propertyType == SerializedPropertyType.Color || property == null && ValueType == typeof(Color)))
            {
                DrawPalette(property, label, palette);
                return true;
            }

            if (property?.propertyType == SerializedPropertyType.ManagedReference)
            {
                DrawManagedReference(property, label);
                return true;
            }

            if (property == null && ValueType == typeof(Type))
            {
                DrawTypePicker(null, label, Value as Type);
                return true;
            }

            if (property == null && ValueType is { IsClass: true } or { IsInterface: true } &&
                ValueType != typeof(string) && !typeof(Object).IsAssignableFrom(ValueType) &&
                (GetAttribute<TypeFilterAttribute>() != null || GetAttribute<PolymorphicDrawerSettingsAttribute>() != null))
            {
                var current = Value;
                var hidden = GetAttribute<HideReferenceObjectPickerAttribute>() != null ||
                    ValueType.IsDefined(typeof(HideReferenceObjectPickerAttribute), true) ||
                    current?.GetType().IsDefined(typeof(HideReferenceObjectPickerAttribute), true) == true;
                if (!hidden)
                    DrawTypePicker(null, current == null ? label : new GUIContent("Type"), current?.GetType(), ValueType);
                if (current != null)
                    DrawNested(null, label);
                else if (hidden)
                    EditorGUILayout.LabelField(label, new GUIContent("None"));
                return true;
            }

            if (property == null && HasReferenceObjectOptions() &&
                ValueType != null && typeof(Object).IsAssignableFrom(ValueType))
            {
                DrawReferenceObjectPicker(null, label);
                using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled || !CanWrite);
                using var change = new EditorGUI.ChangeCheckScope();
                var value = EditorGUILayout.ObjectField(label, Value as Object, ValueType,
                    _tree.Targets.All(target => !EditorUtility.IsPersistent(target)) && GetAttribute<AssetsOnlyAttribute>() == null);
                if (change.changed && IsAllowedReference(value))
                    Value = value;
                return true;
            }

            return false;
        }

        private int ReferenceElementOffset => _serialized && Path.EndsWith("]", StringComparison.Ordinal)
            ? Path.LastIndexOf(ArrayElementMarker, StringComparison.Ordinal) : -1;

        private Type ReferenceObjectType
        {
            get
            {
                var element = ReferenceElementOffset;
                if (element < 0)
                    return ValueType;
                var collection = NodinMemberPath.For(Path[..element]).Member(_tree.Target).field;
                return NodinMemberPath.ElementType(collection?.FieldType) ?? ValueType;
            }
        }

        private object ReferenceOwner(Object target) => ReferenceElementOffset is var element && element >= 0
            ? NodinMemberPath.For(Path[..element]).Member(target).owner ?? target : OwnerOf(target);

        private bool IsAllowedReference(Object value)
        {
            if (value == null)
                return true;
            var persistent = EditorUtility.IsPersistent(value);
            if (!persistent && (GetAttribute<AssetsOnlyAttribute>() != null ||
                    _tree.Targets.Any(EditorUtility.IsPersistent)) ||
                persistent && GetAttribute<SceneObjectsOnlyAttribute>() != null)
                return false;

            if (GetAttribute<ChildGameObjectsOnlyAttribute>() is { } child &&
                (ReferenceObjectType == typeof(GameObject) || typeof(Component).IsAssignableFrom(ReferenceObjectType)))
            {
                var transform = ReferenceTransform(value);
                if (transform == null || !_tree.Targets.All(target =>
                        ReferenceTransform(target) is { } root && transform.IsChildOf(root) &&
                        (child.IncludeSelf || transform != root)))
                    return false;
            }

            var list = GetAttribute<AssetListAttribute>();
            var selector = GetAttribute<AssetSelectorAttribute>();
            if (list == null && selector == null)
                return true;
            if (!persistent || !AssetMatches(value, list, selector) || OtherAssetReferences().Contains(value))
                return false;
            return string.IsNullOrWhiteSpace(selector?.Filter) ||
                AssetDatabase.FindAssets(selector.Filter).Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)));
        }

        private HashSet<Object> OtherAssetReferences()
        {
            var values = new HashSet<Object>();
            var element = ReferenceElementOffset;
            if (element < 0 || (GetAttribute<AssetListAttribute>() == null &&
                    GetAttribute<AssetSelectorAttribute>()?.IsUniqueList != true))
                return values;
            var arrayPath = Path[..element];
            var start = element + ArrayElementMarker.Length;
            var current = int.Parse(Path.AsSpan(start, Path.Length - start - 1));
            foreach (var target in _tree.Targets)
            {
                using var serialization = new SerializedObject(target);
                var array = serialization.FindProperty(arrayPath);
                for (var index = 0; array != null && index < array.arraySize; index++)
                    if (index != current)
                        values.Add(array.GetArrayElementAtIndex(index).objectReferenceValue);
            }
            return values;
        }

        private static Transform ReferenceTransform(Object value) => value switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null
        };

        private bool AssetMatches(Object value, AssetListAttribute list, AssetSelectorAttribute selector)
        {
            var path = AssetDatabase.GetAssetPath(value);
            if (string.IsNullOrEmpty(path) || !WithinAssetFolders(path, list?.Path) ||
                !WithinAssetFolders(path, selector?.Paths))
                return false;
            if (list == null)
                return true;
            if (!string.IsNullOrEmpty(list.AssetNamePrefix) &&
                !value.name.StartsWith(list.AssetNamePrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            var gameObject = ReferenceTransform(value)?.gameObject;
            if (!MatchesAssetLabel(list.Tags, gameObject?.tag) ||
                !MatchesAssetLabel(list.LayerNames, gameObject == null ? null : LayerMask.LayerToName(gameObject.layer)))
                return false;
            return string.IsNullOrWhiteSpace(list.CustomFilterMethod) || _tree.Targets.All(target =>
                NodinExpression.Call(ReferenceOwner(target), list.CustomFilterMethod, value) is true);
        }

        private static bool MatchesAssetLabel(string filter, string label) => string.IsNullOrWhiteSpace(filter) ||
            filter.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(option => string.Equals(option.Trim(), label, StringComparison.OrdinalIgnoreCase));

        private static string[] AssetFolders(string paths) => string.IsNullOrWhiteSpace(paths)
            ? Array.Empty<string>()
            : paths.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => path.Trim().Replace('\\', '/').Trim('/'))
                .Where(path => path.Length != 0)
                .Select(path => path == "Assets" || path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                                path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)
                    ? path : "Assets/" + path)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        private static bool WithinAssetFolders(string path, string folders)
        {
            var roots = AssetFolders(folders);
            return roots.Length == 0 || roots.Any(root =>
                path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
        }

        private IEnumerable<Object> ReferenceCandidates(Type type)
        {
            if (GetAttribute<AssetListAttribute>() == null && GetAttribute<AssetSelectorAttribute>() == null)
            {
                var root = ReferenceTransform(_tree.Target);
                return root == null ? Array.Empty<Object>() : root.GetComponentsInChildren<Transform>(true)
                    .SelectMany(child => ObjectsOn(child.gameObject, type)).Where(IsAllowedReference).Distinct();
            }

            var list = GetAttribute<AssetListAttribute>();
            var selector = GetAttribute<AssetSelectorAttribute>();
            var folders = AssetFolders(selector?.Paths ?? list?.Path);
            var searchFolders = folders.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length > 0 && searchFolders.Length == 0)
                return Array.Empty<Object>();

            var filter = selector?.Filter;
            if (string.IsNullOrWhiteSpace(filter))
            {
                if (typeof(Component).IsAssignableFrom(type) || type == typeof(GameObject))
                    filter = "t:Prefab";
                else if (type == typeof(Sprite))
                    filter = "t:Sprite t:Texture2D";
                else
                    filter = "t:" + type.Name;
            }
            var results = searchFolders.Length > 0 ? AssetDatabase.FindAssets(filter, searchFolders) : AssetDatabase.FindAssets(filter);
            var existing = OtherAssetReferences();
            return results.Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path))
                .Where(asset => asset != null)
                .SelectMany(asset => asset is GameObject gameObject && typeof(Component).IsAssignableFrom(type)
                    ? gameObject.GetComponentsInChildren(type, true).Where(component => component != null).Cast<Object>()
                    : type.IsInstanceOfType(asset) ? new[] { asset } : Array.Empty<Object>())
                .Where(asset => !existing.Contains(asset) && AssetMatches(asset, list, selector) &&
                    GetAttribute<SceneObjectsOnlyAttribute>() == null)
                .Where(asset => GetAttribute<ChildGameObjectsOnlyAttribute>() == null || IsAllowedReference(asset))
                .Distinct()
                .OrderBy(asset => asset.name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(AssetDatabase.GetAssetPath, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<Object> ObjectsOn(GameObject gameObject, Type type)
        {
            if (type.IsInstanceOfType(gameObject))
                yield return gameObject;
            if (typeof(Component).IsAssignableFrom(type))
                foreach (var component in gameObject.GetComponents(type))
                    if (component != null)
                        yield return component;
        }

        private void DrawReferenceObjectPicker(SerializedProperty property, GUIContent label)
        {
            var type = ReferenceObjectType;
            if (!HasReferenceObjectOptions() || type == null || !typeof(Object).IsAssignableFrom(type))
                return;
            var selector = GetAttribute<AssetSelectorAttribute>();
            if (selector?.DrawDropdownForListElements == false && Path.EndsWith("]", StringComparison.Ordinal) &&
                GetAttribute<ChildGameObjectsOnlyAttribute>() == null && GetAttribute<AssetListAttribute>() == null)
                return;
            if (GetAttribute<ChildGameObjectsOnlyAttribute>() != null &&
                GetAttribute<AssetSelectorAttribute>() == null && GetAttribute<AssetListAttribute>() == null &&
                type != typeof(GameObject) && !typeof(Component).IsAssignableFrom(type))
                return;

            using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled || !_tree.ShouldApply || !CanWrite);
            var rect = EditorGUILayout.GetControlRect();
            var previous = property == null ? Value as Object : property.objectReferenceValue;
            var mixed = property?.hasMultipleDifferentValues == true || property == null && _tree.IsMulti &&
                _tree.Targets.Any(target => !Equals(Read(target), previous));
            var title = selector?.DropdownTitle ?? (GetAttribute<ChildGameObjectsOnlyAttribute>() != null ? "Child Objects" : "Assets");
            if (!EditorGUI.DropdownButton(EditorGUI.IndentedRect(rect), new GUIContent($"Choose {title}", label.tooltip), FocusType.Keyboard))
                return;

            var guard = ReferenceEditGuard(property);
            var items = ReferenceCandidates(type).Select(value => ObjectChoice(value, !mixed && value == previous, () =>
            {
                if (guard() && value != null && IsAllowedReference(value))
                    AssignObjectReference(property != null, value);
            })).Prepend(new ReferenceChoice("None", "", !mixed && previous == null, () =>
            {
                if (guard())
                    AssignObjectReference(property != null, null);
            }));
            PopupWindow.Show(rect, new ReferencePicker(title, items));
        }

        private void AssignObjectReference(bool serialized, Object value)
        {
            if (serialized)
                ApplyReferenceEdit(property => property.objectReferenceValue = value);
            else
                ApplyReflectedReferenceEdit(value);
        }

        private ReferenceChoice ObjectChoice(Object value, bool selected, Action apply, bool mixed = false)
        {
            var path = AssetDatabase.GetAssetPath(value);
            if (string.IsNullOrEmpty(path) || value is Component)
            {
                var parts = new Stack<string>();
                for (var current = ReferenceTransform(value); current != null; current = current.parent)
                    parts.Push(current.name);
                var hierarchy = string.Join("/", parts);
                path = string.IsNullOrEmpty(path) ? hierarchy : $"{path}  ›  {hierarchy}";
            }
            var name = value is Component ? $"{value.name} ({value.GetType().Name})" : value.name;
            var flat = GetAttribute<AssetSelectorAttribute>()?.FlattenTreeView == true;
            return new ReferenceChoice(flat || string.IsNullOrEmpty(path) ? name : $"{name}  ·  {path}",
                path, selected, apply, mixed: mixed);
        }

        private void DrawAssetCollectionPicker(SerializedProperty property)
        {
            var list = GetAttribute<AssetListAttribute>();
            var selector = GetAttribute<AssetSelectorAttribute>();
            var drawer = GetAttribute<ListDrawerSettingsAttribute>();
            if (list == null && drawer?.HideAddButton == true)
                return;
            var type = NodinMemberPath.ElementType(ValueType);
            var title = selector?.DropdownTitle ?? (list == null ? "Add Asset" : "Asset Membership");
            if (list?.AutoPopulate == true && !_tree.EditsDisabled && _tree.ShouldApply &&
                !_tree.State(Path + ":asset-populated", false))
            {
                _tree.SetState(Path + ":asset-populated", true);
                var guard = ReferenceEditGuard(property);
                EditorApplication.delayCall += () =>
                {
                    if (guard() && !IsDisabled())
                        PopulateAssets(type);
                };
            }

            using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled || !_tree.ShouldApply);
            using var row = new EditorGUILayout.HorizontalScope();
            var rect = EditorGUILayout.GetControlRect();
            if (EditorGUI.DropdownButton(EditorGUI.IndentedRect(rect), new GUIContent(title), FocusType.Keyboard))
            {
                var guard = ReferenceEditGuard(property);
                var arrays = _tree.Targets.Select(ReadReferenceArray).ToArray();
                var items = ReferenceCandidates(type).Select(value =>
                {
                    var present = arrays.All(array => array.Contains(value));
                    var mixed = !present && arrays.Any(array => array.Contains(value));
                    var remove = list != null && present;
                    var unique = list != null || selector.IsUniqueList;
                    var blocked = remove ? drawer?.HideRemoveButton == true :
                        drawer?.HideAddButton == true || present && list == null && unique;
                    Action edit = blocked ? null : () =>
                    {
                        if (!guard() || value == null || !IsAllowedReference(value))
                            return;
                        var indices = arrays.Select(array => remove ? Array.IndexOf(array, value) : array.Length).Distinct().ToArray();
                        var index = indices.Length == 1 ? indices[0] : -1;
                        EditReferenceTargets((target, array) => EditAssetMembership(array, value, remove, unique),
                            new CollectionChangeInfo(remove ? CollectionChangeType.Remove : CollectionChangeType.Add, index));
                    };
                    return ObjectChoice(value, present, edit, mixed);
                });
                PopupWindow.Show(rect, new ReferencePicker(title, items));
            }
            if (list?.AutoPopulate == true && GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                PopulateAssets(type);
        }

        private void PopulateAssets(Type type)
        {
            var values = ReferenceCandidates(type).ToArray();
            if (values.Length == 0 || _tree.Targets.Select(ReadReferenceArray)
                    .All(array => values.All(array.Contains)))
                return;
            EditReferenceTargets((target, array) =>
            {
                var existing = new HashSet<Object>(ReadReferenceArray(array));
                foreach (var value in values)
                    if (existing.Add(value))
                        array.GetArrayElementAtIndex(array.arraySize++).objectReferenceValue = value;
            }, new CollectionChangeInfo(CollectionChangeType.Add));
        }

        private Object[] ReadReferenceArray(Object target)
        {
            using var serialization = new SerializedObject(target);
            return ReadReferenceArray(serialization.FindProperty(Path));
        }

        private static Object[] ReadReferenceArray(SerializedProperty array)
        {
            if (array == null || !array.isArray)
                return Array.Empty<Object>();
            return Enumerable.Range(0, array.arraySize)
                .Select(index => array.GetArrayElementAtIndex(index).objectReferenceValue).ToArray();
        }

        private static void EditAssetMembership(SerializedProperty array, Object value, bool remove, bool unique)
        {
            if (!remove)
            {
                if (!unique || !ReadReferenceArray(array).Contains(value))
                    array.GetArrayElementAtIndex(array.arraySize++).objectReferenceValue = value;
                return;
            }
            for (var index = array.arraySize - 1; index >= 0; index--)
            {
                var item = array.GetArrayElementAtIndex(index);
                if (item.objectReferenceValue != value)
                    continue;
                item.objectReferenceValue = null;
                array.DeleteArrayElementAtIndex(index);
            }
        }

        private void DrawPalette(SerializedProperty property, GUIContent label, ColorPaletteAttribute palette)
        {
            using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled || !CanWrite);
            using var row = new EditorGUILayout.HorizontalScope();
            var rect = EditorGUILayout.GetControlRect();
            var value = property == null ? (Color)Value : property.colorValue;
            var content = property == null ? label : EditorGUI.BeginProperty(rect, label, property);
            try
            {
                using var change = new EditorGUI.ChangeCheckScope();
                var next = EditorGUI.ColorField(rect, content, value, true, palette.ShowAlpha, false);
                if (change.changed)
                {
                    if (property == null)
                        Value = next;
                    else
                        property.colorValue = next;
                }
            }
            finally
            {
                if (property != null)
                    EditorGUI.EndProperty();
            }

            using var popupDisabled = new EditorGUI.DisabledScope(!_tree.ShouldApply);
            var paletteRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(62));
            if (!EditorGUI.DropdownButton(paletteRect, new GUIContent("Palette"), FocusType.Keyboard))
                return;

            var guard = ReferenceEditGuard(property);
            var mixed = property?.hasMultipleDifferentValues == true || property == null && _tree.IsMulti &&
                _tree.Targets.Any(target => !Equals(Read(target), value));
            var items = PaletteColors(palette.PaletteName).Distinct().Select(color =>
            {
                var hex = "#" + (palette.ShowAlpha ? ColorUtility.ToHtmlStringRGBA(color) : ColorUtility.ToHtmlStringRGB(color));
                return new ReferenceChoice(hex, hex, !mixed && color.Equals(value), () =>
                {
                    if (!guard())
                        return;
                    if (property == null)
                    {
                        ApplyReflectedReferenceEdit(target => WithPaletteAlpha(color, (Color)Read(target), palette.ShowAlpha));
                    }
                    else
                    {
                        EditReferenceTargets((target, current) =>
                            current.colorValue = WithPaletteAlpha(color, current.colorValue, palette.ShowAlpha));
                    }
                }, color);
            });
            PopupWindow.Show(paletteRect, new ReferencePicker("Color Palette", items));
        }

        private static Color WithPaletteAlpha(Color swatch, Color current, bool showAlpha)
        {
            if (!showAlpha)
                swatch.a = current.a;
            return swatch;
        }

        private IEnumerable<Color> PaletteColors(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return new[]
                {
                    Color.black, Color.gray, Color.white, Color.red, Color.yellow,
                    Color.green, Color.cyan, Color.blue, Color.magenta
                };
            }
            if (reference.StartsWith("$", StringComparison.Ordinal) || reference.StartsWith("@", StringComparison.Ordinal))
                return ParsePalette(Resolve(reference, ReferenceOwner(_tree.Target)));
            if (NodinExpression.TryEvaluate(ReferenceOwner(_tree.Target), reference, out var source, out _) && source != null)
                return ParsePalette(source);
            return ParsePalette(reference);
        }

        private static IEnumerable<Color> ParsePalette(object source)
        {
            if (source is IEnumerable<Color> colors)
                return colors;
            if (source is IEnumerable<Color32> compact)
                return compact.Select(color => (Color)color);
            if (source is string text)
                source = text.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
            return source is IEnumerable<string> encoded
                ? encoded.Select(entry => ColorUtility.TryParseHtmlString(entry.Trim(), out var color) ? (Color?)color : null)
                    .Where(color => color.HasValue).Select(color => color.Value)
                : Array.Empty<Color>();
        }

        private void DrawManagedReference(SerializedProperty property, GUIContent label)
        {
            var baseType = ManagedReferenceDeclaredType(property);
            var current = property.managedReferenceValue?.GetType();
            var hidden = GetAttribute<HideReferenceObjectPickerAttribute>() != null ||
                baseType?.IsDefined(typeof(HideReferenceObjectPickerAttribute), true) == true ||
                current?.IsDefined(typeof(HideReferenceObjectPickerAttribute), true) == true;
            var sameType = HaveSameManagedType(property);
            var hasChildren = property.hasVisibleChildren && sameType;
            if (!hidden)
                DrawTypePicker(property, hasChildren ? new GUIContent("Type") : label, current, baseType, !sameType);
            if (hasChildren)
                DrawNested(property, label);
            else if (hidden && label != GUIContent.none)
                EditorGUILayout.LabelField(label, new GUIContent(property.hasMultipleDifferentValues ? "—" : current?.Name ?? "None"));
        }

        private bool HaveSameManagedType(SerializedProperty property)
        {
            if (!_tree.IsMulti || !property.hasMultipleDifferentValues)
                return true;
            var typename = property.managedReferenceFullTypename;
            return _tree.Targets.All(target =>
            {
                using var serialized = new SerializedObject(target);
                return serialized.FindProperty(Path)?.managedReferenceFullTypename == typename;
            });
        }

        private static Type ManagedReferenceDeclaredType(SerializedProperty property)
        {
            var typename = property.managedReferenceFieldTypename;
            var separator = typename.IndexOf(' ');
            if (separator < 0)
                return null;
            var assembly = typename[..separator];
            var name = typename[(separator + 1)..].Replace('/', '+');
            return Type.GetType($"{name}, {assembly}");
        }

        private void DrawTypePicker(SerializedProperty property, GUIContent label, Type current, Type baseType = null,
            bool mixedManagedTypes = false)
        {
            var drawer = GetAttribute<TypeDrawerSettingsAttribute>();
            var settings = GetAttribute<TypeSelectorSettingsAttribute>();
            var polymorphic = baseType == null ? null : GetAttribute<PolymorphicDrawerSettingsAttribute>();
            var mixed = mixedManagedTypes || property == null && _tree.IsMulti && _tree.Targets.Any(target =>
                !Equals(baseType == null ? Read(target) : Read(target)?.GetType(), current));
            var locked = polymorphic?.ReadOnlyIfNotNullReference == true &&
                (current != null || mixed && _tree.Targets.Any(target => Read(target) != null));
            using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled || !_tree.ShouldApply || !CanWrite ||
                locked || property != null && baseType == null);
            var rect = EditorGUILayout.GetControlRect();
            var content = property == null ? label : EditorGUI.BeginProperty(rect, label, property);
            try
            {
                var field = string.IsNullOrEmpty(content.text) ? EditorGUI.IndentedRect(rect) : EditorGUI.PrefixLabel(rect, content);
                var typeName = mixed ? "—" : current == null ? "None" : TypeLabel(current);
                if (polymorphic?.ShowBaseType == true)
                    typeName = $"{typeName}  ({baseType?.Name})";
                if (!EditorGUI.DropdownButton(field, new GUIContent(typeName, current?.FullName), FocusType.Keyboard))
                    return;

                var guard = ReferenceEditGuard(property);
                var items = ReferenceTypes(baseType, drawer, settings, polymorphic).Select(type => new ReferenceChoice(TypeChoiceLabel(type, settings), type.AssemblyQualifiedName,
                    !mixed && type == current, !mixed && type == current ? null : () =>
                    {
                        if (guard() && ReferenceTypes(baseType, drawer, settings, polymorphic, type).Any())
                            AssignReferenceType(property != null, baseType != null, type, polymorphic);
                    }));
                if (settings?.ShowNoneItem != false)
                    items = items.Prepend(new ReferenceChoice("None", "", !mixed && current == null,
                        !mixed && current == null ? null : () =>
                        {
                            if (guard())
                                AssignReferenceType(property != null, baseType != null, null, polymorphic);
                        }));
                PopupWindow.Show(field, new ReferencePicker("Select Type", items));
            }
            finally
            {
                if (property != null)
                    EditorGUI.EndProperty();
            }
        }

        private IEnumerable<Type> ReferenceTypes(Type baseType, TypeDrawerSettingsAttribute drawer, TypeSelectorSettingsAttribute settings,
            PolymorphicDrawerSettingsAttribute polymorphic, Type selected = null)
        {
            IEnumerable<Type> types;
            if (GetAttribute<TypeFilterAttribute>() is { } filter)
            {
                var available = _tree.Targets.Select(target => Resolve(filter.Member, ReferenceOwner(target)) as IEnumerable<Type>
                    ?? throw new InvalidOperationException($"'{filter.Member}' must return an enumerable of types."));
                types = available.Aggregate((left, right) => left.Intersect(right));
                if (selected != null)
                    types = types.Where(type => type == selected);
            }
            else if (selected != null)
                types = new[] { selected };
            else if (baseType != null && baseType != typeof(object))
                types = TypeCache.GetTypesDerivedFrom(baseType).Prepend(baseType);
            else
                types = LoadedReferenceTypes.Value;

            return types.Where(type => type != null &&
                    MatchesBase(type, baseType) && MatchesBase(type, drawer?.BaseType) &&
                    (drawer == null || MatchesTypeFlags(type, drawer.Filter)) &&
                    (baseType == null || CanCreateManagedType(type, polymorphic)) &&
                    (string.IsNullOrWhiteSpace(settings?.FilterTypesFunction) || _tree.Targets.All(target =>
                        NodinExpression.Call(ReferenceOwner(target), settings.FilterTypesFunction, type) is true)))
                .Distinct()
                .OrderBy(type => type.GetCustomAttribute<TypeRegistryItemAttribute>()?.Priority ?? 0)
                .ThenBy(type => TypeChoiceLabel(type, settings), StringComparer.OrdinalIgnoreCase)
                .ThenBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal);
        }

        private static readonly Lazy<Type[]> LoadedReferenceTypes = new(() =>
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(LoadReferenceTypes).ToArray());

        private static IEnumerable<Type> LoadReferenceTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException error) { return error.Types.Where(type => type != null); }
            catch (NotSupportedException) { return Array.Empty<Type>(); }
        }

        private static bool MatchesBase(Type type, Type baseType)
        {
            if (baseType == null || baseType.IsAssignableFrom(type))
                return true;
            if (!baseType.IsGenericTypeDefinition)
                return false;
            for (var current = type; current != null; current = current.BaseType)
                if (current.IsGenericType && current.GetGenericTypeDefinition() == baseType)
                    return true;
            return type.GetInterfaces().Any(item => item.IsGenericType && item.GetGenericTypeDefinition() == baseType);
        }

        private static bool MatchesTypeFlags(Type type, TypeInclusionFilter filter)
        {
            var category = type.IsInterface ? TypeInclusionFilter.IncludeInterfaces :
                type.IsAbstract ? TypeInclusionFilter.IncludeAbstracts : TypeInclusionFilter.IncludeConcreteTypes;
            return (filter & category) != 0 && (!type.ContainsGenericParameters ||
                (filter & TypeInclusionFilter.IncludeGenerics) != 0);
        }

        private static bool CanCreateManagedType(Type type, PolymorphicDrawerSettingsAttribute settings) =>
            type.IsClass && !type.IsAbstract && !type.IsGenericType && !type.IsArray &&
            !typeof(Object).IsAssignableFrom(type) && !typeof(Delegate).IsAssignableFrom(type) && type != typeof(string) &&
            !typeof(System.Collections.IDictionary).IsAssignableFrom(type) &&
            type.IsDefined(typeof(SerializableAttribute), false) &&
            (!string.IsNullOrWhiteSpace(settings?.CreateInstanceFunction) || ParameterlessConstructor(type) != null ||
                settings != null && settings.NonDefaultConstructorPreference != NonDefaultConstructorPreference.Exclude);

        private static ConstructorInfo ParameterlessConstructor(Type type) =>
            type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);

        private static string TypeLabel(Type type) => type.GetCustomAttribute<TypeRegistryItemAttribute>()?.Name ?? type.Name;

        private static string TypeChoiceLabel(Type type, TypeSelectorSettingsAttribute settings)
        {
            var name = TypeLabel(type);
            if (settings?.ShowCategories == false)
                return name;
            var category = type.GetCustomAttribute<TypeRegistryItemAttribute>()?.CategoryPath;
            if (string.IsNullOrWhiteSpace(category))
                category = settings?.PreferNamespaces == false ? type.Assembly.GetName().Name : type.Namespace;
            return string.IsNullOrEmpty(category) ? name : $"{category} / {name}";
        }

        private void AssignReferenceType(bool serialized, bool instance, Type type, PolymorphicDrawerSettingsAttribute settings)
        {
            if (serialized)
                ApplyManagedReferenceType(type, settings);
            else if (instance)
                ApplyReflectedReferenceEdit(target => Read(target)?.GetType() == type
                    ? Read(target) : CreateManagedInstance(type, settings, target));
            else
                ApplyReflectedReferenceEdit(type);
        }

        internal void ApplyManagedReferenceType(Type type, PolymorphicDrawerSettingsAttribute settings)
        {
            if (!_tree.ShouldApply || _tree.EditsDisabled)
                return;
            var values = _tree.Targets.Where(target => target != null && Read(target)?.GetType() != type)
                .ToDictionary(target => target, target => CreateManagedInstance(type, settings, target));
            if (values.Count == 0)
                return;
            EditReferenceTargets((target, property) =>
            {
                if (!values.TryGetValue(target, out var value))
                    return;
                property.managedReferenceValue = value;
                if (type != null)
                    property.isExpanded = true;
            });
        }

        private object CreateManagedInstance(Type type, PolymorphicDrawerSettingsAttribute settings, Object target)
        {
            if (type == null)
                return null;
            if (!string.IsNullOrWhiteSpace(settings?.CreateInstanceFunction))
            {
                var created = NodinExpression.Call(ReferenceOwner(target), settings.CreateInstanceFunction, type);
                if (created?.GetType() != type)
                    throw new InvalidOperationException($"'{settings.CreateInstanceFunction}' must create a {type.Name}.");
                return created;
            }
            if (ParameterlessConstructor(type) is { } constructor)
                return constructor.Invoke(null);
            if (settings?.NonDefaultConstructorPreference == NonDefaultConstructorPreference.ConstructIdeal)
            {
                var shortest = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(candidate => candidate.GetParameters().All(parameter => !parameter.ParameterType.IsByRef &&
                        !parameter.ParameterType.IsPointer && !parameter.ParameterType.IsByRefLike))
                    .OrderBy(candidate => candidate.GetParameters().Length).FirstOrDefault();
                if (shortest != null)
                    return shortest.Invoke(shortest.GetParameters().Select(parameter => parameter.HasDefaultValue
                        ? parameter.DefaultValue : parameter.ParameterType.IsValueType
                            ? Activator.CreateInstance(parameter.ParameterType) : null).ToArray());
            }
            if (settings?.NonDefaultConstructorPreference == NonDefaultConstructorPreference.LogWarning)
                Debug.LogWarning($"A {type.Name} managed reference was created without a constructor.", target);
            return FormatterServices.GetUninitializedObject(type);
        }

        private Func<bool> ReferenceEditGuard(SerializedProperty property)
        {
            if (property != null)
            {
                var unchanged = NodinCollections.DeferredEditGuard(this, property.propertyPath);
                return () => unchanged() && IsVisible() && !IsDisabled();
            }

            var targets = _tree.Targets;
            var values = targets.Select(Read).ToArray();
            return () => _tree.ShouldApply && targets.All(target => target != null) && IsVisible() && !IsDisabled() &&
                targets.SequenceEqual(_tree.Targets) && targets.Select(Read).SequenceEqual(values);
        }

        private void ApplyReferenceEdit(Action<SerializedProperty> edit)
        {
            _tree.Serialization.UpdateIfRequiredOrScript();
            var property = SerializedProperty;
            if (property == null)
                return;
            var before = SnapshotValues();
            edit(property);
            if (before == null)
                _tree.ApplyModifiedProperties();
            else
                NotifyChanged(before);
        }

        private void ApplyReflectedReferenceEdit(object value) => ApplyReflectedReferenceEdit(_ => value);

        private void ApplyReflectedReferenceEdit(Func<Object, object> value)
        {
            var before = SnapshotValues();
            _tree.Perform("Edit " + Name, target => Write(target, value(target)), IsStatic);
            NotifyChanged(before);
        }

        private void EditReferenceTargets(Action<Object, SerializedProperty> edit, CollectionChangeInfo? change = null)
        {
            if (!_tree.ShouldApply || _tree.EditsDisabled)
                return;
            var targets = _tree.Targets;
            if (targets.Any(target => target == null))
                return;
            _tree.ApplyModifiedProperties();

            var copies = new List<SerializedObject>();
            var before = SnapshotValues();
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Edit " + Name);
            try
            {
                if (change.HasValue)
                    SerializedCollectionCallback(change.Value, true);
                foreach (var target in targets)
                    copies.Add(new SerializedObject(target));

                var properties = copies.Select(serialization => serialization.FindProperty(Path)).ToArray();
                if (properties.Any(property => property == null))
                    return;
                for (var index = 0; index < properties.Length; index++)
                    edit(targets[index], properties[index]);
                var changed = false;
                foreach (var serialization in copies)
                    changed |= serialization.ApplyModifiedProperties();
                if (!changed)
                    return;

                if (change.HasValue)
                    SerializedCollectionCallback(change.Value, false);
                _tree.Serialization.Update();
                if (before == null)
                    _tree.NotifyChangesApplied();
                else
                    NotifyChanged(before);
            }
            finally
            {
                foreach (var copy in copies)
                    copy.Dispose();
                Undo.CollapseUndoOperations(group);
            }
        }

        private sealed class ReferenceChoice
        {
            internal ReferenceChoice(string label, string detail, bool selected, Action apply, Color? swatch = null,
                bool mixed = false)
            {
                Content = new GUIContent(mixed ? $"—  {label}" : selected ? $"✓  {label}" : label, detail);
                SearchText = label + " " + detail;
                Apply = apply;
                Swatch = swatch;
            }

            internal GUIContent Content { get; }
            internal string SearchText { get; }
            internal Action Apply { get; }
            internal Color? Swatch { get; }
        }

        private sealed class ReferencePicker : PopupWindowContent
        {
            private const float Padding = 6;
            private const float RowHeight = 22;
            private readonly string _title;
            private readonly ReferenceChoice[] _choices;
            private readonly SearchField _search = new();
            private ReferenceChoice[] _visible;
            private string _query = "";
            private Vector2 _scroll;

            internal ReferencePicker(string title, IEnumerable<ReferenceChoice> choices)
            {
                _title = title;
                _choices = _visible = choices.ToArray();
            }

            public override Vector2 GetWindowSize() => new(375, Mathf.Clamp(52 + _choices.Length * RowHeight, 92, 330));

            public override void OnGUI(Rect rect)
            {
                EditorGUI.LabelField(new Rect(Padding, Padding, rect.width - Padding * 2, 18), _title, EditorStyles.boldLabel);
                var query = _search.OnGUI(new Rect(Padding, 27, rect.width - Padding * 2, 18), _query);
                if (!string.Equals(query, _query, StringComparison.Ordinal))
                {
                    _query = query;
                    _visible = _choices.Where(choice =>
                        choice.SearchText.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                    _scroll = Vector2.zero;
                }

                if (_visible.Length == 0)
                {
                    EditorGUI.LabelField(new Rect(Padding, 50, rect.width - Padding * 2, 18), "No matching values");
                    return;
                }

                var viewport = new Rect(Padding, 49, rect.width - Padding * 2, rect.height - 49 - Padding);
                var contentWidth = viewport.width - (_visible.Length * RowHeight > viewport.height ? 16 : 0);
                _scroll = GUI.BeginScrollView(viewport, _scroll,
                    new Rect(0, 0, contentWidth, _visible.Length * RowHeight));
                Action selection = null;
                try
                {
                    var start = Mathf.FloorToInt(_scroll.y / RowHeight);
                    var end = Mathf.Min(_visible.Length, Mathf.CeilToInt((_scroll.y + viewport.height) / RowHeight));
                    for (var index = start; index < end; index++)
                    {
                        var choice = _visible[index];
                        var row = new Rect(0, index * RowHeight, contentWidth, RowHeight - 2);
                        using var disabled = new EditorGUI.DisabledScope(choice.Apply == null);
                        if (GUI.Button(row, choice.Content, EditorStyles.miniButton))
                            selection = choice.Apply;
                        if (choice.Swatch is { } swatch)
                            EditorGUI.DrawRect(new Rect(row.x + 6, row.y + 3, 20, row.height - 6), swatch);
                    }
                }
                finally { GUI.EndScrollView(); }

                if (selection == null)
                    return;
                editorWindow.Close();
                EditorApplication.delayCall += () =>
                {
                    try { selection(); }
                    catch (Exception error) { Debug.LogException(error.InnerException ?? error); }
                };
                GUIUtility.ExitGUI();
            }
        }
    }
}
