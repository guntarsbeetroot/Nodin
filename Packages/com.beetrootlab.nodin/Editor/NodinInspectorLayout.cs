using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nodin.Editor
{
    internal static class NodinInspectorLayout
    {
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
                using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                var pages = rows.Where(row => row.Group != null).ToArray();
                if (pages.Length > 0)
                {
                    var key = group.Path + ":tab";
                    var selected = Array.FindIndex(pages, row => row.Group.Name == parent.Tree.State(key, ""));
                    selected = Math.Max(0, selected);
                    var next = DrawTabs(selected, pages.Select(row => row.Group.Name).ToArray());
                    var page = pages[Mathf.Clamp(next, 0, pages.Length - 1)];
                    parent.Tree.SetState(key, page.Group.Name);
                    if (Event.current.type == EventType.Used && next != selected)
                        page = pages[selected];
                    DrawGroup(parent, page.Group);
                }
                foreach (var row in rows.Where(row => row.Group == null))
                    row.Node.Draw();
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

        private static int DrawTabs(int selected, string[] names)
        {
            var style = EditorStyles.toolbarButton;
            var contents = names.Select(name => new GUIContent(name, name)).ToArray();
            var minimum = contents.Select(content => style.CalcSize(content).x + 6).ToArray();
            var rect = EditorGUILayout.GetControlRect(false, 22);
            var spacing = Mathf.Clamp((rect.width - minimum.Sum()) / Mathf.Max(1, names.Length - 1), 2, 8);
            var available = Mathf.Max(0, rect.width - (names.Length - 1) * spacing);
            var widths = TabWidths(minimum, available);
            var x = rect.x;
            var next = selected;
            for (var i = 0; i < contents.Length; i++)
            {
                var tabRect = new Rect(x, rect.y, widths[i], 21);
                if (GUI.Toggle(tabRect, i == selected, contents[i], style) && i != selected)
                    next = i;
                x += widths[i] + spacing;
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
            if (foldouts.Length > 0)
            {
                var key = group.Path + ":foldout";
                var expanded = parent.Tree.State(key, foldouts.Any(foldout => foldout.Expanded));
                var name = foldouts.Select(foldout => foldout.GroupName).FirstOrDefault(text => text != null) ?? group.Name;
                bool next;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    next = EditorGUILayout.Foldout(expanded, parent.Text(name, parent.GroupOwner), true);
                parent.Tree.SetState(key, next);
                if (!expanded)
                    return;
            }

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
