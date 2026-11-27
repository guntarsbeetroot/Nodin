using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nodin.Editor
{
    public sealed partial class NodinPropertyNode
    {
        internal static PrefabKind ContextOf(Object target)
        {
            var gameObject = target is Component component ? component.gameObject : target as GameObject;
            if (!gameObject)
                return PrefabKind.NonPrefabInstance;
            var assetType = PrefabUtility.GetPrefabAssetType(gameObject);
            var stage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (stage != null && stage.IsPartOfPrefabContents(gameObject))
            {
                var nearest = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
                if (nearest != null && nearest != stage.prefabContentsRoot)
                    return PrefabKind.InstanceInPrefab;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
                return PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant
                    ? PrefabKind.Variant : PrefabKind.Regular;
            }
            if (EditorUtility.IsPersistent(gameObject))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(gameObject) &&
                    PrefabUtility.GetNearestPrefabInstanceRoot(gameObject) is { transform: { parent: not null } })
                    return PrefabKind.InstanceInPrefab;
                return assetType == PrefabAssetType.Variant ? PrefabKind.Variant : PrefabKind.Regular;
            }
            return PrefabUtility.IsPartOfPrefabInstance(gameObject)
                ? PrefabKind.InstanceInScene : PrefabKind.NonPrefabInstance;
        }

        private static bool IsIn(Object target, PrefabKind contexts) => (ContextOf(target) & contexts) != 0;

        private bool ContextVisible(Object target) => Attributes.All(attribute => attribute switch
        {
            ShowInAttribute show => IsIn(target, show.PrefabKind),
            HideInAttribute hide => !IsIn(target, hide.PrefabKind),
            ShowInInlineEditorsAttribute => _tree.IsInline,
            HideInInlineEditorsAttribute => !_tree.IsInline,
            HideInEditorModeAttribute => EditorApplication.isPlaying,
            HideInPlayModeAttribute => !EditorApplication.isPlaying,
            _ => true
        });

        private bool ContextDisabled(Object target) => Attributes.Any(attribute => attribute switch
        {
            EnableInAttribute enable => !IsIn(target, enable.PrefabKind),
            DisableInAttribute disable => IsIn(target, disable.PrefabKind),
            DisallowModificationsInAttribute disallow => IsIn(target, disallow.PrefabKind),
            DisableInInlineEditorsAttribute => _tree.IsInline,
            DisableInEditorModeAttribute => !EditorApplication.isPlaying,
            DisableInPlayModeAttribute => EditorApplication.isPlaying,
            _ => false
        });

        private void DrawAttributeValidation(GUIContent label)
        {
            var name = string.IsNullOrEmpty(label.text) ? ObjectNames.NicifyVariableName(Name) : label.text;
            if (GetAttribute<NodinPathAttribute>() is { RequireExistingPath: true } path &&
                !NodinValueControls.PathExists(this, Value as string, path))
                EditorGUILayout.HelpBox($"{name} must point to an existing {(path is FilePathAttribute ? "file" : "folder")}.", MessageType.Error);

            foreach (var required in Attributes.OfType<RequiredInAttribute>())
                if (_tree.Targets.Any(target => IsIn(target, required.PrefabKind) && IsMissing(Read(target))))
                    EditorGUILayout.HelpBox(Text(required.ErrorMessage) ?? $"{name} is required here.", MessageType.Error);

            foreach (var rule in Attributes.OfType<RequiredListLengthAttribute>())
                if (_tree.Targets.Any(target => IsIn(target, rule.PrefabKind) && InvalidLength(rule, target)))
                    EditorGUILayout.HelpBox(Text(rule.ErrorMessage) ?? $"{name} has an invalid item count.", MessageType.Error);

            foreach (var rule in Attributes.OfType<DisallowModificationsInAttribute>())
                if (_serialized && _tree.Targets.Any(target => IsIn(target, rule.PrefabKind) && HasOverride(target)))
                    EditorGUILayout.HelpBox(Text(rule.ErrorMessage) ?? $"{name} has a prefab override in a restricted context.", MessageType.Error);
        }

        private void DrawInputValidation(ValidateInputAttribute rule, GUIContent label)
        {
            foreach (var target in _tree.Targets)
            {
                var value = _tree.IsMulti ? Read(target) : Value;
                var owner = OwnerOf(target);
                var message = Text(rule.Message, owner) ?? $"{label.text ?? Name} is invalid.";
                var type = rule.MessageType;
                if (ValidateValue(owner, value, rule.Member, ref message, ref type))
                    continue;
                EditorGUILayout.HelpBox(message, (MessageType)type);
                break;
            }
        }

        private static bool ValidateValue(object owner, object value, string member, ref string message, ref InfoMessageType type)
        {
            if (member.StartsWith("@", StringComparison.Ordinal))
                return NodinExpression.Truth(NodinExpression.Evaluate(owner, member));
            for (var current = owner.GetType(); current != null; current = current.BaseType)
                foreach (var method in current.GetMethods(NodinExpression.Members))
                {
                    if (method.Name != member || method.ReturnType != typeof(bool) || method.ContainsGenericParameters)
                        continue;
                    var parameters = method.GetParameters();
                    if (parameters.Length is < 2 or > 3 || parameters[0].ParameterType.IsByRef ||
                        parameters[1].ParameterType != typeof(string).MakeByRefType() ||
                        parameters.Length == 3 && parameters[2].ParameterType != typeof(InfoMessageType).MakeByRefType())
                        continue;
                    object typed;
                    try { typed = NodinExpression.ConvertValue(value, parameters[0].ParameterType); }
                    catch (Exception exception) when (exception is InvalidCastException or FormatException or ArgumentException or OverflowException)
                    {
                        continue;
                    }
                    var args = parameters.Length == 3 ? new object[] { typed, message, type } : new[] { typed, message };
                    var valid = method.Invoke(owner, args) is true;
                    message = args[1] as string ?? message;
                    if (args.Length == 3 && args[2] is InfoMessageType resultType)
                        type = resultType;
                    return valid;
                }
            return NodinExpression.Truth(NodinExpression.Call(owner, member.TrimStart('$'), value));
        }

        private bool InvalidLength(RequiredListLengthAttribute rule, Object target)
        {
            var value = Read(target);
            if (value != null && (value is not IEnumerable || value is string))
                return false;
            var count = value is ICollection items ? items.Count : (value as IEnumerable)?.Cast<object>().Count() ?? 0;
            var owner = OwnerOf(target);
            return (rule.MinLength != null && count < LengthBound(rule.MinLength, owner)) ||
                   (rule.MaxLength != null && count > LengthBound(rule.MaxLength, owner));
        }

        private static int LengthBound(object bound, object owner) =>
            Convert.ToInt32(bound is string text ? NodinExpression.Evaluate(owner, text) : bound, CultureInfo.InvariantCulture);

        private bool HasOverride(Object target)
        {
            using var serialization = new SerializedObject(target);
            return serialization.FindProperty(Path)?.prefabOverride == true;
        }

        private void DrawTypeInfo(Type type, object owner)
        {
            if (type == null)
                return;
            foreach (var info in type.GetCustomAttributes<TypeInfoBoxAttribute>(true))
                EditorGUILayout.HelpBox(Text(info.Message, owner), MessageType.Info);
        }

        private void DrawDetailedInfo()
        {
            var index = 0;
            foreach (var box in Attributes.OfType<DetailedInfoBoxAttribute>())
            {
                var key = Path + ":details:" + index++;
                if (!string.IsNullOrEmpty(box.VisibleIf) && !NodinExpression.Truth(Resolve(box.VisibleIf)))
                    continue;
                using var scope = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                var expanded = _tree.State(key, false);
                _tree.SetState(key, EditorGUILayout.Foldout(expanded, Text(box.Message), true));
                if (expanded)
                    EditorGUILayout.HelpBox(Text(box.Details), (MessageType)box.MessageType);
            }
        }

        private void DrawPropertyContext(Rect rect)
        {
            if (Event.current.type != EventType.ContextClick || !rect.Contains(Event.current.mousePosition) ||
                _tree.EditsDisabled || GetAttribute<DisableContextMenuAttribute>()?.DisableForMember == true)
                return;
            var entries = Attributes.OfType<CustomContextMenuAttribute>().ToArray();
            if (entries.Length == 0 || !_tree.ShouldApply)
                return;

            var menu = new GenericMenu();
            var guard = NodinCollections.DeferredEditGuard(this, Path);
            foreach (var entry in entries)
                menu.AddItem(new GUIContent(Text(entry.MenuItem)), false, () =>
                {
                    if (guard() && !IsDisabled())
                        Invoke(entry.Action);
                });
            menu.ShowAsContext();
            Event.current.Use();
        }

        private void DrawDiagnostics()
        {
            if (GetAttribute<ShowPropertyResolverAttribute>() != null)
                EditorGUILayout.LabelField("Property source", _serialized ? "Unity serialization" :
                    _member is MethodInfo ? "Reflected method" : "Reflected value", EditorStyles.miniLabel);
            if (GetAttribute<ShowDrawerChainAttribute>() != null)
            {
                var attributes = Attributes.Where(attribute => attribute.GetType().Assembly == typeof(ShowInInspectorAttribute).Assembly)
                    .Select(attribute => attribute.GetType().Name.Replace("Attribute", ""));
                EditorGUILayout.LabelField(string.Join(" → ", attributes.Append(_serialized ? "Serialized value" : "Reflected value")),
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        internal bool HasCollectionCallbacks => Attributes.OfType<OnCollectionChangedAttribute>().Any();

        internal void CollectionCallback(Object target, CollectionChangeInfo change, bool before)
        {
            if (!HasCollectionCallbacks)
                return;
            var owner = OwnerOf(target);
            var collection = Read(target);
            foreach (var hook in Attributes.OfType<OnCollectionChangedAttribute>())
            {
                var action = before ? hook.Before : hook.After;
                if (string.IsNullOrWhiteSpace(action))
                    continue;
                if (action.StartsWith("@", StringComparison.Ordinal))
                    NodinExpression.Evaluate(owner, action);
                else
                    NodinExpression.Call(owner, action, change, collection);
                WriteBackOwner(target, owner);
            }
        }

        internal void SerializedCollectionCallback(CollectionChangeInfo change, bool before)
        {
            if (!HasCollectionCallbacks)
                return;
            var targets = _tree.Targets;
            if (before)
                Undo.RecordObjects(targets, "Edit " + Name);
            foreach (var target in targets)
            {
                CollectionCallback(target, change, before);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                EditorUtility.SetDirty(target);
            }
            // A clean wrapper can reload callback changes without discarding staged edits.
            if (!_tree.Serialization.hasModifiedProperties)
                _tree.Serialization.Update();
        }

        internal void EditReflectedCollection(CollectionChangeInfo change, Action<object> edit)
        {
            if (_tree.IsMulti || _tree.EditsDisabled || !_tree.ShouldApply)
                return;
            _tree.Perform("Edit " + Name, target =>
            {
                CollectionCallback(target, change, true);
                edit(Read(target));
                CollectionCallback(target, change, false);
            }, IsStatic);
        }

        internal void NotifySerializedCollectionElement()
        {
            if (!_serialized || !_tree.ShouldApply || _tree.EditsDisabled || !_tree.Serialization.hasModifiedProperties)
                return;
            const string item = ".Array.data[";
            for (var start = Path.LastIndexOf(item, StringComparison.Ordinal); start >= 0;
                 start = start == 0 ? -1 : Path.LastIndexOf(item, start - 1, StringComparison.Ordinal))
            {
                var node = _tree.GetPropertyAtUnityPath(Path[..start]);
                if (node?.HasCollectionCallbacks != true)
                    continue;
                var number = start + item.Length;
                var index = int.Parse(Path.Substring(number, Path.IndexOf(']', number) - number), CultureInfo.InvariantCulture);
                var change = new CollectionChangeInfo(CollectionChangeType.Replace, index);
                var staged = DetachPendingElementEdit(out var value);
                node.SerializedCollectionCallback(change, true);
                if (staged)
                {
                    var current = SerializedProperty;
                    if (current == null)
                        return;
                    current.boxedValue = value;
                }
                _tree.ApplyModifiedProperties();
                node.SerializedCollectionCallback(change, false);
                return;
            }
        }

        private bool DetachPendingElementEdit(out object editedValue)
        {
            editedValue = null;
            var staged = SerializedProperty;
            if (_tree.IsMulti || staged == null || staged.isArray ||
                staged.propertyType is SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference)
                return false;

            using var original = new SerializedObject(_tree.Target);
            var previous = original.FindProperty(Path);
            if (previous == null)
                return false;
            try
            {
                // Restore the value in the buffer so the before-hook can edit the live object.
                editedValue = staged.boxedValue;
                staged.boxedValue = previous.boxedValue;
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
            {
                return false;
            }
            _tree.ApplyModifiedProperties();
            return true;
        }

        internal void UpdateLifecycle()
        {
            if (!_tree.ShouldApply)
                return;
            var initializers = Attributes.OfType<OnInspectorInitAttribute>().Where(hook => hook.Action != null).ToArray();
            var disposers = Attributes.OfType<OnInspectorDisposeAttribute>().Where(hook => hook.Action != null).ToArray();
            if (initializers.Length == 0 && disposers.Length == 0)
                return;
            foreach (var target in _tree.Targets)
            {
                var owner = OwnerOf(target);
                if (owner == null)
                    continue;
                var type = Read(target)?.GetType();
                var ownerIsStruct = owner.GetType().IsValueType;
                bool IsCurrent()
                {
                    if (!target || _serialized && _tree.Serialization.FindProperty(Path) == null)
                        return false;
                    try
                    {
                        var current = OwnerOf(target);
                        return (ownerIsStruct ? current?.GetType() == owner.GetType() : ReferenceEquals(current, owner)) &&
                               Read(target)?.GetType() == type;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        return false;
                    }
                }

                _tree.TrackPropertyLifecycle(Path, target, IsCurrent,
                    () => RunLifecycleActions(target, owner, true, initializers.Select(hook => hook.Action)),
                    () =>
                    {
                        var current = IsCurrent();
                        if (!ownerIsStruct || current)
                            RunLifecycleActions(target, ownerIsStruct ? OwnerOf(target) : owner, current,
                                disposers.Select(hook => hook.Action));
                    });
            }
        }

        private void RunLifecycleActions(Object target, object owner, bool writeBack, IEnumerable<string> actions)
        {
            foreach (var action in actions)
            {
                try
                {
                    NodinPropertyTree.RunAttributeAction(owner, action);
                    if (writeBack && target && owner.GetType().IsValueType)
                        WriteBackOwner(target, owner);
                    if (writeBack && target && !_tree.Serialization.hasModifiedProperties)
                        _tree.Serialization.Update();
                }
                catch (Exception exception) when (exception is not ExitGUIException && exception.InnerException is not ExitGUIException)
                {
                    Debug.LogException(exception.InnerException ?? exception, target);
                }
            }
        }

        private NodinPropertyState StateFor(Object target)
        {
            if (!_tree.ShouldApply || GetAttribute<OnStateUpdateAttribute>() == null)
                return null;
            return _tree.AttributeState(Path, target, state =>
            {
                var owner = OwnerOf(target);
                foreach (var hook in Attributes.OfType<OnStateUpdateAttribute>())
                {
                    var action = hook.Action;
                    if (string.IsNullOrWhiteSpace(action))
                        continue;
                    if (!action.StartsWith("@", StringComparison.Ordinal))
                    {
                        NodinExpression.Call(owner, action.TrimStart('$'), state);
                        continue;
                    }
                    var assignment = action.IndexOf('=');
                    if (assignment > 0 && action.StartsWith("@$property.State.", StringComparison.Ordinal))
                    {
                        var member = action["@$property.State.".Length..assignment].Trim();
                        var expression = action[(assignment + 1)..].Trim();
                        var value = NodinExpression.Truth(expression == "$value" ? Read(target) :
                            NodinExpression.Evaluate(owner, expression));
                        switch (member)
                        {
                            case nameof(state.Visible): state.Visible = value; break;
                            case nameof(state.Enabled): state.Enabled = value; break;
                            case nameof(state.Expanded): state.Expanded = value; break;
                            default: throw new ArgumentException($"Unknown property state member '{member}'.");
                        }
                    }
                    else
                        NodinExpression.Evaluate(owner, action);
                }
            });
        }

        internal bool? ExpandedByState => StateFor(_tree.Target)?.Expanded;

        private void DrawDuplicateReference(GUIContent label, string previous)
        {
            EditorGUILayout.LabelField(label, new GUIContent($"Reference to {previous}"), EditorStyles.miniLabel);
        }

        private bool IsDuplicateReference(object value, out string previous)
        {
            previous = null;
            if (value == null || value is Object or string || value.GetType().IsValueType)
                return false;
            if (IsAncestorValue(value))
            {
                previous = "parent";
                return true;
            }
            previous = _tree.PreviousReferencePath(value, Path);
            return previous != null;
        }

        internal bool CanDrawCollectionContext => GetAttribute<DisableContextMenuAttribute>()?.DisableCollectionElements != true;
    }
    public sealed partial class NodinPropertyTree
    {
        private readonly Dictionary<(string, Object), (Func<bool> IsCurrent, Action Dispose)> _propertyLifecycles = new();
        private readonly Dictionary<(string, Object), (NodinPropertyState State, Exception Error)> _attributeStates = new();
        private readonly Dictionary<object, string> _drawnReferences = new(new ReferenceComparer());
        private static readonly Dictionary<Type, MethodInfo[]> LifecycleMethods = new();
        private bool _startedLifecycle;

        internal bool IsInline => ActiveInlineEditors.Contains(Target);

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            bool IEqualityComparer<object>.Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        internal string PreviousReferencePath(object value, string path)
        {
            if (_drawnReferences.TryGetValue(value, out var previous) && previous != path)
                return previous;
            _drawnReferences[value] = path;
            return null;
        }

        private static MethodInfo[] GetLifecycleMethods(Type type)
        {
            if (LifecycleMethods.TryGetValue(type, out var methods))
                return methods;
            var found = new List<MethodInfo>();
            for (var current = type; current != null && current != typeof(Object); current = current.BaseType)
                found.AddRange(current.GetMethods(NodinExpression.Members).Where(method =>
                    !method.ContainsGenericParameters && method.GetParameters().Length == 0 &&
                    (method.IsDefined(typeof(OnInspectorInitAttribute), true) ||
                     method.IsDefined(typeof(OnInspectorDisposeAttribute), true))));
            methods = found.GroupBy(MemberKey).Select(group => group.First()).ToArray();
            LifecycleMethods.Add(type, methods);
            return methods;
        }

        private static bool HasTypeCustomization(Type type) =>
            type.IsDefined(typeof(SearchableAttribute), true) ||
            type.IsDefined(typeof(HideMonoScriptAttribute), true) ||
            type.IsDefined(typeof(HideNetworkBehaviourFieldsAttribute), true) ||
            type.IsDefined(typeof(TypeInfoBoxAttribute), true) ||
            GetLifecycleMethods(type).Length != 0;

        private bool ShowRootProperty(string name)
        {
            var type = Target.GetType();
            if (name == "m_Script")
                return Target is not EditorWindow && !type.IsDefined(typeof(HideMonoScriptAttribute), true);
            if (!type.IsDefined(typeof(HideNetworkBehaviourFieldsAttribute), true))
                return true;
            for (var current = type; current != null; current = current.BaseType)
                if (current.FullName == "UnityEngine.Networking.NetworkBehaviour")
                    return name is not ("m_NetworkChannel" or "m_SendInterval" or "m_NetworkSendInterval");
            return true;
        }

        private void BeginAttributeDraw()
        {
            _attributeStates.Clear();
            _drawnReferences.Clear();
            if (_startedLifecycle || !ShouldApply)
                return;
            _startedLifecycle = true;
            RunRootLifecycle(true);
            Serialization.UpdateIfRequiredOrScript();
        }

        private void RunRootLifecycle(bool init)
        {
            var statics = new HashSet<MethodInfo>();
            foreach (var target in Targets.Where(target => target))
                foreach (var method in GetLifecycleMethods(target.GetType()))
                {
                    var actions = init
                        ? method.GetCustomAttributes<OnInspectorInitAttribute>(true).Select(hook => hook.Action)
                        : method.GetCustomAttributes<OnInspectorDisposeAttribute>(true).Select(hook => hook.Action);
                    if (method.IsStatic && !statics.Add(method))
                        continue;
                    foreach (var action in actions)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(action))
                                method.Invoke(target, null);
                            else
                                RunAttributeAction(target, action);
                        }
                        catch (Exception exception) when (exception is not ExitGUIException && exception.InnerException is not ExitGUIException)
                        {
                            Debug.LogException(exception.InnerException ?? exception, target);
                        }
                    }
                }
        }

        private void DisposeAttributeDraw()
        {
            foreach (var hook in _propertyLifecycles.Values.ToArray())
                hook.Dispose();
            _propertyLifecycles.Clear();
            _attributeStates.Clear();
            _drawnReferences.Clear();
            if (_startedLifecycle)
                RunRootLifecycle(false);
        }

        internal static void RunAttributeAction(object owner, string action)
        {
            if (action.StartsWith("@", StringComparison.Ordinal))
                NodinExpression.Evaluate(owner, action);
            else
                NodinExpression.Call(owner, action.TrimStart('$'));
        }

        private void RetirePropertyLifecycles()
        {
            foreach (var item in _propertyLifecycles.ToArray())
            {
                if (item.Value.IsCurrent())
                    continue;
                _propertyLifecycles.Remove(item.Key);
                item.Value.Dispose();
            }
        }

        internal void TrackPropertyLifecycle(string path, Object target, Func<bool> isCurrent, Action init, Action dispose)
        {
            var key = (path, target);
            if (_propertyLifecycles.TryGetValue(key, out var existing))
            {
                if (existing.IsCurrent())
                    return;
                existing.Dispose();
            }
            _propertyLifecycles[key] = (isCurrent, dispose);
            init();
        }

        internal NodinPropertyState AttributeState(string path, Object target, Action<NodinPropertyState> update)
        {
            var key = (path, target);
            if (!_attributeStates.TryGetValue(key, out var entry))
            {
                var state = new NodinPropertyState();
                Exception error = null;
                try { update(state); }
                catch (Exception exception) when (exception is not ExitGUIException && exception.InnerException is not ExitGUIException)
                {
                    error = exception;
                }
                entry = (state, error);
                _attributeStates[key] = entry;
            }
            if (entry.Error != null)
                throw new InvalidOperationException(entry.Error.Message, entry.Error);
            return entry.State;
        }
    }
}
