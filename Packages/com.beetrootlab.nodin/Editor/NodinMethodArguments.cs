using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nodin.Editor
{
    internal static class NodinMethodArguments
    {
        internal static object Draw(NodinPropertyTree tree, string path, GUIContent label, Type type, object value) =>
            Draw(tree, path, label, type, value, new List<object>(), 0);

        private static object Draw(NodinPropertyTree tree, string path, GUIContent label, Type type, object value,
            List<object> ancestors, int depth)
        {
            var concrete = value?.GetType() ?? type;
            var element = NodinMemberPath.ElementType(concrete);
            if (element == null && NodinValueControls.SupportsReflection(concrete))
                return NodinValueControls.Reflection(label, type, value, true);
            if (typeof(Delegate).IsAssignableFrom(type))
            {
                EditorGUILayout.LabelField(label.text, "Callback supplied by code");
                return value;
            }

            var fields = element == null ? EditableFields(concrete).ToArray() : Array.Empty<FieldInfo>();
            if (fields.Length == 0 && element == null || depth == 8 || value != null && !concrete.IsValueType && ancestors.Any(item => ReferenceEquals(item, value)))
            {
                NodinValueControls.Display(label, value, false);
                return value;
            }

            var key = path + ":argument";
            var expanded = tree.State(key, depth == 0);
            using (new EditorGUILayout.HorizontalScope())
            {
                tree.SetState(key, EditorGUILayout.Foldout(expanded, label, true));
                if (value == null)
                {
                    using var disabled = new EditorGUI.DisabledScope(!CanCreate(type, element));
                    if (GUILayout.Button("Create", GUILayout.ExpandWidth(false)))
                        return Create(type, element);
                }
                else if (!type.IsValueType && GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                    return null;
            }

            if (!expanded || value == null)
                return value;
            using var indent = new EditorGUI.IndentLevelScope();
            if (!concrete.IsValueType)
                ancestors.Add(value);
            try
            {
                if (value is IList list)
                    return DrawList(tree, path, concrete, element, list, ancestors, depth);
                foreach (var field in fields)
                {
                    EditField(value, field, previous => Draw(tree, path + "." + field.Name,
                        new GUIContent(ObjectNames.NicifyVariableName(field.Name)), field.FieldType, previous, ancestors, depth + 1));
                }
                return value;
            }
            finally
            {
                if (!concrete.IsValueType)
                    ancestors.RemoveAt(ancestors.Count - 1);
            }
        }

        private static object DrawList(NodinPropertyTree tree, string path, Type concrete, Type element, IList list,
            List<object> ancestors, int depth)
        {
            var remove = -1;
            for (var i = 0; i < list.Count; i++)
            {
                using var row = new EditorGUILayout.HorizontalScope();
                using (new EditorGUILayout.VerticalScope())
                {
                    using var disabled = new EditorGUI.DisabledScope(list.IsReadOnly);
                    EditListElement(list, i, element, previous => Draw(tree, path + $"[{i}]",
                        new GUIContent($"Element {i}"), element, previous, ancestors, depth + 1));
                }
                using (new EditorGUI.DisabledScope(list.IsReadOnly || list.IsFixedSize && !concrete.IsArray))
                    if (GUILayout.Button(new GUIContent("−", "Remove argument item"), GUILayout.Width(24)))
                        remove = i;
            }

            bool add;
            using (new EditorGUI.DisabledScope(list.IsReadOnly || list.IsFixedSize && !concrete.IsArray))
                add = GUILayout.Button("Add argument item", GUILayout.ExpandWidth(false));
            if (remove < 0 && !add)
                return list;
            if (concrete.IsArray)
            {
                var resized = Array.CreateInstance(element, list.Count + (add ? 1 : -1));
                for (int read = 0, write = 0; read < list.Count; read++)
                    if (read != remove)
                        resized.SetValue(list[read], write++);
                return resized;
            }
            if (add)
                list.Add(element.IsValueType ? Activator.CreateInstance(element) : null);
            else
                list.RemoveAt(remove);
            return list;
        }

        internal static void EditField(object owner, FieldInfo field, Func<object, object> draw)
        {
            var before = field.GetValue(owner);
            var after = draw(before);
            if (field.FieldType.IsValueType || !Equals(before, after))
                field.SetValue(owner, after);
        }

        internal static void EditListElement(IList list, int index, Type element, Func<object, object> draw)
        {
            var before = list[index];
            var after = draw(before);
            if (!list.IsReadOnly && (element.IsValueType || !Equals(before, after)))
                list[index] = after;
        }

        internal static IEnumerable<FieldInfo> EditableFields(Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => !field.IsInitOnly && !field.IsLiteral &&
                    !field.IsDefined(typeof(HideInInspector), true) &&
                    (field.IsPublic || field.IsDefined(typeof(SerializeField), true)));

        private static bool CanCreate(Type type, Type element) =>
            type.IsArray || element != null && type.IsAssignableFrom(typeof(List<>).MakeGenericType(element)) ||
            !type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) != null;

        internal static object Create(Type type, Type element)
        {
            if (type.IsArray)
                return Array.CreateInstance(element, 0);
            if (element != null && (type.IsInterface || type.IsAbstract))
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(element));
            return Activator.CreateInstance(type);
        }
    }
}
