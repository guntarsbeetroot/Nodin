using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nodin.Editor
{
    internal static class NodinValueControls
    {
        internal static GUIStyle OverlayLabel => new(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            clipping = TextClipping.Clip
        };

        internal static void Display(GUIContent label, object value, bool overflow)
        {
            var text = NodinExpression.IsNull(value) ? "None" : value.ToString();
            using var row = new EditorGUILayout.HorizontalScope();
            if (!string.IsNullOrEmpty(label.text))
                EditorGUILayout.PrefixLabel(label);
            if (overflow)
                EditorGUILayout.SelectableLabel(text, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            else
                EditorGUILayout.LabelField(new GUIContent(text, text), EditorStyles.wordWrappedLabel);
        }

        internal static bool SupportsReflection(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsEnum || typeof(Object).IsAssignableFrom(type) ||
                type == typeof(string) || type == typeof(bool) || type == typeof(float) ||
                type == typeof(double) || type == typeof(int) || type == typeof(long) ||
                type == typeof(uint) || type == typeof(ushort) || type == typeof(byte) ||
                type == typeof(short) || type == typeof(sbyte) || type == typeof(Vector2) ||
                type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Vector2Int) ||
                type == typeof(Vector3Int) || type == typeof(Color) || type == typeof(Rect) ||
                type == typeof(Bounds) || type == typeof(AnimationCurve);
        }

        internal static object Reflection(GUIContent label, Type type, object value, bool allowScene)
        {
            type = Nullable.GetUnderlyingType(type ?? typeof(object)) ?? type ?? typeof(object);
            if (typeof(Object).IsAssignableFrom(type))
                return EditorGUILayout.ObjectField(label, value as Object, type, allowScene);
            if (type.IsEnum)
            {
                var selected = value as Enum ?? (Enum)Activator.CreateInstance(type);
                return type.IsDefined(typeof(FlagsAttribute), false)
                    ? EditorGUILayout.EnumFlagsField(label, selected)
                    : EditorGUILayout.EnumPopup(label, selected);
            }

            if (type == typeof(string)) return EditorGUILayout.TextField(label, (string)value ?? "");
            if (type == typeof(bool)) return EditorGUILayout.Toggle(label, value is true);
            if (type == typeof(float)) return EditorGUILayout.FloatField(label, (float)(value ?? 0f));
            if (type == typeof(double)) return EditorGUILayout.DoubleField(label, (double)(value ?? 0d));
            if (type == typeof(int)) return EditorGUILayout.IntField(label, (int)(value ?? 0));
            if (type == typeof(long)) return EditorGUILayout.LongField(label, (long)(value ?? 0L));
            if (type == typeof(uint) || type == typeof(ushort) || type == typeof(byte) ||
                type == typeof(short) || type == typeof(sbyte))
            {
                var number = EditorGUILayout.LongField(label, Convert.ToInt64(value));
                try { return NodinExpression.ConvertValue(number, type); }
                catch (OverflowException) { return value; }
            }
            if (type == typeof(Vector2)) return EditorGUILayout.Vector2Field(label, (Vector2)(value ?? Vector2.zero));
            if (type == typeof(Vector3)) return EditorGUILayout.Vector3Field(label, (Vector3)(value ?? Vector3.zero));
            if (type == typeof(Vector4)) return EditorGUILayout.Vector4Field(label, (Vector4)(value ?? Vector4.zero));
            if (type == typeof(Vector2Int)) return EditorGUILayout.Vector2IntField(label, (Vector2Int)(value ?? Vector2Int.zero));
            if (type == typeof(Vector3Int)) return EditorGUILayout.Vector3IntField(label, (Vector3Int)(value ?? Vector3Int.zero));
            if (type == typeof(Color)) return EditorGUILayout.ColorField(label, (Color)(value ?? Color.white));
            if (type == typeof(Rect)) return EditorGUILayout.RectField(label, (Rect)(value ?? default(Rect)));
            if (type == typeof(Bounds)) return EditorGUILayout.BoundsField(label, (Bounds)(value ?? default(Bounds)));
            if (type == typeof(AnimationCurve)) return EditorGUILayout.CurveField(label, value as AnimationCurve);
            Display(label, value, false);
            return value;
        }

        internal static void String(NodinPropertyNode node, SerializedProperty property, GUIContent label)
        {
            var path = node.GetAttribute<NodinPathAttribute>();
            if (path != null)
            {
                var rect = EditorGUILayout.GetControlRect();
                var content = EditorGUI.BeginProperty(rect, label, property);
                try
                {
                    var browse = rect;
                    browse.xMin = rect.xMax - 29;
                    rect.xMax = browse.xMin - 4;
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        var next = EditorGUI.TextField(rect, content, property.stringValue);
                        if (check.changed)
                            property.stringValue = next;
                    }
                    if (GUI.Button(browse, new GUIContent("…", "Browse")))
                    {
                        var chosen = ChoosePath(node, property.stringValue, path);
                        if (!string.IsNullOrEmpty(chosen))
                        {
                            property.stringValue = chosen;
                            GUI.changed = true;
                        }
                    }
                }
                finally { EditorGUI.EndProperty(); }
                return;
            }

            var lines = Mathf.Max(2, node.GetAttribute<MultiLinePropertyAttribute>().Lines);
            var hasLabel = label != GUIContent.none && !string.IsNullOrEmpty(label.text);
            var line = EditorGUIUtility.singleLineHeight;
            var headerHeight = hasLabel ? line + EditorGUIUtility.standardVerticalSpacing : 0;
            var area = EditorGUILayout.GetControlRect(true, headerHeight + lines * line);
            var contentLabel = EditorGUI.BeginProperty(area, label, property);
            try
            {
                var field = area;
                if (hasLabel)
                {
                    var header = area;
                    header.height = line;
                    EditorGUI.LabelField(header, contentLabel);
                    field.yMin += headerHeight;
                }
                using var change = new EditorGUI.ChangeCheckScope();
                var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                var text = EditorGUI.TextArea(field, property.stringValue, style);
                if (change.changed)
                    property.stringValue = text;
            }
            finally { EditorGUI.EndProperty(); }
        }

        internal static string ChoosePath(NodinPropertyNode node, string current, NodinPathAttribute attribute)
        {
            var (project, parent) = PathRoots(node, attribute);
            var initial = GetInitialPath(project, parent, current);
            if (File.Exists(initial))
                initial = Path.GetDirectoryName(initial);
            if (!Directory.Exists(initial))
                initial = Directory.Exists(parent) ? parent : project;

            string path;
            if (attribute is FilePathAttribute file)
            {
                var extensions = node.Text(file.Extensions);
                path = string.IsNullOrWhiteSpace(extensions)
                    ? EditorUtility.OpenFilePanel("Select file", initial, "")
                    : EditorUtility.OpenFilePanelWithFilters("Select file", initial, new[] { "Supported files", extensions });
            }
            else
                path = EditorUtility.OpenFolderPanel("Select folder", initial, "");

            if (string.IsNullOrEmpty(path))
                return null;
            var stored = StorePath(project, parent, current, path, attribute.AbsolutePath);
            return attribute.UseBackslashes ? stored.Replace('/', '\\') : stored.Replace('\\', '/');
        }

        internal static bool PathExists(NodinPropertyNode node, string current, NodinPathAttribute attribute)
        {
            if (string.IsNullOrWhiteSpace(current))
                return false;
            try
            {
                var (project, parent) = PathRoots(node, attribute);
                var full = GetInitialPath(project, parent, current);
                return attribute is FilePathAttribute ? File.Exists(full) : Directory.Exists(full);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static (string project, string parent) PathRoots(NodinPropertyNode node, NodinPathAttribute attribute)
        {
            var project = Path.GetDirectoryName(Application.dataPath);
            return (project, Path.GetFullPath(Path.Combine(project, node.Text(attribute.ParentFolder) ?? "")));
        }

        private static bool UsesAssetPath(string path) => path == "Assets" || path == "Packages" ||
            path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal);

        internal static string GetInitialPath(string project, string parent, string current)
        {
            if (string.IsNullOrWhiteSpace(current))
                return parent;
            var file = current.Replace('\\', '/');
            return Path.GetFullPath(Path.Combine(UsesAssetPath(file) ? project : parent, file));
        }

        internal static string StorePath(string project, string parent, string current, string selected, bool absolute)
        {
            if (absolute)
                return selected;
            var projectPath = Path.GetRelativePath(project, selected).Replace('\\', '/');
            if (UsesAssetPath(projectPath) && (string.IsNullOrWhiteSpace(current) || UsesAssetPath(current.Replace('\\', '/'))))
                return projectPath;
            return Path.GetRelativePath(parent, selected).Replace('\\', '/');
        }

        internal static Rect PreviewRect(Rect row, GUIContent label, PreviewFieldAttribute preview)
        {
            if (!string.IsNullOrEmpty(label.text))
                row = EditorGUI.PrefixLabel(new Rect(row.x, row.y, row.width, EditorGUIUtility.singleLineHeight), label);
            else
                row = EditorGUI.IndentedRect(row);
            var size = Mathf.Max(0, Mathf.Min(preview.Height, row.width));
            var x = preview.Alignment switch
            {
                ObjectFieldAlignment.Center => row.x + (row.width - size) * .5f,
                ObjectFieldAlignment.Right => row.xMax - size,
                _ => row.x,
            };
            return new Rect(x, row.y, size, preview.Height);
        }

        internal static void EnumButtons(NodinPropertyNode node, SerializedProperty property, GUIContent label, Type enumType)
        {
            var values = Enum.GetValues(enumType).Cast<Enum>().Distinct().ToArray();
            var flags = enumType.IsDefined(typeof(FlagsAttribute), false);
            var mixed = property.hasMultipleDifferentValues;
            var masks = flags && mixed ? GetCurrentMasks(node) : null;
            using (new EditorGUI.DisabledScope(flags && property.hasMultipleDifferentValues && !node.Tree.ShouldApply))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrEmpty(label.text))
                    EditorGUILayout.PrefixLabel(label);
                using var buttons = new EditorGUILayout.VerticalScope();
                foreach (var row in values.Select((value, index) => new { value, index }).GroupBy(entry => entry.index / 4))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        foreach (var entry in row)
                        {
                            var bit = unchecked((uint)EnumBits(entry.value));
                            var current = unchecked((uint)property.intValue);
                            var selected = flags && bit != 0
                                ? masks?.All(mask => (mask & bit) == bit) ?? ((current & bit) == bit)
                                : !mixed && current == bit;
                            if (GUILayout.Toggle(selected, ObjectNames.NicifyVariableName(entry.value.ToString()),
                                    EditorStyles.miniButton, GUILayout.MinWidth(0)) == selected)
                                continue;
                            if (flags && bit != 0 && property.hasMultipleDifferentValues)
                                SetFlagForAllTargets(node, unchecked((int)bit), !selected);
                            else
                                property.intValue = unchecked((int)(flags && bit != 0 ? current ^ bit : bit));
                        }
                    }
                }
            }
        }

        private static uint[] GetCurrentMasks(NodinPropertyNode node)
        {
            return node.Tree.Targets.Select(target =>
            {
                using var serialized = new SerializedObject(target);
                return unchecked((uint)(serialized.FindProperty(node.Path)?.intValue ?? 0));
            }).ToArray();
        }

        internal static void SetFlagForAllTargets(NodinPropertyNode node, int mask, bool enabled)
        {
            var tree = node.Tree;
            if (!tree.ShouldApply || tree.EditsDisabled)
                return;
            tree.ApplyModifiedProperties();
            var changed = false;
            foreach (var target in tree.Targets)
            {
                using var serialized = new SerializedObject(target);
                var property = serialized.FindProperty(node.Path);
                if (property == null)
                    continue;
                property.intValue = enabled ? property.intValue | mask : property.intValue & ~mask;
                changed |= serialized.ApplyModifiedProperties();
            }
            tree.Serialization.Update();
            if (changed)
                tree.NotifyChangesApplied();
        }

        private static ulong EnumBits(Enum value)
        {
            var type = Enum.GetUnderlyingType(value.GetType());
            return type == typeof(ulong) ? Convert.ToUInt64(value, CultureInfo.InvariantCulture) :
                unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }
    }
}
