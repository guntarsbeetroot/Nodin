using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nodin.Editor
{
    public static class NodinInspectorExtensions
    {
        private sealed class Entry
        {
            internal Type Attribute;
            internal Func<NodinPropertyNode, Attribute, GUIContent, bool> Draw;
            internal int Priority;
        }

        internal sealed class Injection
        {
            internal Type Target;
            internal string Before;
            internal Action<Object> Draw;
            internal NodinGroupAttribute[] Groups;
        }

        private static readonly List<Entry> Drawers = new();
        private static readonly List<Injection> Injections = new();

        /// <summary>Registers an IMGUI drawer. Return true to replace the built-in value control.</summary>
        public static void RegisterDrawer<T>(Func<NodinPropertyNode, T, GUIContent, bool> draw, int priority = 0)
            where T : Attribute
        {
            if (draw == null)
                throw new ArgumentNullException(nameof(draw));
            Drawers.Add(new Entry
            {
                Attribute = typeof(T),
                Draw = (node, attribute, label) => draw(node, (T)attribute, label),
                Priority = priority
            });
            var ordered = Drawers.OrderByDescending(entry => entry.Priority).ToArray();
            Drawers.Clear();
            Drawers.AddRange(ordered);
            NodinPropertyTree.ClearDrawerCache();
        }

        /// <summary>Registers a root-level inspector panel before a named member.</summary>
        public static void RegisterInjection<T>(string beforeMember, Action<T> draw, params NodinGroupAttribute[] groups)
            where T : Object
        {
            if (draw == null)
                throw new ArgumentNullException(nameof(draw));
            Injections.Add(new Injection
            {
                Target = typeof(T),
                Before = beforeMember,
                Draw = target => draw((T)target),
                Groups = groups
            });
        }

        internal static IEnumerable<Injection> For(Type type) => Injections.Where(entry => entry.Target.IsAssignableFrom(type));

        internal static bool HasDrawer(NodinPropertyNode node) => node.Attributes.Any(HasDrawer);

        internal static bool HasDrawer(Attribute attribute) => Drawers.Any(entry => entry.Attribute.IsInstanceOfType(attribute));

        internal static bool Draw(NodinPropertyNode node, GUIContent label)
        {
            foreach (var entry in Drawers)
            {
                var attribute = node.Attributes.FirstOrDefault(entry.Attribute.IsInstanceOfType);
                if (attribute != null && entry.Draw(node, attribute, label))
                    return true;
            }
            return false;
        }
    }

    public static class NodinInspectorGUI
    {
        public static void BeginBox(string title = null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (!string.IsNullOrEmpty(title))
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        public static void EndBox() => EditorGUILayout.EndVertical();

        public static void HorizontalLineSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(1, 1, 1, .18f) : new Color(0, 0, 0, .27f));
        }
    }
}
