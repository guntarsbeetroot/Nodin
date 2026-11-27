using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Nodin.Editor
{
    internal sealed class NodinMemberPath
    {
        private static readonly ConcurrentDictionary<string, NodinMemberPath> Paths = new();
        private readonly string[] _tokens;

        private NodinMemberPath(string path) =>
            _tokens = path.Replace(".Array.data[", ".[").Split('.');

        internal static NodinMemberPath For(string path) => Paths.GetOrAdd(path ?? "", text => new NodinMemberPath(text));

        internal object Get(object root) => Walk(root, _tokens.Length).value;

        internal Type TypeOf(object root) => Walk(root, _tokens.Length).type;

        internal (object owner, FieldInfo field) Member(object root)
        {
            if (_tokens[^1].StartsWith("["))
                return (Walk(root, _tokens.Length - 1).value, null);
            var (owner, type) = Walk(root, _tokens.Length - 1);
            return (owner, type == null ? null : NodinExpression.FindField(type, _tokens[^1]));
        }

        private (object value, Type type) Walk(object root, int count)
        {
            var value = root;
            var type = root?.GetType();
            for (var i = 0; i < count && type != null; i++)
            {
                var token = _tokens[i];
                if (token.Length == 0)
                    continue;
                if (token[0] == '[')
                {
                    var index = int.Parse(token.AsSpan(1, token.Length - 2));
                    type = ElementType(type);
                    value = value is IList list && index < list.Count ? list[index] : null;
                }
                else
                {
                    var field = NodinExpression.FindField(type, token);
                    type = field?.FieldType;
                    value = value != null ? field?.GetValue(value) : null;
                }
                type = value?.GetType() ?? type;
            }
            return (value, type);
        }

        internal static Type ElementType(Type type)
        {
            if (type == null)
                return null;
            if (type.IsArray)
                return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
                return type.GetGenericArguments()[0];
            return type.GetInterfaces()
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IList<>))?.GetGenericArguments()[0];
        }

        internal void Set(object root, object value) => Set(root, 0, value);

        private object Set(object owner, int depth, object value)
        {
            if (depth == _tokens.Length)
                return value;
            var token = _tokens[depth];
            if (token[0] == '[')
            {
                var list = (IList)owner;
                var index = int.Parse(token.AsSpan(1, token.Length - 2));
                list[index] = Set(list[index], depth + 1, value);
            }
            else
            {
                var field = NodinExpression.FindField(owner.GetType(), token) ?? throw new MissingFieldException(token);
                field.SetValue(owner, Set(field.GetValue(owner), depth + 1, value));
            }
            return owner;
        }

        internal static IEnumerable<SerializedProperty> SerializedChildren(SerializedObject root, SerializedProperty parent, bool visible = true)
        {
            if (parent == null)
            {
                var cursor = root.GetIterator();
                if (!cursor.NextVisible(true))
                    yield break;
                do { yield return cursor.Copy(); } while (cursor.NextVisible(false));
                yield break;
            }

            var child = parent.Copy();
            var end = child.GetEndProperty();
            if (!(visible ? child.NextVisible(true) : child.Next(true)))
                yield break;
            while (!SerializedProperty.EqualContents(child, end) && child.depth > parent.depth)
            {
                yield return child.Copy();
                if (!(visible ? child.NextVisible(false) : child.Next(false)))
                    break;
            }
        }
    }
}
