using System;
using System.Diagnostics;

namespace Nodin
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class MaxValueAttribute : Attribute
    {
        public MaxValueAttribute(double maximum) => Maximum = maximum;

        public double Maximum { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PropertyRangeAttribute : Attribute
    {
        public PropertyRangeAttribute(double minimum, double maximum) => (Minimum, Maximum) = (minimum, maximum);
        public PropertyRangeAttribute(string minimum, double maximum) : this(0, maximum) => MinimumMember = minimum;
        public PropertyRangeAttribute(double minimum, string maximum) : this(minimum, 0) => MaximumMember = maximum;
        public PropertyRangeAttribute(string minimum, string maximum) : this(0d, 0d) =>
            (MinimumMember, MaximumMember) = (minimum, maximum);

        public double Minimum { get; }
        public double Maximum { get; }
        public string MinimumMember { get; }
        public string MaximumMember { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ProgressBarAttribute : Attribute
    {
        public ProgressBarAttribute(double minimum, double maximum) => (Minimum, Maximum) = (minimum, maximum);
        public ProgressBarAttribute(string minimum, double maximum) : this(0, maximum) => MinimumMember = minimum;
        public ProgressBarAttribute(double minimum, string maximum) : this(minimum, 0) => MaximumMember = maximum;
        public ProgressBarAttribute(string minimum, string maximum) : this(0d, 0d) =>
            (MinimumMember, MaximumMember) = (minimum, maximum);

        public double Minimum { get; }
        public double Maximum { get; }
        public string MinimumMember { get; }
        public string MaximumMember { get; }
        public float Height { get; set; }
        public bool DrawValueLabel { get; set; } = true;
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class MinMaxSliderAttribute : Attribute
    {
        public MinMaxSliderAttribute(float minimum, float maximum, bool showFields = false) =>
            (Minimum, Maximum, ShowFields) = (minimum, maximum, showFields);
        public MinMaxSliderAttribute(string minimum, float maximum, bool showFields = false) : this(0, maximum, showFields) =>
            MinimumMember = minimum;
        public MinMaxSliderAttribute(float minimum, string maximum, bool showFields = false) : this(minimum, 0, showFields) =>
            MaximumMember = maximum;
        public MinMaxSliderAttribute(string minimum, string maximum, bool showFields = false) : this(0f, 0f, showFields) =>
            (MinimumMember, MaximumMember) = (minimum, maximum);
        public MinMaxSliderAttribute(string rangeMember, bool showFields = false) : this(0f, 0f, showFields) =>
            RangeMember = rangeMember;

        public float Minimum { get; }
        public float Maximum { get; }
        public string MinimumMember { get; }
        public string MaximumMember { get; }
        public string RangeMember { get; }
        public bool ShowFields { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class UnitAttribute : Attribute
    {
        public UnitAttribute(string symbol) : this(symbol, symbol, 1) { }

        public UnitAttribute(string storedUnit, string displayUnit, double displayMultiplier)
        {
            StoredUnit = storedUnit;
            DisplayUnit = displayUnit;
            DisplayMultiplier = displayMultiplier;
        }

        public string StoredUnit { get; }
        public string DisplayUnit { get; }
        public double DisplayMultiplier { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class WrapAttribute : Attribute
    {
        public WrapAttribute(double minimum, double maximum) => (Minimum, Maximum) = (minimum, maximum);

        public double Minimum { get; }
        public double Maximum { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class EnumPagingAttribute : Attribute { }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class DelayedPropertyAttribute : Attribute { }
}
