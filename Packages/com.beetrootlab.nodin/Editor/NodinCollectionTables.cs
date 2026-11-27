using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nodin.Editor
{
    internal static partial class NodinCollections
    {
        internal static void DrawDictionary(NodinPropertyNode node, IDictionary dictionary, GUIContent label)
        {
            var options = node.GetAttribute<DictionaryDrawerSettingsAttribute>();
            var key = node.Path + ":dictionary";
            var expanded = node.ExpandedByState ?? node.Tree.State(key,
                options?.DisplayMode == DictionaryDisplayOptions.ExpandedFoldout);
            node.Tree.SetState(key, EditorGUILayout.Foldout(expanded, $"{label.text} ({dictionary.Count})", true));
            if (!expanded)
                return;

            var generic = dictionary.GetType().GetInterfaces().FirstOrDefault(type =>
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>));
            var types = generic?.GetGenericArguments();
            var keyType = types?[0] ?? typeof(object);
            var valueType = types?[1] ?? typeof(object);
            var readOnly = options?.IsReadOnly == true || dictionary.IsReadOnly || node.Tree.EditsDisabled || node.Tree.IsMulti;
            var changeKeys = !readOnly && !dictionary.IsFixedSize && NodinValueControls.SupportsReflection(keyType);
            var entries = new List<DictionaryEntry>(dictionary.Count);
            foreach (DictionaryEntry entry in dictionary)
                entries.Add(entry);

            using var indent = new EditorGUI.IndentLevelScope();
            for (var index = 0; index < entries.Count; index++)
            {
                var item = entries[index];
                var entryKey = item.Key;
                var folded = options?.DisplayMode is DictionaryDisplayOptions.ExpandedFoldout or DictionaryDisplayOptions.Foldout;
                var child = node.CollectionElement(index, valueType == typeof(object) && item.Value != null ? item.Value.GetType() : valueType,
                    owner => ((IDictionary)owner)[entryKey],
                    readOnly ? null : (owner, edited) => ((IDictionary)owner)[entryKey] = edited,
                    new CollectionChangeInfo(CollectionChangeType.Replace, index, key: entryKey));
                using var row = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                if (folded)
                {
                    var entryState = key + ":entry:" + index;
                    var open = node.Tree.State(entryState, options.DisplayMode == DictionaryDisplayOptions.ExpandedFoldout);
                    node.Tree.SetState(entryState, EditorGUILayout.Foldout(open, $"{entryKey}", true));
                    if (!open)
                        continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (changeKeys)
                    {
                        using var check = new EditorGUI.ChangeCheckScope();
                        var next = NodinValueControls.Reflection(new GUIContent(options?.KeyLabel ?? "Key"), keyType, entryKey, true);
                        if (check.changed && !NodinExpression.IsNull(next) && !dictionary.Contains(next))
                        {
                            EditDictionary(node, new CollectionChangeInfo(CollectionChangeType.Replace, index, key: entryKey),
                                data =>
                                {
                                    data.Add(next, data[entryKey]);
                                    data.Remove(entryKey);
                                });
                            GUIUtility.ExitGUI();
                        }
                    }
                    else
                        EditorGUILayout.LabelField(options?.KeyLabel ?? "Key", entryKey?.ToString() ?? "None");

                    if (!readOnly && !dictionary.IsFixedSize &&
                        GUILayout.Button(new GUIContent("−", "Remove entry"), GUILayout.Width(25)))
                    {
                        EditDictionary(node, new CollectionChangeInfo(CollectionChangeType.Remove, index, key: entryKey),
                            data => data.Remove(entryKey));
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUI.DisabledScope(readOnly))
                    child.Draw(new GUIContent(options?.ValueLabel ?? "Value"));
            }

            if (!changeKeys || !node.Tree.ShouldApply)
                return;
            var addKey = key + ":new-key";
            var nextKey = node.Tree.State<object>(addKey, keyType.IsValueType ? Activator.CreateInstance(keyType) : null);
            using var footer = new EditorGUILayout.HorizontalScope();
            nextKey = NodinValueControls.Reflection(new GUIContent("New key"), keyType, nextKey, true);
            node.Tree.SetState(addKey, nextKey);
            using var disabled = new EditorGUI.DisabledScope(NodinExpression.IsNull(nextKey) || dictionary.Contains(nextKey));
            if (GUILayout.Button("Add", GUILayout.Width(48)))
                EditDictionary(node, new CollectionChangeInfo(CollectionChangeType.Add, dictionary.Count, key: nextKey),
                    data => data.Add(nextKey, valueType.IsValueType ? Activator.CreateInstance(valueType) : null));
        }

        private static void EditDictionary(NodinPropertyNode node, CollectionChangeInfo change, Action<IDictionary> edit)
        {
            node.EditReflectedCollection(change, value => edit((IDictionary)value));
        }

        internal static void DrawMatrix(NodinPropertyNode node, Array matrix, GUIContent label, TableMatrixAttribute options)
        {
            var element = matrix.GetType().GetElementType();
            var xDimension = options.Transpose ? 1 : 0;
            var yDimension = 1 - xDimension;
            var columns = matrix.GetLength(xDimension);
            var rows = matrix.GetLength(yDimension);
            if (columns == 0 || rows == 0)
            {
                EditorGUILayout.LabelField(label, new GUIContent("Empty matrix"));
                return;
            }

            var readOnly = node.Tree.EditsDisabled || node.Tree.IsMulti || options.IsReadOnly || !node.Tree.ShouldApply;
            var height = Mathf.Max(EditorGUIUtility.singleLineHeight, options.RowHeight);
            var width = options.SquareCells ? Mathf.Max(height, 35) : 84;
            if (options.SquareCells)
                height = width;
            var key = node.Path + ":matrix";
            var widths = node.Tree.State(key + ":widths", new Dictionary<int, float>());
            var title = node.Text(options.HorizontalTitle) ?? label.text;
            if (!string.IsNullOrEmpty(title))
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(options.VerticalTitle))
                EditorGUILayout.LabelField(node.Text(options.VerticalTitle), EditorStyles.miniLabel);

            var showHeader = !options.HideColumnIndices || options.ResizableColumns && !options.SquareCells;
            var rowLabelWidth = options.HideRowIndices ? 0 : 30;
            var tableWidth = rowLabelWidth + columns * width + widths.Where(entry => entry.Key < columns).Sum(entry => entry.Value - width);
            var scrollbar = tableWidth > EditorGUIUtility.currentViewWidth - 32 ? 18 : 4;
            var headerHeight = showHeader ? EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing : 0;
            var scrollHeight = Mathf.Min(540, headerHeight + rows * (height + EditorGUIUtility.standardVerticalSpacing) + scrollbar);
            using var indent = new EditorGUI.IndentLevelScope(options.RespectIndentLevel ? 0 : -EditorGUI.indentLevel);
            using var scroll = new EditorGUILayout.ScrollViewScope(node.Tree.State(key, Vector2.zero), GUILayout.Height(scrollHeight));
            node.Tree.SetState(key, scroll.scrollPosition);
            if (showHeader)
            {
                using var header = new EditorGUILayout.HorizontalScope();
                if (!options.HideRowIndices)
                    GUILayout.Space(rowLabelWidth);
                for (var x = 0; x < columns; x++)
                {
                    var current = widths.TryGetValue(x, out var saved) ? saved : width;
                    var rect = GUILayoutUtility.GetRect(current, EditorGUIUtility.singleLineHeight, GUILayout.Width(current));
                    if (!options.HideColumnIndices)
                        GUI.Label(rect, (x + matrix.GetLowerBound(xDimension)).ToString(), EditorStyles.centeredGreyMiniLabel);
                    if (options.ResizableColumns && !options.SquareCells)
                        ResizeColumn(key, x, rect, widths, current);
                }
            }

            for (var y = 0; y < rows; y++)
            {
                using var row = new EditorGUILayout.HorizontalScope();
                if (!options.HideRowIndices)
                {
                    var index = GUILayoutUtility.GetRect(rowLabelWidth, height, GUILayout.Width(rowLabelWidth));
                    index.y += (index.height - EditorGUIUtility.singleLineHeight) * .5f;
                    index.height = EditorGUIUtility.singleLineHeight;
                    GUI.Label(index, (y + matrix.GetLowerBound(yDimension)).ToString(), EditorStyles.miniLabel);
                }
                for (var x = 0; x < columns; x++)
                {
                    var first = (options.Transpose ? y : x) + matrix.GetLowerBound(0);
                    var second = (options.Transpose ? x : y) + matrix.GetLowerBound(1);
                    var item = matrix.GetValue(first, second);
                    var current = widths.TryGetValue(x, out var saved) ? saved : width;
                    var rect = GUILayoutUtility.GetRect(current, height, GUILayout.Width(current));
                    object edited;
                    using (new EditorGUI.DisabledScope(readOnly))
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        if (string.IsNullOrEmpty(options.DrawElementMethod))
                        {
                            edited = DrawMatrixCell(rect, element, item, !EditorUtility.IsPersistent(node.Tree.Target));
                            if (!check.changed)
                                continue;
                        }
                        else
                            edited = NodinExpression.Call(node.Owner, options.DrawElementMethod, rect, item);
                    }
                    if (!readOnly && !Equals(item, edited))
                    {
                        var index = y * columns + x;
                        node.EditReflectedCollection(new CollectionChangeInfo(CollectionChangeType.Replace, index),
                            value => ((Array)value).SetValue(NodinExpression.ConvertValue(edited, element), first, second));
                    }
                }
            }
        }

        private static object DrawMatrixCell(Rect rect, Type type, object value, bool allowScene)
        {
            using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
            if (typeof(Object).IsAssignableFrom(type))
                return EditorGUI.ObjectField(rect, value as Object, type, allowScene);

            rect.y += (rect.height - EditorGUIUtility.singleLineHeight) * .5f;
            rect.height = EditorGUIUtility.singleLineHeight;
            if (type.IsEnum && value is Enum option)
                return type.IsDefined(typeof(FlagsAttribute), false) ? EditorGUI.EnumFlagsField(rect, option) :
                    EditorGUI.EnumPopup(rect, option);

            switch (value)
            {
                case null when type == typeof(string): return EditorGUI.TextField(rect, "");
                case string text: return EditorGUI.TextField(rect, text);
                case bool boolean: return EditorGUI.Toggle(rect, boolean);
                case float single: return EditorGUI.FloatField(rect, single);
                case double number: return EditorGUI.DoubleField(rect, number);
                case int integer: return EditorGUI.IntField(rect, integer);
                case long wide: return EditorGUI.LongField(rect, wide);
                case uint or ushort or byte or short or sbyte:
                    var next = EditorGUI.LongField(rect, Convert.ToInt64(value));
                    try { return NodinExpression.ConvertValue(next, type); }
                    catch (OverflowException) { return value; }
                case Color color: return EditorGUI.ColorField(rect, color);
                case Vector2 vector: return EditorGUI.Vector2Field(rect, GUIContent.none, vector);
                case Vector3 vector: return EditorGUI.Vector3Field(rect, GUIContent.none, vector);
                case Vector4 vector: return EditorGUI.Vector4Field(rect, GUIContent.none, vector);
                default:
                    var description = value?.ToString() ?? "None";
                    GUI.Label(rect, new GUIContent(description, description), EditorStyles.miniLabel);
                    return value;
            }
        }

        private static void ResizeColumn(string key, int index, Rect header,
            IDictionary<int, float> widths, float current)
        {
            var handle = new Rect(header.xMax - 4, header.y, 4, header.height);
            EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeHorizontal);
            var id = GUIUtility.GetControlID((key, index).GetHashCode(), FocusType.Passive, handle);
            var input = Event.current;
            switch (input.GetTypeForControl(id))
            {
                case EventType.MouseDown when input.button == 0 && handle.Contains(input.mousePosition):
                    GUIUtility.hotControl = id;
                    input.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    widths[index] = Mathf.Clamp(current + input.delta.x, 35, 480);
                    input.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    input.Use();
                    break;
            }
        }
    }
}
