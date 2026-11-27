using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nodin.Editor
{
    internal static class NodinInspectorLayout
    {
        private static GUIStyle _tabLabel;
        private static GUIStyle _foldoutPanel;

        private sealed class Group
        {
            internal string Name;
            internal string Path;
            internal readonly List<NodinGroupAttribute> Options = new();
            internal readonly List<Row> Rows = new();
            internal readonly Dictionary<string, Group> Children = new();
            internal bool IsTabs;

            internal Group Child(string name, float order, int index)
            {
                if (Children.TryGetValue(name, out var group))
                    return group;
                group = new Group { Name = name, Path = Path + "/" + name };
                Children.Add(name, group);
                Rows.Add(new Row { Group = group, Order = order, Index = index });
                return group;
            }
        }

        private sealed class Row
        {
            internal NodinPropertyNode Node;
            internal Group Group;
            internal float Order;
            internal int Index;
            internal float Width;
            internal bool Visible => Node?.IsVisible() ?? Group.Rows.Any(row => row.Visible);
        }

        internal static void Draw(NodinPropertyNode parent, IEnumerable<NodinPropertyNode> children)
        {
            var root = new Group { Path = parent.Path };
            var nodes = children.ToList();
            var injections = parent.Path.Length == 0
                ? NodinInspectorExtensions.For(parent.Tree.Target.GetType()).ToArray()
                : Array.Empty<NodinInspectorExtensions.Injection>();

            var tabPaths = nodes.SelectMany(node => node.Attributes.OfType<TabGroupAttribute>())
                .Concat(injections.SelectMany(entry => (entry.Groups ?? Array.Empty<NodinGroupAttribute>()).OfType<TabGroupAttribute>()))
                .GroupBy(tab => tab.Tab)
                .ToDictionary(tabs => tabs.Key, tabs => tabs.Select(PathOf).Distinct().ToArray());
            var i = 0;
            foreach (var node in nodes)
            {
                foreach (var injection in injections.Where(entry => entry.Before == node.Name))
                    Add(root, Injection(parent, injection, node), i++, node.Order, tabPaths);
                Add(root, node, i++, node.Order, tabPaths);
            }

            foreach (var injection in injections.Where(entry => !nodes.Any(n => n.Name == entry.Before)))
                Add(root, Injection(parent, injection, null), i++, 0, tabPaths);
            DrawRows(parent, root);
        }

        private static NodinPropertyNode Injection(NodinPropertyNode parent, NodinInspectorExtensions.Injection injection, NodinPropertyNode next)
        {
            var options = injection.Groups?.Length > 0
                ? injection.Groups
                : next?.Attributes.OfType<NodinGroupAttribute>().ToArray() ?? Array.Empty<NodinGroupAttribute>();
            return new NodinPropertyNode(parent.Tree, "", $"Panel before {injection.Before}", null, options,
                injection: injection.Draw);
        }

        private static string PathOf(NodinGroupAttribute attribute) =>
            attribute is TabGroupAttribute tab ? tab.Group + "/" + tab.Tab : attribute.Group;

        private static bool InGroup(string path, string group) =>
            path == group || path.StartsWith(group + "/", StringComparison.Ordinal);

        internal static string NormalizeGroupPath(NodinGroupAttribute option, TabGroupAttribute tab,
            IReadOnlyDictionary<string, string[]> tabPaths)
        {
            if (option is TabGroupAttribute)
                return PathOf(option);

            var path = option.Group;
            if (tab != null)
            {
                var page = PathOf(tab);
                if (InGroup(path, tab.Group) || InGroup(tab.Group, path))
                    return path;
                if (InGroup(path, tab.Tab))
                    return tab.Group + "/" + path;
                return page + "/" + path;
            }

            var segment = path.Split('/')[0];
            if (tabPaths.TryGetValue(segment, out var aliases) && aliases.Length == 1)
                return aliases[0] + path[segment.Length..];
            return path;
        }

        private static void Add(Group root, NodinPropertyNode node, int index, float order,
            IReadOnlyDictionary<string, string[]> tabPaths)
        {
            var groups = node.Attributes.OfType<NodinGroupAttribute>()
                .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Group)).ToArray();
            var tab = groups.OfType<TabGroupAttribute>().FirstOrDefault();
            var options = groups.Select(option => (option, path: NormalizeGroupPath(option, tab, tabPaths))).ToArray();
            var destination = root;
            var path = options.OrderByDescending(entry => entry.path.Count(c => c == '/'))
                .ThenByDescending(entry => entry.option is TabGroupAttribute)
                .Select(entry => entry.path).FirstOrDefault();
            var width = 0f;
            if (path != null)
            {
                var parts = path.Split('/');
                for (var j = 0; j < parts.Length; j++)
                {
                    var prefix = string.Join("/", parts.Take(j + 1));
                    var local = options.Where(entry => entry.option is TabGroupAttribute
                        ? entry.option.Group == prefix : entry.path == prefix).Select(entry => entry.option).ToArray();
                    var groupOrder = local.Select(option => option.Order).FirstOrDefault(value => value != 0);
                    var child = destination.Child(parts[j], groupOrder == 0 ? order : groupOrder, index);
                    foreach (var option in local)
                    {
                        if (option is TabGroupAttribute)
                            child.IsTabs = true;
                        else
                            child.Options.Add(option);
                    }
                    if (groupOrder != 0)
                        destination.Rows.First(row => row.Group == child).Order = groupOrder;
                    if (j == parts.Length - 1)
                        width = local.OfType<HorizontalGroupAttribute>().FirstOrDefault()?.Width ?? 0;
                    destination = child;
                }
            }
            destination.Rows.Add(new Row { Node = node, Order = order, Index = index, Width = width });
        }

        private static void DrawRows(NodinPropertyNode parent, Group group)
        {
            var rows = group.Rows.Where(row => row.Visible).OrderBy(row => row.Order).ThenBy(row => row.Index).ToArray();
            if (rows.Length == 0)
                return;

            if (group.IsTabs)
            {
                DrawTabGroup(parent, group, rows);
                return;
            }

            if (group.Options.Any(option => option is HorizontalGroupAttribute or ButtonGroupAttribute))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var row in rows)
                    {
                        var width = row.Width is > 0 and <= 1
                            ? Mathf.Max(24, (EditorGUIUtility.currentViewWidth - 40) * row.Width)
                            : row.Width;
                        var sizing = width > 0 ? GUILayout.Width(width) : GUILayout.ExpandWidth(true);
                        using (new EditorGUILayout.VerticalScope(sizing))
                        {
                            var oldWidth = EditorGUIUtility.labelWidth;
                            if (width > 0)
                                EditorGUIUtility.labelWidth = Mathf.Min(oldWidth, width * .45f);
                            try { DrawRow(parent, row); }
                            finally { EditorGUIUtility.labelWidth = oldWidth; }
                        }
                    }
                }
                return;
            }

            foreach (var row in rows)
                DrawRow(parent, row);
        }

        private static void DrawTabGroup(NodinPropertyNode parent, Group group, Row[] rows)
        {
            using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            var pages = rows.Where(row => row.Group != null).ToArray();
            Group page = null;
            if (pages.Length > 0)
            {
                var key = group.Path + ":tab";
                var selected = Math.Max(0, Array.FindIndex(pages, row => row.Group.Name == parent.Tree.State(key, "")));
                var next = DrawTabs(selected, pages.Select(row => row.Group.Name).ToArray());
                parent.Tree.SetState(key, pages[next].Group.Name);
                page = pages[Event.current.type == EventType.Used ? selected : next].Group;
            }

            // Hierarchy mode puts Unity's foldout arrows in the left gutter, outside the tab panel.
            var previousHierarchyMode = EditorGUIUtility.hierarchyMode;
            EditorGUIUtility.hierarchyMode = false;
            try
            {
                if (page != null)
                    DrawGroup(parent, page);
                foreach (var row in rows.Where(row => row.Group == null))
                    row.Node.Draw();
            }
            finally { EditorGUIUtility.hierarchyMode = previousHierarchyMode; }
        }

        private static int DrawTabs(int selected, string[] names)
        {
            var style = _tabLabel ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 0, 0)
            };
            var contents = names.Select(name => new GUIContent(name, name)).ToArray();
            var minimum = contents.Select(content => style.CalcSize(content).x + 6).ToArray();
            var rect = EditorGUILayout.GetControlRect(false, 22);
            var widths = TabWidths(minimum, rect.width);
            var divider = new Color(0, 0, 0, EditorGUIUtility.isProSkin ? 0.42f : 0.25f);
            var inactive = new Color(0, 0, 0, 0.15f);
            var pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
            var x = rect.x;
            var next = selected;
            for (var i = 0; i < contents.Length; i++)
            {
                var left = Mathf.Round(x * pixelsPerPoint) / pixelsPerPoint;
                x += widths[i];
                var right = Mathf.Round(x * pixelsPerPoint) / pixelsPerPoint;
                var tabRect = new Rect(left, rect.y, right - left, 21);
                var active = i == selected;
                if (Event.current.type == EventType.Repaint && tabRect.width > 0)
                {
                    if (!active)
                    {
                        EditorGUI.DrawRect(tabRect, inactive);
                        EditorGUI.DrawRect(new Rect(left, tabRect.yMax - 1, tabRect.width, 1), divider);
                    }
                    // Each shared edge belongs to the tab on its right.
                    if (i > 0)
                        EditorGUI.DrawRect(new Rect(left, tabRect.y, Mathf.Min(1, tabRect.width), tabRect.height), divider);
                }
                if (GUI.Toggle(tabRect, active, contents[i], style) && !active)
                    next = i;
            }
            return next;
        }

        internal static float[] TabWidths(float[] minimum, float available)
        {
            var total = minimum.Sum();
            if (total <= 0)
                return minimum;
            if (total <= available)
            {
                var extra = (available - total) / minimum.Length;
                return minimum.Select(width => width + extra).ToArray();
            }
            return minimum.Select(width => width * available / total).ToArray();
        }

        private static void DrawRow(NodinPropertyNode parent, Row row)
        {
            if (row.Node != null)
                row.Node.Draw();
            else
                DrawGroup(parent, row.Group);
        }

        private static void DrawGroup(NodinPropertyNode parent, Group group)
        {
            var foldouts = group.Options.OfType<FoldoutGroupAttribute>().ToArray();
            if (foldouts.Length == 0)
            {
                DrawGroupContents(parent, group);
                return;
            }

            var key = group.Path + ":foldout";
            var expanded = parent.Tree.State(key, foldouts.Any(foldout => foldout.Expanded));
            var name = foldouts.Select(foldout => foldout.GroupName).FirstOrDefault(text => text != null) ?? group.Name;
            var panel = _foldoutPanel ??= new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(4, 4, 1, 3)
            };
            using var scope = new EditorGUILayout.VerticalScope(panel);

            // The Inspector normally places foldout arrows in the left gutter.
            var previousHierarchyMode = EditorGUIUtility.hierarchyMode;
            EditorGUIUtility.hierarchyMode = false;
            try
            {
                var next = DrawFoldoutHeader(expanded, parent.Text(name, parent.GroupOwner), panel.padding);
                parent.Tree.SetState(key, next);
                // Keep the current layout while Unity consumes the click.
                if (expanded)
                    DrawGroupContents(parent, group);
            }
            finally { EditorGUIUtility.hierarchyMode = previousHierarchyMode; }
        }

        private static bool DrawFoldoutHeader(bool expanded, string name, RectOffset panelPadding)
        {
            var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 3, GUIStyle.none);
            if (Event.current.type == EventType.Repaint)
            {
                var face = Rect.MinMaxRect(row.xMin - panelPadding.left + 1, row.yMin,
                    row.xMax + panelPadding.right - 1, row.yMax);
                var tint = EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.05f) : new Color(0, 0, 0, 0.04f);
                EditorGUI.DrawRect(face, tint);
                EditorGUI.DrawRect(new Rect(face.x, face.yMax - 1, face.width, 1), new Color(0, 0, 0, 0.3f));
            }

            row.xMin += 3;
            var previousLabelWidth = EditorGUIUtility.labelWidth;
            using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
            EditorGUIUtility.labelWidth = Mathf.Max(1, row.width - EditorStyles.foldout.margin.horizontal);
            try { return EditorGUI.Foldout(row, expanded, name, true); }
            finally { EditorGUIUtility.labelWidth = previousLabelWidth; }
        }

        private static void DrawGroupContents(NodinPropertyNode parent, Group group)
        {
            var title = group.Options.OfType<TitleGroupAttribute>().ToArray();
            if (title.Length > 0)
                NodinPropertyNode.DrawTitle(parent.Text(group.Name, parent.GroupOwner),
                    parent.Text(title.Select(item => item.Subtitle).FirstOrDefault(text => text != null), parent.GroupOwner));

            var boxes = group.Options.OfType<BoxGroupAttribute>().ToArray();
            using (new EditorGUILayout.VerticalScope(boxes.Length > 0 ? EditorStyles.helpBox : GUIStyle.none))
            {
                if (boxes.Length > 0 && boxes.All(box => box.ShowLabel))
                {
                    var style = boxes.Any(box => box.CenterLabel)
                        ? new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter }
                        : EditorStyles.boldLabel;
                    EditorGUILayout.LabelField(parent.Text(group.Name, parent.GroupOwner), style);
                }
                DrawRows(parent, group);
            }
        }
    }
}
