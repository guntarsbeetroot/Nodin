using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nodin.Editor
{
    public sealed partial class NodinPropertyNode
    {
        internal NodinPropertyTree Tree => _tree;
        internal float Order => GetAttribute<PropertyOrderAttribute>()?.Order ?? (Name == "m_Script" ? float.MinValue : 0);
        private object Resolve(string reference, object owner = null) => NodinExpression.Evaluate(owner ?? Owner, reference);
        internal object GroupOwner => GroupOwnerOf(_tree.Target);
        internal object GroupOwnerOf(Object target) => this == _tree.RootProperty ? target : Read(target) ?? OwnerOf(target);

        public void Draw() => Draw(null);

        private bool CanInteract => CanWrite || GetAttribute<EnableGUIAttribute>() != null;

        internal GUIContent InspectorLabel => Label();
        internal GUIContent TableLabel => Label(honorHideLabel: false);

        internal void Draw(GUIContent overrideLabel)
        {
            if (this == _tree.RootProperty)
            {
                var search = _tree.Target.GetType().GetCustomAttribute<SearchableAttribute>(true);
                DrawTypeInfo(_tree.Target.GetType(), _tree.Target);
                var children = Children;
                if (search == null)
                    NodinInspectorLayout.Draw(this, children);
                else
                {
                    children.FirstOrDefault(child => child.Name == "m_Script")?.Draw();
                    NodinInspectorLayout.Draw(this, NodinCollections.FilterChildren(this,
                        children.Where(child => child.Name != "m_Script"), search));
                }
                return;
            }
            if (!IsVisible())
                return;

            var label = overrideLabel ?? Label();
            var beforeColor = GUI.color;
            var beforeWidth = EditorGUIUtility.labelWidth;
            try
            {
                using var indent = new EditorGUI.IndentLevelScope(GetAttribute<IndentAttribute>()?.IndentLevel ?? 0);
                _ = StateFor(_tree.Target);
                UpdateLifecycle();
                if (GetAttribute<GUIColorAttribute>() is { } color)
                    GUI.color *= color.Member == null ? color.Color : (Color)Resolve(color.Member);
                if (GetAttribute<LabelWidthAttribute>() is { } width)
                    EditorGUIUtility.labelWidth = width.Width;

                var previous = _tree.EditsDisabled;
                _tree.EditsDisabled = previous || IsDisabled();
                try
                {
                    if (GetAttribute<CustomContextMenuAttribute>() == null)
                        DrawContent(label);
                    else
                    {
                        using var scope = new EditorGUILayout.VerticalScope();
                        DrawPropertyContext(scope.rect);
                        DrawContent(label);
                    }
                }
                finally { _tree.EditsDisabled = previous; }
            }
            catch (Exception e) when (e is not ExitGUIException && e.InnerException is not ExitGUIException)
            {
                if (GetAttribute<SuppressInvalidAttributeErrorAttribute>() == null)
                    EditorGUILayout.HelpBox($"{Name}: {(e.InnerException ?? e).Message}", MessageType.Error);
            }
            finally
            {
                GUI.color = beforeColor;
                EditorGUIUtility.labelWidth = beforeWidth;
            }
        }

        internal bool IsVisible()
        {
            try
            {
                return _tree.Targets.Any(target => StateFor(target)?.Visible != false && ContextVisible(target) && Attributes.All(attribute => attribute switch
                {
                    ShowIfAttribute show => Matches(show, target),
                    HideIfAttribute hide => !Matches(hide, target),
                    _ => true
                }));
            }
            catch (Exception)
            {
                // Keep a failed condition in the layout so the user can see the diagnostic.
                return true;
            }
        }

        private bool IsDisabled()
        {
            foreach (var target in _tree.Targets)
            {
                if (!ContextVisible(target) || ContextDisabled(target) || StateFor(target)?.Enabled == false)
                    return true;
                foreach (var condition in Attributes.OfType<NodinConditionAttribute>())
                {
                    var match = Matches(condition, target);
                    if (condition is ShowIfAttribute or EnableIfAttribute ? !match : match)
                        return true;
                }
            }

            return GetAttribute<ReadOnlyAttribute>() != null || Name == "m_Script" ||
                GetAttribute<ListDrawerSettingsAttribute>()?.IsReadOnly == true || GetAttribute<TableListAttribute>()?.IsReadOnly == true ||
                _member is MethodInfo { IsStatic: false } && Owner?.GetType().IsValueType == true && _parent?.CanWrite != true ||
                _tree.IsMulti && !_serialized && _member is not MethodInfo && _injection == null ||
                GetAttribute<EnableGUIAttribute>() == null && _member is not MethodInfo && _injection == null && !CanWrite &&
                (ValueType == null || ValueType.IsValueType || ValueType == typeof(string));
        }

        private bool Matches(NodinConditionAttribute condition, Object target)
        {
            var value = Resolve(condition.Condition, OwnerOf(target));
            return condition.HasValue ? NodinExpression.Equal(value, condition.Value) : NodinExpression.Truth(value);
        }

        internal string Text(string text, object owner = null)
        {
            if (string.IsNullOrEmpty(text) || text[0] is not ('@' or '$'))
                return text;
            return NodinExpression.TryEvaluate(owner ?? Owner, text, out var value, out var error)
                ? value?.ToString() ?? "" : $"{text} ({error})";
        }

        private GUIContent Label(bool honorHideLabel = true)
        {
            if (honorHideLabel && GetAttribute<HideLabelAttribute>() != null)
                return GUIContent.none;
            return new GUIContent(Text(GetAttribute<LabelTextAttribute>()?.Text) ??
                SerializedProperty?.displayName ?? ObjectNames.NicifyVariableName(Name),
                Text(GetAttribute<PropertyTooltipAttribute>()?.Message) ?? GetAttribute<TooltipAttribute>()?.tooltip ?? SerializedProperty?.tooltip);
        }

        private void DrawContent(GUIContent label)
        {
            var spacing = GetAttribute<PropertySpaceAttribute>();
            if (spacing?.Before > 0)
                GUILayout.Space(spacing.Before);

            if (GetAttribute<TitleAttribute>() is { } title)
                DrawTitle(Text(title.Text), Text(title.Subtitle), title.Alignment, title.HorizontalLine, title.Bold);

            foreach (var box in Attributes.OfType<InfoBoxAttribute>())
                if (string.IsNullOrEmpty(box.VisibleIf) || NodinExpression.Truth(Resolve(box.VisibleIf)))
                    EditorGUILayout.HelpBox(Text(box.Message), (MessageType)box.MessageType);

            DrawDetailedInfo();
            DrawDiagnostics();

            if (_injection != null)
                _injection(_tree.Target);
            else if (_member is MethodInfo method)
            {
                using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                DrawMethod(method, label);
            }
            else
                DrawValueAndActions(label);

            if (spacing?.After > 0)
                GUILayout.Space(spacing.After);
        }

        internal static void DrawTitle(string title, string subtitle, TitleAlignments alignment = TitleAlignments.Left,
            bool horizontalLine = true, bool bold = true)
        {
            GUILayout.Space(3);
            var titleStyle = bold ? EditorStyles.boldLabel : EditorStyles.label;
            var subtitleStyle = EditorStyles.wordWrappedMiniLabel;
            if (alignment is TitleAlignments.Centered or TitleAlignments.Right)
            {
                var anchor = alignment == TitleAlignments.Centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight;
                titleStyle = new GUIStyle(titleStyle) { alignment = anchor };
                subtitleStyle = new GUIStyle(subtitleStyle) { alignment = anchor };
            }
            if (alignment == TitleAlignments.Split && !string.IsNullOrEmpty(subtitle))
            {
                using var row = new EditorGUILayout.HorizontalScope();
                EditorGUILayout.LabelField(title, titleStyle);
                EditorGUILayout.LabelField(subtitle, new GUIStyle(subtitleStyle) { alignment = TextAnchor.MiddleRight });
            }
            else
            {
                EditorGUILayout.LabelField(title, titleStyle);
                if (!string.IsNullOrEmpty(subtitle))
                    EditorGUILayout.LabelField(subtitle, subtitleStyle);
            }
            if (horizontalLine)
                NodinInspectorGUI.HorizontalLineSeparator();
        }

        private void DrawMethod(MethodInfo method, GUIContent content)
        {
            var hasButton = GetAttribute<ButtonAttribute>() is { } || GetAttribute<ButtonGroupAttribute>() != null || GetAttribute<ResponsiveButtonGroupAttribute>() != null;
            if (!hasButton && GetAttribute<OnInspectorGUIAttribute>() != null)
            {
                if (method.GetParameters().Length == 0)
                    method.Invoke(Owner, null);
                return;
            }

            var button = GetAttribute<ButtonAttribute>();
            var label = Text(button?.Name) ?? (content == GUIContent.none ? ObjectNames.NicifyVariableName(Name) : content.text);
            var parameters = method.GetParameters();
            if (parameters.Any(parameter => parameter.ParameterType.IsByRef || parameter.IsOut))
            {
                EditorGUILayout.HelpBox($"{label} uses ref or out parameters.", MessageType.Info);
                return;
            }

            var args = _tree.State(Path + ":args", parameters.Select(parameter =>
                parameter.HasDefaultValue ? parameter.DefaultValue : Default(parameter.ParameterType)).ToArray());
            if (parameters.Length > 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var parameter = parameters[i];
                        args[i] = NodinMethodArguments.Draw(_tree, Path + $":parameter:{i}",
                            new GUIContent(ObjectNames.NicifyVariableName(parameter.Name)), parameter.ParameterType, args[i]);
                    }
                    DrawMethodButton(method, label, button, args);
                }
            }
            else
                DrawMethodButton(method, label, button, args);
        }

        private void DrawMethodButton(MethodInfo method, string label, ButtonAttribute button, object[] args)
        {
            var height = button?.Height > 0 ? button.Height : button?.Size switch
            {
                ButtonSizes.Medium => 24, ButtonSizes.Large => 30,
                ButtonSizes.ExtraLarge => 38, ButtonSizes.Gigantic => 48, _ => EditorGUIUtility.singleLineHeight
            };
            if (!GUILayout.Button(label, GUILayout.MinHeight(height)))
                return;

            _tree.Perform(ObjectNames.NicifyVariableName(Name), target =>
            {
                var owner = OwnerOf(target);
                method.Invoke(owner, args);
                WriteBackOwner(target, owner);
            }, method.IsStatic);
        }

        private static object Default(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

        private void DrawValueAndActions(GUIContent label)
        {
            var hook = GetAttribute<OnInspectorGUIAttribute>();
            if (hook?.Append != true)
                DrawGuiHook(hook?.Action);

            var callbacks = Attributes.OfType<OnValueChangedAttribute>().ToArray();
            var before = callbacks.Length == 0 ? null : SnapshotValues();
            var inline = Attributes.OfType<InlineButtonAttribute>().ToArray();
            var suffix = GetAttribute<SuffixLabelAttribute>();
            var sideBySide = inline.Length > 0 || suffix is { Overlay: false };

            if (sideBySide)
                EditorGUILayout.BeginHorizontal();

            try
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    DrawValue(label);
                    if (suffix is { Overlay: true } && Event.current.type == EventType.Repaint)
                    {
                        var rect = GUILayoutUtility.GetLastRect();
                        rect.xMax -= 6;
                        GUI.Label(rect, Text(suffix.Label), NodinValueControls.OverlayLabel);
                    }
                    if (check.changed)
                    {
                        ApplyNumericConstraints();
                        NotifySerializedCollectionElement();
                        if (before != null)
                            NotifyChanged(before);
                    }
                }

                if (suffix is { Overlay: false })
                    GUILayout.Label(Text(suffix.Label), EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                foreach (var button in inline)
                    if (GUILayout.Button(Text(button.Label) ?? ObjectNames.NicifyVariableName(button.Action),
                            GUILayout.ExpandWidth(false)))
                        Invoke(button.Action);
            }
            finally
            {
                if (sideBySide)
                    EditorGUILayout.EndHorizontal();
            }

            DrawGuiHook(hook?.Append == true ? hook.Action : hook?.AfterAction);

            if (GetAttribute<RequiredAttribute>() is { } required && IsMissing(Value))
                EditorGUILayout.HelpBox(Text(required.Message) ?? $"{label.text ?? Name} is required.", MessageType.Error);

            if (GetAttribute<ValidateInputAttribute>() is { } validate)
                DrawInputValidation(validate, label);

            DrawAttributeValidation(label);
        }

        private void DrawGuiHook(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;
            using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
            NodinPropertyTree.RunAttributeAction(Owner, action);
        }

        private static bool IsMissing(object value) => NodinExpression.IsNull(value) ||
            value is string text && string.IsNullOrWhiteSpace(text) ||
            value is ICollection collection && collection.Count == 0;

        internal Dictionary<Object, object> SnapshotValues()
        {
            if (!Attributes.OfType<OnValueChangedAttribute>().Any() || !_tree.ShouldApply || _tree.EditsDisabled)
                return null;
            return _tree.Targets.ToDictionary(target => target, ReadSnapshot);
        }

        private object ReadSnapshot(Object target)
        {
            if (!_serialized)
                return Read(target);
            using var serialization = new SerializedObject(target);
            return serialization.FindProperty(Path)?.contentHash;
        }

        internal void NotifyChanged(Dictionary<Object, object> before)
        {
            if (before == null || !_tree.ShouldApply)
                return;
            if (_serialized)
                _tree.ApplyModifiedProperties();

            var targets = before.Where(pair => !Equals(pair.Value, ReadSnapshot(pair.Key)))
                .Select(pair => pair.Key).ToArray();
            if (targets.Length == 0)
                return;

            var callbacks = Attributes.OfType<OnValueChangedAttribute>().ToArray();
            _tree.Perform("Edit " + Name, targets, target =>
            {
                var owner = OwnerOf(target);
                foreach (var callback in callbacks)
                    NodinExpression.Call(owner, callback.Action);
                WriteBackOwner(target, owner);
            });
        }

        private void DrawValue(GUIContent label)
        {
            var property = SerializedProperty;
            var mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property?.hasMultipleDifferentValues ?? false;
            try
            {
                if (property != null && GetAttribute<SuppressInvalidAttributeErrorAttribute>() != null &&
                    property.propertyType is not (SerializedPropertyType.Float or SerializedPropertyType.Integer) &&
                    GetAttribute<RangeAttribute>() != null)
                {
                    DrawCustom(property, label, content =>
                    {
                        using var scope = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                        using var change = new EditorGUI.ChangeCheckScope();
                        var edited = NodinValueControls.Reflection(content, ValueType, Value, !EditorUtility.IsPersistent(_tree.Target));
                        if (change.changed)
                            Value = edited;
                        return true;
                    });
                    return;
                }
                if (property != null && GetAttribute<DrawWithUnityAttribute>() != null)
                {
                    using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                    EditorGUILayout.PropertyField(property, label, true);
                    return;
                }
                if (NodinInspectorExtensions.HasDrawer(this))
                {
                    using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                    if (DrawCustom(property, label, content => NodinInspectorExtensions.Draw(this, content)))
                        return;
                }
                if (GetAttribute<CustomValueDrawerAttribute>() is { } custom)
                {
                    using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                    DrawCustom(property, label, content =>
                    {
                        using var change = new EditorGUI.ChangeCheckScope();
                        var previous = Value;
                        var edited = NodinExpression.Call(Owner, custom.Member, previous, content);
                        if (change.changed || !Equals(previous, edited))
                            Value = edited;
                        return true;
                    });
                    return;
                }

                if (GetAttribute<DisplayAsStringAttribute>() is { } display)
                {
                    NodinValueControls.Display(label, Value, display.Overflow);
                    return;
                }

                using (new EditorGUI.DisabledScope(_tree.EditsDisabled || !CanInteract))
                    if (DrawNumericControl(property, label))
                        return;

                if (property == null)
                {
                    DrawReflected(label);
                    return;
                }

                DrawSerialized(property, label);
            }
            finally
            {
                EditorGUI.showMixedValue = mixed;
            }
        }

        private static bool DrawCustom(SerializedProperty property, GUIContent label, Func<GUIContent, bool> draw)
        {
            if (property == null)
                return draw(label);
            var rect = EditorGUILayout.BeginVertical();
            try
            {
                var content = EditorGUI.BeginProperty(rect, label, property);
                try { return draw(content); }
                finally { EditorGUI.EndProperty(); }
            }
            finally { EditorGUILayout.EndVertical(); }
        }

        private void DrawSerialized(SerializedProperty property, GUIContent label)
        {
            if (DrawReferenceControl(property, label))
                return;

            if (property.isArray && !property.isFixedBuffer && property.propertyType != SerializedPropertyType.String &&
                (GetAttribute<ListDrawerSettingsAttribute>() != null || GetAttribute<TableListAttribute>() != null ||
                 GetAttribute<SearchableAttribute>() != null || GetAttribute<ValueDropdownAttribute>() != null ||
                 GetAttribute<PreviewFieldAttribute>() != null || GetAttribute<InlineEditorAttribute>() != null ||
                 GetAttribute<AssetsOnlyAttribute>() != null || GetAttribute<SceneObjectsOnlyAttribute>() != null ||
                 HasReferenceCollectionOptions() || HasCollectionCallbacks ||
                 _tree.EditsDisabled || _tree.HasNestedCustomization(ValueType) ||
                 !_tree.IsMulti && !Attributes.OfType<PropertyAttribute>()
                     .Any(attribute => attribute is not TooltipAttribute and not NonReorderableAttribute)))
            {
                NodinCollections.Draw(this, property, label);
                return;
            }

            if (GetAttribute<ValueDropdownAttribute>() is { } dropdown)
            {
                using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                NodinCollections.DrawDropdown(this, property, label, dropdown);
                return;
            }

            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                (GetAttribute<PreviewFieldAttribute>() != null || GetAttribute<InlineEditorAttribute>() != null ||
                 GetAttribute<AssetsOnlyAttribute>() != null || GetAttribute<SceneObjectsOnlyAttribute>() != null ||
                 HasReferenceObjectOptions()))
            {
                DrawObject(property, label);
                return;
            }

            if (property.propertyType == SerializedPropertyType.String &&
                (GetAttribute<NodinPathAttribute>() != null || GetAttribute<MultiLinePropertyAttribute>() != null))
            {
                using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                NodinValueControls.String(this, property, label);
                return;
            }

            if (property.propertyType == SerializedPropertyType.Boolean && GetAttribute<ToggleLeftAttribute>() != null)
            {
                using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                using var change = new EditorGUI.ChangeCheckScope();
                var rect = EditorGUILayout.GetControlRect();
                var content = EditorGUI.BeginProperty(rect, label, property);
                try
                {
                    var next = EditorGUI.ToggleLeft(rect, content, property.boolValue);
                    if (change.changed)
                        property.boolValue = next;
                }
                finally { EditorGUI.EndProperty(); }
                return;
            }

            if (property.propertyType == SerializedPropertyType.Enum && GetAttribute<EnumToggleButtonsAttribute>() != null &&
                ValueType?.IsEnum == true)
            {
                using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                NodinValueControls.EnumButtons(this, property, label, ValueType);
                return;
            }

            if (property.propertyType is SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference &&
                (GetAttribute<InlinePropertyAttribute>() != null || GetAttribute<ToggleAttribute>() != null ||
                 GetAttribute<SearchableAttribute>() != null || _tree.EditsDisabled || _tree.HasNestedCustomization(ValueType)))
            {
                DrawNested(property, label);
                return;
            }

            using (new EditorGUI.DisabledScope(_tree.EditsDisabled))
                EditorGUILayout.PropertyField(property, label, true);
        }

        private void DrawReflected(GUIContent label)
        {
            if (DrawReferenceControl(null, label))
                return;
            var value = Value;
            if (value != null && IsAncestorValue(value))
            {
                DrawDuplicateReference(label, "parent");
                return;
            }
            if (value is Array { Rank: 2 } matrix && GetAttribute<TableMatrixAttribute>() is { } matrixOptions)
            {
                NodinCollections.DrawMatrix(this, matrix, label, matrixOptions);
                return;
            }
            if (value is IDictionary dictionary)
            {
                NodinCollections.DrawDictionary(this, dictionary, label);
                return;
            }
            if (value != null && NodinCollections.DrawSetOrList(this, value, label))
                return;

            var valueType = value?.GetType() ?? ValueType;
            if (valueType == typeof(string) && GetAttribute<NodinPathAttribute>() is { } path)
            {
                using var disabledPath = new EditorGUI.DisabledScope(_tree.EditsDisabled || !CanInteract);
                using var row = new EditorGUILayout.HorizontalScope();
                using var check = new EditorGUI.ChangeCheckScope();
                var text = EditorGUILayout.TextField(label, value as string ?? "");
                var changedPath = check.changed;
                if (GUILayout.Button(new GUIContent("…", "Browse"), GUILayout.Width(29)) &&
                    NodinValueControls.ChoosePath(this, text, path) is { } selected)
                {
                    text = selected;
                    changedPath = true;
                }
                if (changedPath)
                    Value = text;
                return;
            }
            if (valueType == typeof(string) && GetAttribute<MultiLinePropertyAttribute>() is { } multiline)
            {
                using var textDisabled = new EditorGUI.DisabledScope(_tree.EditsDisabled || !CanInteract);
                if (label != GUIContent.none)
                    EditorGUILayout.LabelField(label);
                using var check = new EditorGUI.ChangeCheckScope();
                var nextText = EditorGUILayout.TextArea(value as string ?? "",
                    GUILayout.MinHeight(multiline.Lines * EditorGUIUtility.singleLineHeight));
                if (check.changed)
                    Value = nextText;
                return;
            }
            if (value != null && value is not Object && valueType != typeof(string) && !valueType.IsPrimitive &&
                !valueType.IsEnum && Children.Count > 0)
            {
                DrawNested(null, label);
                return;
            }

            using var changed = new EditorGUI.ChangeCheckScope();
            using var disabled = new EditorGUI.DisabledScope(!CanInteract || _tree.EditsDisabled);
            var next = NodinValueControls.Reflection(label, valueType, value,
                GetAttribute<AssetsOnlyAttribute>() == null);
            if (changed.changed && CanWrite && (next is not Object obj || IsAllowedReference(obj) &&
                (GetAttribute<SceneObjectsOnlyAttribute>() == null || !EditorUtility.IsPersistent(obj))))
                Value = next;
        }

        private void DrawNested(SerializedProperty property, GUIContent label)
        {
            var value = Value;
            if (IsDuplicateReference(value, out var previousReference) &&
                (previousReference == "parent" || GetAttribute<HideDuplicateReferenceBoxAttribute>() == null))
            {
                DrawDuplicateReference(label, previousReference);
                return;
            }
            DrawTypeInfo(value?.GetType() ?? ValueType, value ?? Owner);
            var inlineOptions = GetAttribute<InlinePropertyAttribute>();
            var inline = inlineOptions != null;
            if (inlineOptions?.LabelWidth > 0)
                EditorGUIUtility.labelWidth = inlineOptions.LabelWidth;
            var toggle = GetAttribute<ToggleAttribute>();
            var toggleChild = toggle != null ? Children.FirstOrDefault(child => child.Name == toggle.Member) : null;
            var hasToggle = toggleChild?.Value is bool;
            var key = Path + ":nested";
            var expanded = inline || (ExpandedByState ?? property?.isExpanded ?? _tree.State(key, false));
            var next = expanded;
            if (!inline && label == GUIContent.none)
                label = new GUIContent(ObjectNames.NicifyVariableName(Name));

            if (hasToggle)
                next = DrawToggleHeader(toggleChild, label, inline, expanded);
            else if (!inline)
                next = EditorGUILayout.Foldout(expanded, label, true);
            else if (label != GUIContent.none)
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (next != expanded)
            {
                if (property != null)
                    property.isExpanded = next;
                else
                    _tree.SetState(key, next);
                // Keep the layout from the current event until the next Layout pass.
                if (Event.current.type != EventType.Used)
                    expanded = next;
            }

            if (expanded)
            {
                if (hasToggle)
                    GUILayout.Space(3);
                using var indent = new EditorGUI.IndentLevelScope(inline ? 0 : 1);
                var children = Children.Where(child => !hasToggle || child.Path != toggleChild.Path);
                if (GetAttribute<SearchableAttribute>() is { } search)
                    children = NodinCollections.FilterChildren(this, children, search);
                var previous = _tree.EditsDisabled;
                _tree.EditsDisabled = previous || hasToggle &&
                    (toggleChild.Value is false || toggleChild.SerializedProperty?.hasMultipleDifferentValues == true);
                try { NodinInspectorLayout.Draw(this, children); }
                finally { _tree.EditsDisabled = previous; }
            }
            if (hasToggle)
                GUILayout.Space(4);
        }

        private static GUIStyle _toggleHeaderLabel;
        private static GUIStyle _toggleHeaderArrow;
        private static readonly GUIContent ExpandedToggleArrow = new GUIContent("▼");
        private static readonly GUIContent CollapsedToggleArrow = new GUIContent("◀");

        internal bool DrawToggleHeader(NodinPropertyNode toggle, GUIContent label, bool inline, bool expanded)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var row = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, lineHeight + 1));
            using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
            _toggleHeaderLabel ??= new GUIStyle(EditorStyles.miniLabel)
            {
                clipping = TextClipping.Clip,
                padding = new RectOffset(1, 19, 0, 0)
            };
            _toggleHeaderArrow ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            if (Event.current.type == EventType.Repaint)
            {
                var shadow = row;
                shadow.y += 2;
                GUI.DrawTexture(shadow, Texture2D.whiteTexture, ScaleMode.StretchToFill, true,
                    0, new Color(0, 0, 0, 0.2f * GUI.color.a), 0, 3);
                var shade = EditorGUIUtility.isProSkin ? 0.34f : 0.8f;
                GUI.DrawTexture(row, Texture2D.whiteTexture, ScaleMode.StretchToFill, true,
                    0, GUI.color * new Color(shade, shade, shade), 0, 3);
            }

            var showToggle = toggle.IsVisible();
            if (showToggle)
            {
                var check = new Rect(row.x + 3, row.center.y - lineHeight / 2, 16, lineHeight);
                DrawToggle(check, toggle, (bool)toggle.Value);
            }

            var labelRect = row;
            labelRect.xMin = Mathf.Min(row.xMax, row.x + (showToggle ? 19 : 5));
            if (inline)
            {
                GUI.Label(labelRect, label, _toggleHeaderLabel);
                return true;
            }

            // Expanding a row should not register as an edit to the inspected value.
            var changed = GUI.changed;
            var next = GUI.Toggle(labelRect, expanded, label, _toggleHeaderLabel);
            GUI.changed = changed;
            var arrow = new Rect(row.xMax - 19, row.y, 14, row.height);
            GUI.Label(arrow, expanded ? ExpandedToggleArrow : CollapsedToggleArrow, _toggleHeaderArrow);
            return next;
        }

        internal void DrawToggle(Rect rect, NodinPropertyNode toggle, bool enabled)
        {
            using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled || toggle.IsDisabled());
            var previous = EditorGUI.showMixedValue;
            var property = toggle.SerializedProperty;
            EditorGUI.showMixedValue = property?.hasMultipleDifferentValues == true;
            try
            {
                var label = new GUIContent("", toggle.InspectorLabel.text);
                var before = toggle.SnapshotValues();
                if (property != null)
                    label = EditorGUI.BeginProperty(rect, label, property);
                try
                {
                    using var change = new EditorGUI.ChangeCheckScope();
                    var next = EditorGUI.Toggle(rect, label, enabled);
                    if (!change.changed)
                        return;
                    toggle.Value = next;
                }
                finally
                {
                    if (property != null)
                        EditorGUI.EndProperty();
                }
                toggle.NotifyChanged(before);
            }
            finally { EditorGUI.showMixedValue = previous; }
        }

        private void DrawObject(SerializedProperty property, GUIContent label)
        {
            var inline = GetAttribute<InlineEditorAttribute>();
            var preview = GetAttribute<PreviewFieldAttribute>();
            var allowScene = GetAttribute<AssetsOnlyAttribute>() == null && !EditorUtility.IsPersistent(_tree.Target);
            var onlyScene = GetAttribute<SceneObjectsOnlyAttribute>() != null;
            var value = property.objectReferenceValue;
            DrawReferenceObjectPicker(property, label);
            if (inline?.ObjectFieldMode is not InlineEditorObjectFieldModes.CompletelyHidden &&
                (inline?.ObjectFieldMode != InlineEditorObjectFieldModes.Hidden || !value))
            {
                using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled);
                var rect = EditorGUILayout.GetControlRect(false, preview?.Height ?? EditorGUIUtility.singleLineHeight);
                var content = EditorGUI.BeginProperty(rect, label, property);
                try
                {
                    using var change = new EditorGUI.ChangeCheckScope();
                    var fieldRect = preview == null ? rect : NodinValueControls.PreviewRect(rect, content, preview);
                    // PreviewRect has already applied the inspector indent.
                    using var indent = new EditorGUI.IndentLevelScope(preview == null ? 0 : -EditorGUI.indentLevel);
                    var next = EditorGUI.ObjectField(fieldRect, preview == null ? content : GUIContent.none,
                        value, ReferenceObjectType ?? typeof(Object), allowScene);
                    if (change.changed && IsAllowedReference(next) && (!onlyScene || next == null || !EditorUtility.IsPersistent(next)))
                        property.objectReferenceValue = next;
                }
                finally { EditorGUI.EndProperty(); }
            }
            else if (label != GUIContent.none && inline?.ObjectFieldMode != InlineEditorObjectFieldModes.CompletelyHidden)
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (value == null || property.hasMultipleDifferentValues)
                return;
            if (inline != null)
            {
                if (inline.ObjectFieldMode == InlineEditorObjectFieldModes.CompletelyHidden ||
                    inline.ObjectFieldMode == InlineEditorObjectFieldModes.Hidden && inline.Expanded)
                {
                    _tree.DrawInlineEditor(Path, value);
                    return;
                }

                var key = Path + ":editor";
                var expanded = _tree.State(key, inline.Expanded);
                _tree.SetState(key, EditorGUILayout.Foldout(expanded, $"Inspect {value.name}", true));
                if (expanded)
                    _tree.DrawInlineEditor(Path, value);
            }
        }
    }
}
