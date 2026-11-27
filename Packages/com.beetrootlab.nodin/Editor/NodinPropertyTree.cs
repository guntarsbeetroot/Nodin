using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nodin.Editor
{
    public sealed class NodinPropertyTree : IDisposable
    {
        private static readonly Dictionary<Type, MemberInfo[]> InspectedMembers = new();
        private readonly Dictionary<string, object> _state = new();
        private readonly Dictionary<string, UnityEditor.Editor> _inlineEditors = new();
        private readonly bool _ownsSerialization;
        private bool _drawing;
        private bool _changedDuringDraw;
        private bool _apply;
        private bool _disposed;

        internal SerializedObject Serialization { get; }
        internal Object Target => Serialization.targetObject;
        internal Object[] Targets => Serialization.targetObjects;
        public NodinPropertyNode RootProperty { get; }

        /// <summary>Raised after inspector edits commit, including edits made by deferred menus.</summary>
        public event Action ChangesApplied;

        private NodinPropertyTree(SerializedObject serializedObject, bool ownsSerialization)
        {
            Serialization = serializedObject ?? throw new ArgumentNullException(nameof(serializedObject));
            _ownsSerialization = ownsSerialization;
            RootProperty = new NodinPropertyNode(this, "", "", null, Array.Empty<Attribute>());
        }

        public static NodinPropertyTree Create(SerializedObject serializedObject) => new(serializedObject, false);
        public static NodinPropertyTree Create(Object target) => new(new SerializedObject(target), true);

        public void BeginDraw(bool applyChanges = true)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NodinPropertyTree));
            if (_drawing)
                throw new InvalidOperationException("This property tree is already drawing.");
            Serialization.UpdateIfRequiredOrScript();
            _apply = applyChanges;
            _drawing = true;
        }

        public void EndDraw()
        {
            if (!_drawing)
                throw new InvalidOperationException("BeginDraw must be called first.");
            try
            {
                if (_apply)
                    ApplyModifiedProperties();
            }
            finally
            {
                _drawing = false;
                if (_changedDuringDraw)
                {
                    _changedDuringDraw = false;
                    ChangesApplied?.Invoke();
                }
            }
        }

        internal void ApplyModifiedProperties()
        {
            if (Serialization.ApplyModifiedProperties())
                NotifyChangesApplied();
        }

        internal void NotifyChangesApplied()
        {
            if (_drawing)
                _changedDuringDraw = true;
            else
                ChangesApplied?.Invoke();
        }

        public void Draw()
        {
            BeginDraw();
            try { RootProperty.Draw(); }
            finally { EndDraw(); }
        }

        public NodinPropertyNode GetPropertyAtUnityPath(string path)
        {
            var property = Serialization.FindProperty(path);
            return property == null ? null : FromSerialized(property);
        }

        internal bool HasCustomization()
        {
            if (NodinInspectorExtensions.For(Target.GetType()).Any() || Target.GetType().IsDefined(typeof(SearchableAttribute), true))
                return true;
            return Children(RootProperty).Any(HasCustomization);
        }

        private bool HasCustomization(NodinPropertyNode node) =>
            node.Attributes.Any(a => a.GetType().Assembly == typeof(ShowInInspectorAttribute).Assembly) ||
            NodinInspectorExtensions.HasDrawer(node) ||
            HasNestedCustomization(node.ValueType, new HashSet<Type>());

        private static readonly Dictionary<Type, bool> NestedCustomization = new();

        private static bool HasNestedCustomization(Type type, HashSet<Type> visited)
        {
            type = NodinMemberPath.ElementType(type) ?? type;
            if (type == null || !visited.Add(type) || !type.IsDefined(typeof(SerializableAttribute), false) ||
                type.IsEnum || type.IsPrimitive || type == typeof(string) || typeof(Object).IsAssignableFrom(type))
                return false;
            return InspectMembers(type).Length > 0 || Fields(type).Any(field =>
                field.GetCustomAttributes(false).OfType<Attribute>().Any(a =>
                    a.GetType().Assembly == typeof(ShowInInspectorAttribute).Assembly || NodinInspectorExtensions.HasDrawer(a))
                || HasNestedCustomization(field.FieldType, visited));
        }

        internal static void ClearDrawerCache() => NestedCustomization.Clear();

        internal bool HasNestedCustomization(Type type)
        {
            if (type == null)
                return false;
            if (!NestedCustomization.TryGetValue(type, out var cached))
            {
                cached = HasNestedCustomization(type, new HashSet<Type>());
                NestedCustomization[type] = cached;
            }
            return cached;
        }

        internal IReadOnlyList<NodinPropertyNode> Children(NodinPropertyNode parent)
        {
            var root = parent == RootProperty;
            var property = parent.SerializedProperty;
            var owner = root ? Target : parent.Value;
            var children = new List<NodinPropertyNode>();
            if (property?.isArray == true && property.propertyType != SerializedPropertyType.String)
            {
                for (var i = 0; i < property.arraySize; i++)
                    children.Add(FromSerialized(property.GetArrayElementAtIndex(i)));
                return children;
            }

            if (root || property != null && property.propertyType is SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference)
            {
                foreach (var child in NodinMemberPath.SerializedChildren(Serialization, property))
                    if (child.name != "m_Script" || Target is not EditorWindow)
                        children.Add(FromSerialized(child));
            }

            if (owner == null)
                return children;

            var members = InspectMembers(owner.GetType()).AsEnumerable();
            if (!root && property == null)
                members = members.Concat(Fields(owner.GetType()));
            var names = new HashSet<string>(children.Select(child => child.Name));
            foreach (var member in members)
            {
                var key = MemberKey(member);
                if (!names.Add(key))
                    continue;
                var path = root ? key : parent.Path + "." + key;
                var serialized = root || property != null ? Serialization.FindProperty(path) : null;
                if (serialized != null)
                    children.Add(FromSerialized(serialized));
                else
                    children.Add(new NodinPropertyNode(this, path, member.Name, member, Attributes(member), root ? null : parent));
            }
            return children;
        }

        private static IEnumerable<FieldInfo> Fields(Type type)
        {
            for (; type != null && type != typeof(Object); type = type.BaseType)
                foreach (var field in type.GetFields(NodinExpression.Members))
                    if (!field.IsStatic && !field.IsDefined(typeof(NonSerializedAttribute)) &&
                        !field.IsDefined(typeof(HideInInspector)) && (field.IsPublic || field.IsDefined(typeof(SerializeField)) ||
                        field.IsDefined(typeof(SerializeReference))))
                        yield return field;
        }

        private static MemberInfo[] InspectMembers(Type type)
        {
            if (InspectedMembers.TryGetValue(type, out var members))
                return members;

            var hierarchy = new Stack<Type>();
            for (var current = type; current != null && current != typeof(Object); current = current.BaseType)
                hierarchy.Push(current);

            var list = new List<MemberInfo>();
            foreach (var current in hierarchy)
                foreach (var member in current.GetMembers(NodinExpression.Members).OrderBy(m => m.MetadataToken))
                    if (member switch
                    {
                        FieldInfo => member.IsDefined(typeof(ShowInInspectorAttribute)),
                        PropertyInfo property => property.GetIndexParameters().Length == 0 && property.GetMethod != null &&
                                                 member.IsDefined(typeof(ShowInInspectorAttribute)),
                        MethodInfo method => !method.IsSpecialName && !method.ContainsGenericParameters &&
                                             (member.IsDefined(typeof(ButtonAttribute)) || member.IsDefined(typeof(ButtonGroupAttribute)) ||
                                              member.IsDefined(typeof(OnInspectorGUIAttribute))),
                        _ => false
                    })
                        list.Add(member);

            members = list.GroupBy(MemberKey).Select(group => group.Last()).ToArray();
            InspectedMembers[type] = members;
            return members;
        }

        internal static string MemberKey(MemberInfo member) =>
            member is MethodInfo method
                ? method.Name + "(" + string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.FullName)) + ")"
                : member.Name;

        internal static Attribute[] Attributes(MemberInfo member) =>
            member?.GetCustomAttributes(true).OfType<Attribute>().ToArray() ?? Array.Empty<Attribute>();

        internal NodinPropertyNode FromSerialized(SerializedProperty property, Attribute[] elementAttributes = null)
        {
            var (_, field) = NodinMemberPath.For(property.propertyPath).Member(Target);
            return new NodinPropertyNode(this, property.propertyPath, property.name, field, elementAttributes ?? Attributes(field), serialized: true);
        }

        internal T State<T>(string key, T initial)
        {
            if (_state.TryGetValue(key, out var value))
                return (T)value;
            _state.Add(key, initial);
            return initial;
        }

        internal void SetState(string key, object value) => _state[key] = value;

        private static readonly HashSet<Object> ActiveInlineEditors = new();

        internal void DrawInlineEditor(string path, Object value)
        {
            if (Targets.Contains(value) || !ActiveInlineEditors.Add(value))
            {
                EditorGUILayout.HelpBox("This reference is already being inspected.", MessageType.Info);
                return;
            }

            try { DrawCachedEditor(path, value); }
            finally { ActiveInlineEditors.Remove(value); }
        }

        private void DrawCachedEditor(string path, Object value)
        {
            _inlineEditors.TryGetValue(path, out var editor);
            UnityEditor.Editor.CreateCachedEditor(value, null, ref editor);
            _inlineEditors[path] = editor;
            if (editor == null)
                return;
            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(EditsDisabled))
                editor.OnInspectorGUI();
        }

        internal bool ShouldApply => !_disposed && (!_drawing || _apply);
        internal bool EditsDisabled { get; set; }
        internal bool IsMulti => Serialization.isEditingMultipleObjects;

        internal void Perform(string undoName, Action<Object> action, bool once = false) =>
            Perform(undoName, Targets, action, once);

        internal void Perform(string undoName, Object[] targets, Action<Object> action, bool once = false)
        {
            if (_drawing && (!_apply || EditsDisabled))
                return;
            ApplyModifiedProperties();
            Undo.RecordObjects(targets, undoName);
            try
            {
                foreach (var target in once ? targets.Take(1) : targets)
                    action(target);
            }
            finally
            {
                foreach (var target in targets)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    EditorUtility.SetDirty(target);
                }
                Serialization.Update();
                NotifyChangesApplied();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            foreach (var editor in _inlineEditors.Values)
                if (editor != null)
                    Object.DestroyImmediate(editor);
            _inlineEditors.Clear();
            _state.Clear();
            ChangesApplied = null;
            if (_ownsSerialization)
                Serialization.Dispose();
            _disposed = true;
        }
    }

    public sealed partial class NodinPropertyNode
    {
        private readonly NodinPropertyTree _tree;
        private readonly MemberInfo _member;
        private readonly NodinPropertyNode _parent;
        private readonly bool _serialized;
        private readonly Action<Object> _injection;
        private readonly Func<object, object> _read;
        private readonly Action<object, object> _write;
        private readonly Type _valueType;
        public string Name { get; }
        public string Path { get; }
        internal string Identity => _member == null ? Name : NodinPropertyTree.MemberKey(_member);
        public IReadOnlyList<Attribute> Attributes { get; }
        public SerializedProperty SerializedProperty => _serialized ? _tree.Serialization.FindProperty(Path) : null;
        public object Owner => OwnerOf(_tree.Target);
        public IReadOnlyList<NodinPropertyNode> Children => _tree.Children(this);

        public T GetAttribute<T>() where T : Attribute => Attributes.OfType<T>().FirstOrDefault();

        internal NodinPropertyNode(NodinPropertyTree tree, string path, string name, MemberInfo member, Attribute[] attributes,
            NodinPropertyNode parent = null, bool serialized = false, Action<Object> injection = null,
            Type valueType = null, Func<object, object> read = null, Action<object, object> write = null)
        {
            _tree = tree;
            Path = path;
            Name = name;
            _member = member;
            Attributes = attributes;
            _parent = parent;
            _serialized = serialized;
            _injection = injection;
            _valueType = valueType;
            _read = read;
            _write = write;
        }

        internal Type ValueType => _valueType ?? (_member switch
        {
            FieldInfo field => field.FieldType,
            PropertyInfo property => property.PropertyType,
            _ => _serialized ? NodinMemberPath.For(Path).TypeOf(_tree.Target) : null
        });

        internal object OwnerOf(Object target) =>
            _serialized ? NodinMemberPath.For(Path).Member(target).owner ?? target :
            _parent == null ? target : _parent.Read(target);

        private object Read(Object target) => _serialized ? NodinMemberPath.For(Path).Get(target) : ReadMember(OwnerOf(target));

        private object ReadMember(object owner) => _read != null ? _read(owner) : _member switch
        {
            PropertyInfo property => property.GetValue(owner),
            FieldInfo field => field.GetValue(owner),
            _ => null
        };

        public object Value
        {
            get
            {
                var property = SerializedProperty;
                if (property?.propertyType == SerializedPropertyType.Enum && ValueType?.IsEnum == true)
                    return Enum.ToObject(ValueType, property.intValue);
                if (property != null && (!property.isArray || property.propertyType == SerializedPropertyType.String) && property.propertyType != SerializedPropertyType.Generic)
                    return property.boxedValue;
                return Read(_tree.Target);
            }
            set
            {
                if (_tree.EditsDisabled)
                    return;
                var property = SerializedProperty;
                if (property != null)
                {
                    var type = ValueType;
                    if (type != null)
                        value = NodinExpression.ConvertValue(value, type);
                    if (property.propertyType == SerializedPropertyType.Enum && value is Enum enumValue)
                        property.intValue = unchecked((int)Convert.ToInt64(enumValue));
                    else
                        property.boxedValue = value;
                }
                else if (CanWrite && !Equals(Value, value))
                    _tree.Perform("Edit " + Name, target => Write(target, value), IsStatic);
            }
        }

        private bool IsStatic => _member switch
        {
            FieldInfo field => field.IsStatic,
            PropertyInfo property => property.GetMethod.IsStatic,
            MethodInfo method => method.IsStatic,
            _ => false
        };

        private bool CanWrite => _serialized || _write != null || (_parent == null || _parent.ValueType?.IsValueType != true || _parent.CanWrite)
            && (_member is FieldInfo { IsInitOnly: false, IsLiteral: false } || _member is PropertyInfo { SetMethod: not null });

        private void Write(Object target, object value)
        {
            if (_serialized)
            {
                NodinMemberPath.For(Path).Set(target, value);
                return;
            }
            var owner = OwnerOf(target);
            value = NodinExpression.ConvertValue(value, ValueType);
            if (_write != null)
                _write(owner, value);
            else if (_member is FieldInfo field)
                field.SetValue(owner, value);
            else if (_member is PropertyInfo property)
                property.SetValue(owner, value);
            WriteBackOwner(target, owner);
        }

        private void WriteBackOwner(Object target, object owner)
        {
            if (owner?.GetType().IsValueType != true)
                return;
            if (_parent?.CanWrite == true)
                _parent.Write(target, owner);
            else if (_serialized && Path.LastIndexOf('.') is var dot && dot >= 0)
                NodinMemberPath.For(Path[..dot]).Set(target, owner);
        }

        internal NodinPropertyNode CollectionElement(int index, Type type, Func<object, object> read, Action<object, object> write = null) =>
            new(_tree, Path + $"[{index}]", $"Element {index}", null, Array.Empty<Attribute>(), this,
                valueType: type, read: read, write: write);

        private bool IsAncestorValue(object value)
        {
            for (var parent = _parent; parent != null; parent = parent._parent)
                if (ReferenceEquals(parent.Value, value))
                    return true;
            return false;
        }

        internal void Invoke(string method, params object[] args)
        {
            _tree.Perform(ObjectNames.NicifyVariableName(method), target =>
            {
                var owner = OwnerOf(target);
                NodinExpression.Call(owner, method, args);
                WriteBackOwner(target, owner);
            });
        }
    }
}
