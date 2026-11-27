using System;
using System.Diagnostics;
using UnityEngine;

namespace Nodin
{
    public enum ButtonSizes { Small, Medium, Large, ExtraLarge, Gigantic }
    public enum InfoMessageType { None, Info, Warning, Error }
    public enum ObjectFieldAlignment { Left, Center, Right }
    public enum InlineEditorObjectFieldModes { Boxed, Foldout, Hidden, CompletelyHidden }

    public interface INodinDropdownItem
    {
        string Text { get; }
        object Value { get; }
    }

    public readonly struct ValueDropdownItem<T> : INodinDropdownItem
    {
        public ValueDropdownItem(string text, T value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }
        public T Value { get; }
        object INodinDropdownItem.Value => Value;
    }

    [Conditional("UNITY_EDITOR")]
    public abstract class NodinGroupAttribute : Attribute
    {
        protected NodinGroupAttribute(string group, float order)
        {
            Group = group;
            Order = order;
        }

        public string Group { get; }
        public float Order { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    public abstract class NodinConditionAttribute : Attribute
    {
        protected NodinConditionAttribute(string condition) => Condition = condition;
        protected NodinConditionAttribute(string condition, object value) : this(condition)
        {
            Value = value;
            HasValue = true;
        }

        public string Condition { get; }
        public object Value { get; }
        public bool HasValue { get; }
    }

    [Conditional("UNITY_EDITOR")]
    public abstract class NodinPathAttribute : Attribute
    {
        public string ParentFolder { get; set; }
        public bool AbsolutePath { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class BoxGroupAttribute : NodinGroupAttribute
    {
        public BoxGroupAttribute(string group, bool showLabel = true, bool centerLabel = false, float order = 0) : base(group, order)
        {
            ShowLabel = showLabel;
            CenterLabel = centerLabel;
        }
        public bool ShowLabel { get; set; }
        public bool CenterLabel { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class FoldoutGroupAttribute : NodinGroupAttribute
    {
        public FoldoutGroupAttribute(string group, bool expanded = false, float order = 0) : base(group, order) => Expanded = expanded;
        public bool Expanded { get; set; }
        public string GroupName { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class HorizontalGroupAttribute : NodinGroupAttribute
    {
        public HorizontalGroupAttribute(string group = "_Horizontal", float width = 0, float order = 0) : base(group, order) => Width = width;
        public float Width { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class VerticalGroupAttribute : NodinGroupAttribute
    {
        public VerticalGroupAttribute(string group, float order = 0) : base(group, order) { }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class ButtonGroupAttribute : NodinGroupAttribute
    {
        public ButtonGroupAttribute(string group = "_Buttons", float order = 0) : base(group, order) { }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class TitleGroupAttribute : NodinGroupAttribute
    {
        public TitleGroupAttribute(string group, string subtitle = null, float order = 0) : base(group, order) => Subtitle = subtitle;
        public string Subtitle { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class TabGroupAttribute : NodinGroupAttribute
    {
        public TabGroupAttribute(string tab) : this("_Tabs", tab) { }
        public TabGroupAttribute(string group, string tab, float order = 0) : base(group, order) => Tab = tab;
        public string Tab { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class ShowIfAttribute : NodinConditionAttribute
    {
        public ShowIfAttribute(string condition) : base(condition) { }
        public ShowIfAttribute(string condition, object value) : base(condition, value) { }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class HideIfAttribute : NodinConditionAttribute
    {
        public HideIfAttribute(string condition) : base(condition) { }
        public HideIfAttribute(string condition, object value) : base(condition, value) { }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class EnableIfAttribute : NodinConditionAttribute
    {
        public EnableIfAttribute(string condition) : base(condition) { }
        public EnableIfAttribute(string condition, object value) : base(condition, value) { }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class DisableIfAttribute : NodinConditionAttribute
    {
        public DisableIfAttribute(string condition) : base(condition) { }
        public DisableIfAttribute(string condition, object value) : base(condition, value) { }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class ButtonAttribute : Attribute
    {
        public ButtonAttribute() { }
        public ButtonAttribute(ButtonSizes size) => Size = size;
        public ButtonAttribute(string name, ButtonSizes size = ButtonSizes.Small)
        {
            Name = name;
            Size = size;
        }
        public ButtonAttribute(int height) => Height = height;
        public ButtonAttribute(string name, int height) : this(height) => Name = name;
        public string Name { get; set; }
        public ButtonSizes Size { get; }
        public int Height { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class GUIColorAttribute : Attribute
    {
        public GUIColorAttribute(string member) => Member = member;
        public GUIColorAttribute(float r, float g, float b, float a = 1) => Color = new Color(r, g, b, a);
        public string Member { get; }
        public Color Color { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class ListDrawerSettingsAttribute : Attribute
    {
        public bool? InitialExpansion { get; private set; }
        public bool DefaultExpandedState { get => InitialExpansion ?? false; set => InitialExpansion = value; }
        public bool Expanded { get => DefaultExpandedState; set => DefaultExpandedState = value; }
        public bool DraggableItems { get; set; } = true;
        public bool HideAddButton { get; set; }
        public bool HideRemoveButton { get; set; }
        public int NumberOfItemsPerPage { get; set; } = 15;
        public bool ShowPaging { get; set; } = true;
        public string ListElementLabelName { get; set; }
        public bool ShowIndexLabels { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class TableListAttribute : Attribute
    {
        public bool AlwaysExpanded { get; set; }
        public bool ShowPaging { get; set; }
        public int NumberOfItemsPerPage { get; set; } = 15;
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class SearchableAttribute : Attribute
    {
        public bool FuzzySearch { get; set; } = true;
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class InfoBoxAttribute : Attribute
    {
        public InfoBoxAttribute(string message, InfoMessageType infoMessageType = InfoMessageType.Info, string visibleIf = null)
        {
            Message = message;
            MessageType = infoMessageType;
            VisibleIf = visibleIf;
        }
        public string Message { get; }
        public InfoMessageType MessageType { get; }
        public string VisibleIf { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class PreviewFieldAttribute : Attribute
    {
        public PreviewFieldAttribute(float height = 50, ObjectFieldAlignment alignment = ObjectFieldAlignment.Right)
        {
            Height = height;
            Alignment = alignment;
        }
        public PreviewFieldAttribute(ObjectFieldAlignment alignment) : this(50, alignment) { }
        public float Height { get; }
        public ObjectFieldAlignment Alignment { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class InlineEditorAttribute : Attribute
    {
        public bool Expanded { get; set; }
        public InlineEditorObjectFieldModes ObjectFieldMode { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class InlineButtonAttribute : Attribute
    {
        public InlineButtonAttribute(string action, string label = null)
        {
            Action = action;
            Label = label;
        }
        public string Action { get; }
        public string Label { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class SuffixLabelAttribute : Attribute
    {
        public SuffixLabelAttribute(string label, bool overlay = false)
        {
            Label = label;
            Overlay = overlay;
        }
        public string Label { get; }
        public bool Overlay { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class TitleAttribute : Attribute
    {
        public TitleAttribute(string title, string subtitle = null)
        {
            Text = title;
            Subtitle = subtitle;
        }
        public string Text { get; }
        public string Subtitle { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class PropertySpaceAttribute : Attribute
    {
        public PropertySpaceAttribute(float spaceBefore = 8, float spaceAfter = 0)
        {
            Before = spaceBefore;
            After = spaceAfter;
        }
        public float Before { get; }
        public float After { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class DisplayAsStringAttribute : Attribute
    {
        public DisplayAsStringAttribute(bool overflow = true) => Overflow = overflow;
        public bool Overflow { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class ValueDropdownAttribute : Attribute
    {
        public ValueDropdownAttribute(string values) => Values = values;
        public string Values { get; }
        public bool IsUniqueList { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class ValidateInputAttribute : Attribute
    {
        public ValidateInputAttribute(string member, string message = null)
        {
            Member = member;
            Message = message;
        }
        public string Member { get; }
        public string Message { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class OnInspectorGUIAttribute : Attribute
    {
        public OnInspectorGUIAttribute() { }
        public OnInspectorGUIAttribute(string action) => Action = action;
        public string Action { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class FilePathAttribute : NodinPathAttribute
    {
        public string Extensions { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class FolderPathAttribute : NodinPathAttribute
    { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class RequiredAttribute : Attribute
    {
        public RequiredAttribute(string message = null) => Message = message;
        public string Message { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class LabelTextAttribute : Attribute
    {
        public LabelTextAttribute(string text) => Text = text;
        public string Text { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class PropertyOrderAttribute : Attribute
    {
        public PropertyOrderAttribute(float order = 0) => Order = order;
        public float Order { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class ToggleAttribute : Attribute
    {
        public ToggleAttribute(string member) => Member = member;
        public string Member { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public sealed class OnValueChangedAttribute : Attribute
    {
        public OnValueChangedAttribute(string action) => Action = action;
        public string Action { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class TableColumnWidthAttribute : Attribute
    {
        public TableColumnWidthAttribute(int width) => Width = width;
        public int Width { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class LabelWidthAttribute : Attribute
    {
        public LabelWidthAttribute(float width) => Width = width;
        public float Width { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class MinValueAttribute : Attribute
    {
        public MinValueAttribute(double minimum) => Minimum = minimum;
        public double Minimum { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class CustomValueDrawerAttribute : Attribute
    {
        public CustomValueDrawerAttribute(string member) => Member = member;
        public string Member { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class MultiLinePropertyAttribute : Attribute
    {
        public MultiLinePropertyAttribute(int lines = 3) => Lines = lines;
        public int Lines { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class ReadOnlyAttribute : Attribute
    { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class HideLabelAttribute : Attribute
    { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class AssetsOnlyAttribute : Attribute
    { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class InlinePropertyAttribute : Attribute
    { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class ToggleLeftAttribute : Attribute
    { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class EnumToggleButtonsAttribute : Attribute
    { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class DisableInEditorModeAttribute : Attribute
    { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class HideInEditorModeAttribute : Attribute
    { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class SceneObjectsOnlyAttribute : Attribute
    { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class ShowInInspectorAttribute : Attribute
    { }
}

