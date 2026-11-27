using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nodin.Editor
{
    internal static partial class NodinCollections
    {
        private const string DragKey = "Nodin.CollectionRow";
        private const float TableHandleWidth = 13;
        private const float TableRemoveWidth = 16;
        private const float TableColumnGap = 3;
        // Explicit zero margins keep GUILayout from inheriting each control's margins.
        private static readonly GUIStyle TableCellStyle = new();
        private sealed class RowDrag
        {
            internal NodinPropertyTree Tree;
            internal string Path;
            internal int Index;
        }

        internal static void Draw(NodinPropertyNode node, SerializedProperty property, GUIContent label)
        {
            var list = node.GetAttribute<ListDrawerSettingsAttribute>();
            var table = node.GetAttribute<TableListAttribute>();
            var search = node.GetAttribute<SearchableAttribute>();
            var dropdown = node.GetAttribute<ValueDropdownAttribute>();
            var pageSize = Math.Max(1, table?.NumberOfItemsPerPage ?? list?.NumberOfItemsPerPage ?? 15);
            var paging = table?.ShowPaging ?? list?.ShowPaging ?? true;
            var fixedExpansion = table?.AlwaysExpanded == true || list?.ShowFoldout == false;
            var expanded = fixedExpansion || (node.ExpandedByState ?? node.Tree.State(node.Path + ":list",
                list?.InitialExpansion ?? property.isExpanded));
            var compatible = CanMultiEditArray(node, property);
            var count = property.arraySize;
            var query = search == null ? "" : node.Tree.State(node.Path + ":query", "").Trim();
            var showItems = expanded || query.Length != 0;
            var indices = showItems && compatible
                ? Enumerable.Range(0, count).Where(index => query.Length == 0 ||
                    Search(property.GetArrayElementAtIndex(index), query, search.FuzzySearch)).ToArray()
                : Array.Empty<int>();
            var pageKey = node.Path + ":page";
            var pageTotal = paging ? Math.Max(1, (indices.Length + pageSize - 1) / pageSize) : 1;
            var page = Mathf.Clamp(node.Tree.State(pageKey, 0), 0, pageTotal - 1);

            using (var header = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(22)))
            {
                var content = EditorGUI.BeginProperty(header.rect, label, property);
                try
                {
                    var heading = label == GUIContent.none ? "" : content.text;
                    if (fixedExpansion)
                    {
                        if (heading.Length != 0)
                            GUILayout.Label(content, EditorStyles.label, GUILayout.ExpandWidth(false));
                    }
                    else
                    {
                        var title = new GUIContent(heading, content.tooltip ?? heading);
                        var foldoutStyle = EditorStyles.foldout;
                        var titleWidth = Mathf.Min(145, Mathf.Max(18, foldoutStyle.CalcSize(title).x + 9));
                        var titleRect = GUILayoutUtility.GetRect(titleWidth, EditorGUIUtility.singleLineHeight,
                            GUILayout.MinWidth(titleWidth), GUILayout.ExpandWidth(true));
                        var previousLabelWidth = EditorGUIUtility.labelWidth;
                        var previousHierarchyMode = EditorGUIUtility.hierarchyMode;
                        bool next;
                        try
                        {
                            // Inspector hierarchy mode shifts the arrow into the margin, outside the toolbar.
                            EditorGUIUtility.hierarchyMode = false;
                            EditorGUIUtility.labelWidth = Mathf.Min(previousLabelWidth, titleRect.width - foldoutStyle.margin.horizontal);
                            next = EditorGUI.Foldout(titleRect, expanded, title, true, foldoutStyle);
                        }
                        finally
                        {
                            EditorGUIUtility.labelWidth = previousLabelWidth;
                            EditorGUIUtility.hierarchyMode = previousHierarchyMode;
                        }
                        node.Tree.SetState(node.Path + ":list", next);
                        property.isExpanded = next;
                    }

                    if (search != null)
                        Query(node, GUILayout.MinWidth(36), GUILayout.MaxWidth(175));
                    if (list?.ShowItemCount != false)
                        GUILayout.Label(count == 0 ? "Empty" : $"{count} items", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                    DrawListHook(node, list?.OnTitleBarGUI);
                    if (pageTotal > 1)
                    {
                        using (new EditorGUI.DisabledScope(page == 0))
                            if (GUILayout.Button(new GUIContent("‹", "Previous page"), EditorStyles.toolbarButton, GUILayout.Width(19)))
                                node.Tree.SetState(pageKey, page - 1);
                        var requested = EditorGUILayout.DelayedIntField(page + 1, GUILayout.Width(25));
                        if (requested != page + 1)
                            node.Tree.SetState(pageKey, Mathf.Clamp(requested - 1, 0, pageTotal - 1));
                        GUILayout.Label($"/ {pageTotal}", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                        using (new EditorGUI.DisabledScope(page == pageTotal - 1))
                            if (GUILayout.Button(new GUIContent("›", "Next page"), EditorStyles.toolbarButton, GUILayout.Width(19)))
                                node.Tree.SetState(pageKey, page + 1);
                    }

                    using (new EditorGUI.DisabledScope(!compatible || node.Tree.EditsDisabled ||
                        node.Tree.IsMulti && (count >= property.serializedObject.maxArraySizeForMultiEditing ||
                                             list?.CustomAddFunction != null)))
                    {
                        if (list?.HideAddButton != true && !node.HasAssetCollectionPicker() &&
                            GUILayout.Button(new GUIContent("+", "Add item"), EditorStyles.toolbarButton, GUILayout.Width(22)))
                        {
                            if (dropdown != null)
                                ShowDropdown(node, property, dropdown, null);
                            else
                            {
                                EditArray(node, property, new CollectionChangeInfo(CollectionChangeType.Add, count),
                                    array => AppendConfiguredElement(node, array, list?.CustomAddFunction));
                                GUIUtility.ExitGUI();
                            }
                        }
                    }
                }
                finally { EditorGUI.EndProperty(); }
            }

            if (!showItems)
                return;
            if (!compatible)
            {
                EditorGUILayout.HelpBox("Edit lists separately when their lengths differ or exceed Unity’s multi-edit limit.", MessageType.Info);
                return;
            }
            if (pageTotal > 1)
                indices = indices.Skip(page * pageSize).Take(pageSize).ToArray();

            if (indices.Length == 0)
            {
                EditorGUILayout.LabelField(query.Length == 0 ? "No items" : "No matching items", EditorStyles.miniLabel);
                return;
            }

            var reorder = list?.DraggableItems != false && node.GetAttribute<NonReorderableAttribute>() == null && count > 1;
            var removable = list?.HideRemoveButton != true && !(node.Tree.IsMulti && list?.CustomRemoveIndexFunction != null);
            var filtered = query.Length > 0;
            var attributes = node.Attributes.Where(attribute => attribute is PreviewFieldAttribute or AssetsOnlyAttribute
                or SceneObjectsOnlyAttribute or InlineEditorAttribute or ChildGameObjectsOnlyAttribute
                or ColorPaletteAttribute or HideReferenceObjectPickerAttribute or PolymorphicDrawerSettingsAttribute
                or TypeDrawerSettingsAttribute or TypeFilterAttribute or TypeSelectorSettingsAttribute
                or ToggleAttribute or InlinePropertyAttribute).ToArray();
            var rows = indices.Select(index => new CollectionRow
            {
                Index = index,
                Node = node.Tree.FromSerialized(property.GetArrayElementAtIndex(index), attributes)
            }).ToArray();

            if (table != null && rows.Any(row => row.Node.Children.Count > 0))
            {
                DrawTable(node, property, rows, reorder, removable, filtered, paging, table, list);
                return;
            }

            foreach (var row in rows)
            {
                var element = row.Node.SerializedProperty;
                var useDropdown = dropdown?.DrawDropdownForListElements == true;
                var hasFoldout = !useDropdown &&
                    element is { propertyType: SerializedPropertyType.Generic } &&
                    (element.hasVisibleChildren || row.Node.Children.Count > 0);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.MinHeight(23)))
                {
                    if (!hasFoldout)
                        RowHandle(node, property, row.Index, reorder, filtered);
                    var elementLabel = ElementLabel(row.Node, list, row.Index);
                    using (var body = new EditorGUILayout.VerticalScope())
                    {
                        DrawListHook(node, list?.OnBeginListElementGUI, row.Index);
                        if (useDropdown)
                        {
                            using var disabled = new EditorGUI.DisabledScope(node.Tree.EditsDisabled);
                            DrawDropdown(node, property, elementLabel, dropdown, row.Index);
                        }
                        else if (hasFoldout)
                            DrawFoldoutRow(node, property, row, elementLabel, body.rect, reorder, filtered);
                        else
                            row.Node.Draw(elementLabel);
                        DrawListHook(node, list?.OnEndListElementGUI, row.Index);
                    }
                    if (removable)
                        RemoveButton(node, property, row.Index, list?.CustomRemoveIndexFunction);
                }
            }
        }

        private static void AppendConfiguredElement(NodinPropertyNode node, SerializedProperty array, string factory)
        {
            var elementType = NodinMemberPath.ElementType(node.ValueType);
            if (string.IsNullOrWhiteSpace(factory))
            {
                AppendElement(array, elementType);
                return;
            }
            var value = NodinExpression.ConvertValue(NodinExpression.Evaluate(node.Owner, factory), elementType);
            var index = array.arraySize++;
            try
            {
                var element = array.GetArrayElementAtIndex(index);
                if (element.propertyType == SerializedPropertyType.ManagedReference)
                    element.managedReferenceValue = value;
                else
                    element.boxedValue = value;
            }
            catch
            {
                array.arraySize = index;
                throw;
            }
        }

        private static void DrawListHook(NodinPropertyNode node, string action, int? index = null)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;
            if (index.HasValue && !action.StartsWith("@", StringComparison.Ordinal))
            {
                try
                {
                    NodinExpression.Call(node.Owner, action.TrimStart('$'), index.Value);
                    return;
                }
                catch (MissingMethodException)
                {
                    // Also allow callbacks that do not take an index.
                }
            }
            NodinPropertyTree.RunAttributeAction(node.Owner, action);
        }

        private static void DrawFoldoutRow(NodinPropertyNode node, SerializedProperty property, CollectionRow row,
            GUIContent label, Rect body, bool reorder, bool filtered)
        {
            if (reorder && !node.Tree.EditsDisabled && GUI.enabled)
            {
                label.tooltip = filtered ? "Clear search to reorder" : "Right-click to move";
                var header = new Rect(body.x, body.y, body.width, EditorGUIUtility.singleLineHeight);
                if (!filtered && Event.current.type == EventType.ContextClick && header.Contains(Event.current.mousePosition))
                    ShowMoveMenu(node, property, row.Index);
            }

            // Keep the foldout inside the row box, including native property foldouts.
            var previousHierarchyMode = EditorGUIUtility.hierarchyMode;
            EditorGUIUtility.hierarchyMode = false;
            try { row.Node.Draw(label); }
            finally { EditorGUIUtility.hierarchyMode = previousHierarchyMode; }
        }

        private sealed class CollectionRow
        {
            internal int Index;
            internal NodinPropertyNode Node;
        }

        private static GUIContent ElementLabel(NodinPropertyNode element, ListDrawerSettingsAttribute options, int index)
        {
            var name = options?.ListElementLabelName;
            var value = element.Value;
            var resolved = name != null && value != null && NodinExpression.TryEvaluate(value, name, out var result, out _)
                ? result?.ToString() : null;
            var description = resolved ?? $"Element {index}";
            return new GUIContent(options?.ShowIndexLabels == true && resolved != null ? $"{index} : {description}" : description);
        }

        private static void DrawTable(NodinPropertyNode collection, SerializedProperty property, CollectionRow[] rows,
            bool reorder, bool removable, bool filtered, bool paging, TableListAttribute table, ListDrawerSettingsAttribute list)
        {
            var columns = rows.SelectMany(row => row.Node.Children)
                .Where(child => child.IsVisible() && child.GetAttribute<HideInTablesAttribute>() == null).GroupBy(ColumnKey).Select(group => group.First())
                .OrderBy(child => child.Order).ToArray();
            var labels = columns.Select(column => column.TableLabel).ToArray();
            var controlsWidth = (reorder ? TableHandleWidth + TableColumnGap : 0) +
                (removable ? TableRemoveWidth + TableColumnGap : 0);
            var scrollKey = collection.Path + ":table-scroll";
            var widthKey = collection.Path + ":table-width";
            var measured = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            var available = collection.Tree.State(widthKey, EditorGUIUtility.currentViewWidth - 24 - EditorGUI.indentLevel * 15);
            if (Event.current.type == EventType.Repaint && measured.width > 1)
                collection.Tree.SetState(widthKey, measured.width);
            var columnWidths = collection.Tree.State(widthKey + ":columns", new Dictionary<int, float>());
            var columnOptions = columns.Select(column => column.GetAttribute<TableColumnWidthAttribute>()).ToArray();
            var widths = TableWidths(columnOptions.Select(option => option?.Width).ToArray(),
                available - controlsWidth,
                columns.Select((column, i) => TableContentWidth(column, labels[i])).ToArray());
            for (var i = 0; i < widths.Length; i++)
                if (columnWidths.TryGetValue(i, out var width))
                    widths[i] = width;
            // A page should grow with its actual rows. The Inspector owns vertical scrolling.
            var scrollOptions = new List<GUILayoutOption> { GUILayout.ExpandHeight(false) };
            if (table.MinScrollViewHeight > 0)
                scrollOptions.Add(GUILayout.MinHeight(table.MinScrollViewHeight));
            if (table.MaxScrollViewHeight > 0 && (!paging || table.MaxScrollViewHeight != 540))
                scrollOptions.Add(GUILayout.MaxHeight(Math.Max(table.MinScrollViewHeight, table.MaxScrollViewHeight)));
            using var scroll = table.DrawScrollView
                ? new EditorGUILayout.ScrollViewScope(collection.Tree.State(scrollKey, Vector2.zero), scrollOptions.ToArray())
                : null;
            if (scroll != null)
                collection.Tree.SetState(scrollKey, scroll.scrollPosition);
            using (var header = new EditorGUILayout.HorizontalScope())
            {
                if (Event.current.type == EventType.Repaint)
                    GUI.Box(header.rect, GUIContent.none, EditorStyles.toolbar);
                if (reorder)
                    GUILayout.Space(TableHandleWidth + TableColumnGap);
                for (var i = 0; i < columns.Length; i++)
                {
                    if (i > 0)
                        GUILayout.Space(TableColumnGap);
                    using var cell = new EditorGUILayout.VerticalScope(TableCellStyle, GUILayout.Width(widths[i]));
                    var labelRect = GUILayoutUtility.GetRect(widths[i], EditorGUIUtility.singleLineHeight, GUILayout.Width(widths[i]));
                    GUI.Label(labelRect, labels[i], EditorStyles.miniBoldLabel);
                    if (columnOptions[i]?.Resizable != false)
                        ResizeColumn(widthKey, i, labelRect, columnWidths, widths[i]);
                }
                if (removable)
                    GUILayout.Space(TableRemoveWidth + TableColumnGap);
            }

            foreach (var row in rows)
            {
                DrawListHook(collection, list?.OnBeginListElementGUI, row.Index);
                var cells = row.Node.Children.ToDictionary(ColumnKey);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (reorder)
                    {
                        RowHandle(collection, property, row.Index, reorder, filtered, TableHandleWidth);
                        GUILayout.Space(TableColumnGap);
                    }
                    for (var i = 0; i < columns.Length; i++)
                    {
                        if (i > 0)
                            GUILayout.Space(TableColumnGap);
                        using var cell = new EditorGUILayout.VerticalScope(TableCellStyle, GUILayout.Width(widths[i]));
                        if (cells.TryGetValue(ColumnKey(columns[i]), out var value) && value.GetAttribute<HideInTablesAttribute>() == null)
                        {
                            using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
                            value.Draw(GUIContent.none);
                        }
                    }
                    if (removable)
                    {
                        GUILayout.Space(TableColumnGap);
                        using var actions = new EditorGUILayout.VerticalScope(TableCellStyle, GUILayout.Width(TableRemoveWidth));
                        RemoveButton(collection, property, row.Index, list?.CustomRemoveIndexFunction, TableRemoveWidth);
                    }
                }
                DrawListHook(collection, list?.OnEndListElementGUI, row.Index);
            }
        }

        private static string ColumnKey(NodinPropertyNode node) => node.Identity;

        private static float TableContentWidth(NodinPropertyNode column, GUIContent label)
        {
            var labelWidth = EditorStyles.miniBoldLabel.CalcSize(label).x;
            if (column.GetAttribute<PreviewFieldAttribute>() is { } preview && typeof(Object).IsAssignableFrom(column.ValueType))
                return Mathf.Max(preview.Height, labelWidth);
            if (column.ValueType == typeof(Vector2) || column.ValueType == typeof(Vector2Int))
                return Mathf.Max(110, labelWidth);
            return 0;
        }

        internal static float[] TableWidths(int?[] requested, float available, float[] contentWidths = null)
        {
            var widths = requested.Select((width, index) => width.HasValue ? Mathf.Max(35f, width.Value) :
                contentWidths?[index] ?? 0).ToArray();
            var flexibleCount = widths.Count(width => width == 0);
            if (flexibleCount == 0)
                return widths;
            var flexibleWidth = Mathf.Max(55, (available - widths.Sum() -
                Mathf.Max(0, requested.Length - 1) * TableColumnGap) / flexibleCount);
            return widths.Select(width => width == 0 ? flexibleWidth : width).ToArray();
        }

        private static void RemoveButton(NodinPropertyNode node, SerializedProperty property, int index,
            string customRemove, float width = 25)
        {
            using var disabled = new EditorGUI.DisabledScope(node.Tree.EditsDisabled);
            if (!GUILayout.Button(new GUIContent("−", $"Remove item {index + 1}"), GUILayout.Width(width)))
                return;
            if (customRemove != null)
            {
                var change = new CollectionChangeInfo(CollectionChangeType.Remove, index);
                node.EditReflectedCollection(change, _ => NodinExpression.Call(node.Owner, customRemove, index));
                GUIUtility.ExitGUI();
                return;
            }
            EditArray(node, property, new CollectionChangeInfo(CollectionChangeType.Remove, index), array =>
            {
                var size = array.arraySize;
                array.DeleteArrayElementAtIndex(index);
                if (array.arraySize == size)
                    array.DeleteArrayElementAtIndex(index);
            });
            GUIUtility.ExitGUI();
        }

        private static void RowHandle(NodinPropertyNode node, SerializedProperty property, int index, bool reorder, bool filtered,
            float width = 21)
        {
            if (!reorder)
                return;
            var rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));
            GUI.Label(rect, new GUIContent("≡", filtered ? "Clear search to reorder" : "Drag to reorder or right-click to move"), EditorStyles.centeredGreyMiniLabel);
            if (filtered || node.Tree.EditsDisabled || !GUI.enabled || !rect.Contains(Event.current.mousePosition))
                return;

            var input = Event.current;
            if (input.type == EventType.MouseDown && input.button == 0)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DragKey, new RowDrag { Tree = node.Tree, Path = node.Path, Index = index });
                DragAndDrop.StartDrag("Reorder list item");
                input.Use();
            }
            else if (input.type == EventType.ContextClick)
                ShowMoveMenu(node, property, index);
            else if (DragAndDrop.GetGenericData(DragKey) is RowDrag drag && drag.Tree == node.Tree &&
                     drag.Path == node.Path && drag.Index != index && input.type is EventType.DragUpdated or EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                if (input.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    EditArray(node, property, new CollectionChangeInfo(CollectionChangeType.Move, drag.Index, index),
                        array => array.MoveArrayElement(drag.Index, index));
                    input.Use();
                    GUIUtility.ExitGUI();
                }
                input.Use();
            }
        }

        private static void ShowMoveMenu(NodinPropertyNode node, SerializedProperty property, int index)
        {
            if (!node.CanDrawCollectionContext)
                return;
            var menu = new GenericMenu();
            AddMove(menu, "Move up", index > 0, node, property, index, index - 1);
            AddMove(menu, "Move down", index + 1 < property.arraySize, node, property, index, index + 1);
            menu.ShowAsContext();
            Event.current.Use();
        }

        private static void AddMove(GenericMenu menu, string label, bool allowed, NodinPropertyNode node,
            SerializedProperty property, int from, int to)
        {
            if (!allowed || !node.Tree.ShouldApply)
            {
                menu.AddDisabledItem(new GUIContent(label));
                return;
            }

            var path = property.propertyPath;
            var unchanged = DeferredEditGuard(node, path);
            menu.AddItem(new GUIContent(label), false, () =>
            {
                if (unchanged())
                    EditArray(node, node.Tree.Serialization.FindProperty(path), new CollectionChangeInfo(CollectionChangeType.Move, from, to),
                        array => array.MoveArrayElement(from, to));
            });
        }

        internal static Func<bool> DeferredEditGuard(NodinPropertyNode node, string path)
        {
            var targets = node.Tree.Targets;
            var hashes = targets.Select(target => ContentHash(target, path)).ToArray();
            return () => node.Tree.ShouldApply &&
                targets.Select(target => ContentHash(target, path)).SequenceEqual(hashes);
        }

        private static uint? ContentHash(Object target, string path)
        {
            if (target == null)
                return null;
            using var serialized = new SerializedObject(target);
            return serialized.FindProperty(path)?.contentHash;
        }

        private static bool CanMultiEditArray(NodinPropertyNode node, SerializedProperty property)
        {
            if (!node.Tree.IsMulti)
                return true;
            foreach (var target in node.Tree.Targets)
            {
                using var serialized = new SerializedObject(target);
                var single = serialized.FindProperty(property.propertyPath);
                if (single == null || single.arraySize != property.arraySize)
                    return false;
            }
            return true;
        }

        internal static void AppendElement(SerializedProperty array, Type elementType)
        {
            var index = array.arraySize++;
            try
            {
                var element = array.GetArrayElementAtIndex(index);
                if (elementType != null && element.propertyType == SerializedPropertyType.Generic && !element.isArray &&
                    (elementType.IsValueType || !elementType.IsAbstract && elementType.GetConstructor(Type.EmptyTypes) != null))
                {
                    var initial = Activator.CreateInstance(elementType);
                    try
                    {
                        element.boxedValue = initial;
                        return;
                    }
                    catch (Exception error) when (error is InvalidOperationException or ArgumentException or NotSupportedException)
                    {
                        // Some native or managed-reference layouts cannot be assigned as a single boxed value.
                    }
                }

                ResetElement(element);
            }
            catch
            {
                array.arraySize = index;
                throw;
            }
        }

        private static void ResetElement(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Generic:
                    if (property.isArray)
                        property.arraySize = 0;
                    else
                        foreach (var child in NodinMemberPath.SerializedChildren(property.serializedObject, property, visible: false))
                            ResetElement(child);
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.ManagedReference:
                    property.managedReferenceValue = null;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = "";
                    break;
                case SerializedPropertyType.Enum:
                    property.intValue = 0;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = new AnimationCurve();
                    break;
                default:
                    var value = property.boxedValue;
                    if (value?.GetType().IsValueType == true)
                        property.boxedValue = Activator.CreateInstance(value.GetType());
                    break;
            }
        }

        internal static void EditArray(NodinPropertyNode node, SerializedProperty property, CollectionChangeInfo change,
            Action<SerializedProperty> edit)
        {
            if (!node.Tree.ShouldApply || node.Tree.EditsDisabled || property == null)
                return;
            var path = property.propertyPath;
            if (node.HasCollectionCallbacks)
                node.Tree.ApplyModifiedProperties();
            var before = node.SnapshotValues();
            node.SerializedCollectionCallback(change, true);
            var current = node.Tree.Serialization.FindProperty(path);
            if (current == null)
                return;
            edit(current);
            if (before == null)
                node.Tree.ApplyModifiedProperties();
            else
                node.NotifyChanged(before);
            node.SerializedCollectionCallback(change, false);
            if (Event.current != null)
                GUI.changed = true;
        }

        internal static void DrawDropdown(NodinPropertyNode node, SerializedProperty property, GUIContent label,
            ValueDropdownAttribute attribute, int? index = null)
        {
            var value = index.HasValue ? property.GetArrayElementAtIndex(index.Value).boxedValue : property.boxedValue;
            var option = Choices(node, attribute).FirstOrDefault(item => NodinExpression.Equal(item.value, value));
            var rect = EditorGUILayout.GetControlRect();
            var targetProperty = index.HasValue ? property.GetArrayElementAtIndex(index.Value) : property;
            var content = EditorGUI.BeginProperty(rect, label, targetProperty);
            try
            {
                var field = string.IsNullOrEmpty(content.text) ? rect : EditorGUI.PrefixLabel(rect, content);
                var text = targetProperty.hasMultipleDifferentValues ? "—" : option.label ?? value?.ToString() ?? "None";
                if (EditorGUI.DropdownButton(field, new GUIContent(text), FocusType.Keyboard))
                    ShowDropdown(node, property, attribute, index, replaceScalar: !property.isArray || property.propertyType == SerializedPropertyType.String);
            }
            finally { EditorGUI.EndProperty(); }
        }

        private static IEnumerable<(string label, object value)> Choices(NodinPropertyNode node, ValueDropdownAttribute attribute)
        {
            var source = Choices(node.Owner, attribute).ToArray();
            foreach (var target in node.Tree.Targets.Skip(1))
            {
                var allowed = Choices(node.OwnerOf(target), attribute).Select(choice => choice.value).ToArray();
                source = source.Where(choice => allowed.Any(value => NodinExpression.Equal(value, choice.value))).ToArray();
            }
            return source;
        }

        private static IEnumerable<(string label, object value)> Choices(object owner, ValueDropdownAttribute attribute)
        {
            if (NodinExpression.Evaluate(owner, attribute.Values) is not IEnumerable values)
                throw new InvalidOperationException($"{attribute.Values} must return an enumerable.");
            foreach (var value in values)
                yield return value is INodinDropdownItem item ? (item.Text, item.Value) : (value?.ToString() ?? "None", value);
        }

        private static void ShowDropdown(NodinPropertyNode node, SerializedProperty property,
            ValueDropdownAttribute attribute, int? index, bool replaceScalar = false)
        {
            if (!node.Tree.ShouldApply)
                return;
            var menu = new GenericMenu();
            var path = property.propertyPath;
            var unchanged = DeferredEditGuard(node, path);
            if (node.Text(attribute.DropdownTitle) is { Length: > 0 } title)
            {
                menu.AddDisabledItem(new GUIContent(title.Replace("/", "／")));
                menu.AddSeparator("");
            }
            var hasValues = false;
            foreach (var choice in Choices(node, attribute))
            {
                var content = new GUIContent(attribute.ExpandAllMenuItems ? choice.label.Replace("/", " › ") : choice.label);
                var unique = attribute.IsUniqueList || attribute.ExcludeExistingValuesInList;
                if (!replaceScalar && unique && HasChoice(node, property, choice.value, index))
                {
                    if (!attribute.ExcludeExistingValuesInList)
                    {
                        menu.AddDisabledItem(content);
                        hasValues = true;
                    }
                    continue;
                }
                hasValues = true;
                menu.AddItem(content, false, () =>
                {
                    if (!unchanged() || !Choices(node, attribute).Any(current => NodinExpression.Equal(current.value, choice.value)))
                        return;
                    node.Tree.Serialization.UpdateIfRequiredOrScript();
                    if (replaceScalar)
                    {
                        var scalar = node.Tree.Serialization.FindProperty(node.Path);
                        if (scalar == null)
                            return;
                        var before = node.SnapshotValues();
                        node.Value = choice.value;
                        if (before == null)
                            node.Tree.ApplyModifiedProperties();
                        else
                            node.NotifyChanged(before);
                    }
                    else
                    {
                        var arrayProperty = node.Tree.Serialization.FindProperty(path);
                        if (arrayProperty == null || unique && HasChoice(node, arrayProperty, choice.value, index))
                            return;
                        var change = new CollectionChangeInfo(index.HasValue ? CollectionChangeType.Replace : CollectionChangeType.Add,
                            index ?? arrayProperty.arraySize);
                        EditArray(node, arrayProperty, change, array =>
                        {
                            if (index.HasValue && index.Value >= array.arraySize)
                                return;
                            var target = index ?? array.arraySize++;
                            var element = array.GetArrayElementAtIndex(target);
                            var type = NodinMemberPath.ElementType(node.ValueType);
                            element.boxedValue = type == null ? choice.value : NodinExpression.ConvertValue(choice.value, type);
                        });
                    }
                });
            }
            if (!hasValues)
                menu.AddDisabledItem(new GUIContent("No values available"));
            menu.ShowAsContext();
        }

        private static bool HasChoice(NodinPropertyNode node, SerializedProperty property, object value, int? except)
        {
            foreach (var target in node.Tree.Targets)
            {
                using var serialized = new SerializedObject(target);
                var array = serialized.FindProperty(property.propertyPath);
                for (var index = 0; array != null && index < array.arraySize; index++)
                    if (index != except && NodinExpression.Equal(array.GetArrayElementAtIndex(index).boxedValue, value))
                        return true;
            }
            return false;
        }

        private static string Query(NodinPropertyNode node, params GUILayoutOption[] options)
        {
            var key = node.Path + ":query";
            var query = node.Tree.State(key, "");
            var controlKey = node.Path + ":search-field";
            var field = node.Tree.State<SearchField>(controlKey, null);
            if (field == null)
            {
                field = new SearchField { autoSetFocusOnFindCommand = false };
                node.Tree.SetState(controlKey, field);
            }
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, options);
            var next = field.OnToolbarGUI(rect, query);
            if (next.Length == 0 && !field.HasFocus() && Event.current.type == EventType.Repaint)
            {
                rect = EditorGUI.IndentedRect(rect);
                rect.xMin += 14;
                GUI.Label(rect, "Search", EditorStyles.miniLabel);
            }
            if (next != query)
            {
                node.Tree.SetState(key, next);
                node.Tree.SetState(node.Path + ":page", 0);
            }
            return query.Trim();
        }

        internal static IEnumerable<NodinPropertyNode> FilterChildren(NodinPropertyNode node, IEnumerable<NodinPropertyNode> children, SearchableAttribute option)
        {
            var query = Query(node);
            return query.Length == 0 ? children : children.Where(child =>
                child.SerializedProperty is { } property ? Search(property, query, option.FuzzySearch)
                    : Match(child.Name + " " + child.Value, query, option.FuzzySearch));
        }

        private static bool Search(SerializedProperty property, string query, bool fuzzy)
        {
            var end = property.GetEndProperty();
            var iterator = property.Copy();
            do
            {
                var value = iterator.propertyType switch
                {
                    SerializedPropertyType.ObjectReference => iterator.objectReferenceValue?.name,
                    SerializedPropertyType.String => iterator.stringValue,
                    SerializedPropertyType.Enum => iterator.enumValueIndex >= 0 && iterator.enumValueIndex < iterator.enumDisplayNames.Length
                        ? iterator.enumDisplayNames[iterator.enumValueIndex] : null,
                    SerializedPropertyType.Boolean => iterator.boolValue.ToString(),
                    SerializedPropertyType.Integer => iterator.longValue.ToString(),
                    SerializedPropertyType.Float => iterator.doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => null
                };
                if (Match(iterator.displayName, query, fuzzy) || value != null && Match(value, query, fuzzy))
                    return true;
            } while (iterator.NextVisible(true) && !SerializedProperty.EqualContents(iterator, end) && iterator.depth > property.depth);
            return false;
        }

        private static bool Match(string text, string query, bool fuzzy)
        {
            if (text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!fuzzy)
                return false;
            var index = 0;
            foreach (var character in text)
                if (char.ToUpperInvariant(character) == char.ToUpperInvariant(query[index]) && ++index == query.Length)
                    return true;
            return false;
        }

        private static bool IsReadOnlySet(object set, Type elementType) =>
            typeof(ICollection<>).MakeGenericType(elementType).GetProperty("IsReadOnly")?.GetValue(set) is true;

        internal static bool DrawSetOrList(NodinPropertyNode node, object value, GUIContent label)
        {
            if (value is not IEnumerable entries || value is string)
                return false;
            var type = value.GetType();
            var list = value as IList;
            var set = type.GetInterfaces().FirstOrDefault(candidate =>
                candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(ISet<>));
            var sequence = SequenceType(type);
            var elementType = list != null ? NodinMemberPath.ElementType(type) ?? typeof(object) :
                (set ?? sequence)?.GetGenericArguments()[0];
            if (elementType == null)
                return false;

            var items = entries.Cast<object>().ToArray();
            var key = node.Path + ":runtime-list";
            var options = node.GetAttribute<ListDrawerSettingsAttribute>();
            var expanded = options?.ShowFoldout == false || (node.ExpandedByState ??
                node.Tree.State(key, options?.InitialExpansion ?? false));
            var heading = options?.ShowItemCount == false ? label.text : $"{label.text} ({items.Length})";
            if (options?.ShowFoldout == false)
                EditorGUILayout.LabelField(heading);
            else
                node.Tree.SetState(key, EditorGUILayout.Foldout(expanded, heading, true));
            if (!expanded)
                return true;

            var searchable = node.GetAttribute<SearchableAttribute>();
            var query = searchable == null ? "" : Query(node);
            using var indent = new EditorGUI.IndentLevelScope();
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var labelText = options?.ListElementLabelName is { } member && item != null &&
                                NodinExpression.TryEvaluate(item, member, out var text, out _) ? text?.ToString() : null;
                labelText ??= $"Element {i}";
                if (query.Length > 0 && !Match(labelText + " " + item, query, searchable.FuzzySearch))
                    continue;

                var index = i;
                Action<object, object> assign = null;
                if (list is { IsReadOnly: false })
                    assign = (owner, edited) => ((IList)owner)[index] = edited;
                else if (set != null && !IsReadOnlySet(value, elementType))
                    assign = (owner, edited) =>
                    {
                        if (Equals(item, edited) || NodinExpression.Truth(NodinExpression.Call(owner, "Contains", edited)))
                            return;
                        NodinExpression.Call(owner, "Remove", item);
                        NodinExpression.Call(owner, "Add", edited);
                    };
                else if (sequence != null)
                    assign = (owner, edited) => ReplaceSequenceElement(owner, sequence.GetGenericTypeDefinition(), index, edited);

                var child = node.CollectionElement(index, elementType, owner =>
                    owner is IList current ? current[index] :
                    set != null ? item : ((IEnumerable)owner).Cast<object>().ElementAt(index), assign);
                DrawListHook(node, options?.OnBeginListElementGUI, index);
                child.Draw(new GUIContent(labelText));
                DrawListHook(node, options?.OnEndListElementGUI, index);
            }
            return true;
        }

        private static Type SequenceType(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (!current.IsGenericType)
                    continue;
                var definition = current.GetGenericTypeDefinition();
                if (definition == typeof(LinkedList<>) || definition == typeof(Stack<>) || definition == typeof(Queue<>))
                    return current;
            }
            return null;
        }

        internal static void ReplaceSequenceElement(object collection, Type definition, int index, object value)
        {
            if (definition == typeof(LinkedList<>))
            {
                var node = collection.GetType().GetProperty("First").GetValue(collection);
                var nodeType = node.GetType();
                var next = nodeType.GetProperty("Next");
                for (var i = 0; i < index; i++)
                    node = next.GetValue(node);
                nodeType.GetProperty("Value").SetValue(node, value);
                return;
            }

            var items = ((IEnumerable)collection).Cast<object>().ToArray();
            items[index] = value;
            NodinExpression.Call(collection, "Clear");
            var stack = definition == typeof(Stack<>);
            foreach (var item in stack ? items.Reverse() : items)
                NodinExpression.Call(collection, stack ? "Push" : "Enqueue", item);
        }

    }
}
