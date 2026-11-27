using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nodin.Editor
{
    public sealed partial class NodinPropertyNode
    {
        private static readonly CultureInfo NumericCulture = CultureInfo.InvariantCulture;
        private static readonly string[] VectorComponents = { "x", "y", "z", "w" };

        private readonly struct NumericBounds
        {
            internal readonly double Minimum;
            internal readonly double Maximum;

            internal NumericBounds(double minimum, double maximum) => (Minimum, Maximum) = (minimum, maximum);

            internal bool IsValid => !double.IsNaN(Minimum) && !double.IsNaN(Maximum) && Minimum <= Maximum;
            internal bool HasFiniteEndpoints => !double.IsNaN(Minimum) && !double.IsNaN(Maximum) &&
                !double.IsInfinity(Minimum) && !double.IsInfinity(Maximum);
            internal bool IsFinite => HasFiniteEndpoints && Minimum <= Maximum;
            internal NumericBounds Intersect(NumericBounds other) =>
                new(Math.Max(Minimum, other.Minimum), Math.Min(Maximum, other.Maximum));
        }

        private bool DrawNumericControl(SerializedProperty property, GUIContent label)
        {
            var type = ValueType;
            if (type == null || property != null &&
                (GetAttribute<RangeAttribute>() != null || GetAttribute<MinAttribute>() != null ||
                 GetAttribute<ValueDropdownAttribute>() != null))
                return false;

            type = Nullable.GetUnderlyingType(type) ?? type;
            var delayedProperty = GetAttribute<DelayedPropertyAttribute>() != null;
            var delayed = delayedProperty || GetAttribute<DelayedAttribute>() != null;
            var paging = type.IsEnum && GetAttribute<EnumPagingAttribute>() != null &&
                GetAttribute<EnumToggleButtonsAttribute>() == null;
            var interval = type == typeof(Vector2) ? GetAttribute<MinMaxSliderAttribute>() : null;
            var scalar = IsScalarNumber(type);
            var range = scalar ? GetAttribute<PropertyRangeAttribute>() : null;
            var progress = scalar ? GetAttribute<ProgressBarAttribute>() : null;
            var unit = scalar ? GetAttribute<UnitAttribute>() : null;
            var wrap = GetAttribute<WrapAttribute>();
            var constrained = GetAttribute<MinValueAttribute>() != null || GetAttribute<MaxValueAttribute>() != null ||
                wrap != null;
            var components = property?.propertyType switch
            {
                SerializedPropertyType.Vector2 or SerializedPropertyType.Vector2Int => 2,
                SerializedPropertyType.Vector3 or SerializedPropertyType.Vector3Int => 3,
                SerializedPropertyType.Vector4 => 4,
                _ => 0
            };
            var constrainedVector = components > 0 && (constrained || delayedProperty);
            var delayedString = delayedProperty && type == typeof(string) && GetAttribute<MultiLinePropertyAttribute>() == null &&
                GetAttribute<NodinPathAttribute>() == null && GetAttribute<TextAreaAttribute>() == null &&
                GetAttribute<MultilineAttribute>() == null;
            if (!paging && interval == null && range == null && progress == null && unit == null &&
                !(scalar && (constrained || delayedProperty)) && !constrainedVector && !delayedString)
                return false;

            var value = paging && property != null ? Enum.ToObject(type, property.intValue) : Value;
            var progressBounds = progress == null ? default :
                SharedNumericBounds(progress.Minimum, progress.Maximum, progress.MinimumMember, progress.MaximumMember);
            var rangeBounds = range == null ? default :
                SharedNumericBounds(range.Minimum, range.Maximum, range.MinimumMember, range.MaximumMember);
            var scalarBounds = ScalarLimits();
            if (range != null && rangeBounds.HasFiniteEndpoints)
                scalarBounds = scalarBounds.Intersect(rangeBounds);
            if (progress != null && progressBounds.HasFiniteEndpoints)
                scalarBounds = scalarBounds.Intersect(progressBounds);

            var line = EditorGUIUtility.singleLineHeight;
            var barHeight = progress != null && progressBounds.IsFinite
                ? Math.Max(line, float.IsInfinity(progress.Height) || float.IsNaN(progress.Height) ? 0f : progress.Height)
                : 0f;
            var height = barHeight > 0 ? line + EditorGUIUtility.standardVerticalSpacing + barHeight : line;

            var emptyRange = scalar && (scalarBounds.Minimum > scalarBounds.Maximum ||
                Type.GetTypeCode(type) is >= TypeCode.SByte and <= TypeCode.UInt64 &&
                Math.Ceiling(scalarBounds.Minimum) > Math.Floor(scalarBounds.Maximum));
            using var disabled = new EditorGUI.DisabledScope(_tree.EditsDisabled || !CanInteract || emptyRange);
            var row = EditorGUILayout.GetControlRect(true, height);
            var content = property == null ? label : EditorGUI.BeginProperty(row, label, property);
            try
            {
                if (emptyRange && !string.IsNullOrEmpty(content.text))
                    content = new GUIContent(content) { tooltip = "The specified bounds have no shared values." };
                var field = string.IsNullOrEmpty(content.text) ? EditorGUI.IndentedRect(row) :
                    EditorGUI.PrefixLabel(new Rect(row.x, row.y, row.width, line), content);
                field.height = line;
                using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
                if (paging)
                {
                    var next = DrawPagedEnum(field, type, (Enum)(value ?? Activator.CreateInstance(type)));
                    if (next != null)
                    {
                        if (property == null)
                            Value = next;
                        else
                            property.intValue = Enum.GetUnderlyingType(type) == typeof(uint)
                                ? unchecked((int)Convert.ToUInt32(next)) : Convert.ToInt32(next);
                        GUI.changed = true;
                    }
                    return true;
                }

                if (interval != null)
                {
                    var next = DrawMinMax(field, (Vector2)(value ?? default(Vector2)), interval, delayed);
                    if (next.HasValue)
                        Value = next.Value;
                    return true;
                }

                if (constrainedVector)
                {
                    DrawNumericComponents(field, property, components, scalarBounds, wrap, delayed);
                    return true;
                }

                if (delayedString)
                {
                    using var change = new EditorGUI.ChangeCheckScope();
                    var next = EditorGUI.DelayedTextField(field, (string)value ?? "");
                    if (change.changed)
                        Value = next;
                    return true;
                }

                if (barHeight > 0)
                {
                    var meter = new Rect(field.x, row.yMax - barHeight, field.width, barHeight);
                    var number = Convert.ToDouble(value, NumericCulture);
                    var fraction = progressBounds.Minimum == progressBounds.Maximum
                        ? (number >= progressBounds.Maximum ? 1f : 0f) : NumericFraction(number, progressBounds);
                    var text = EditorGUI.showMixedValue ? "—" :
                        progress.DrawValueLabel ? NumericText(value, unit) : "";
                    EditorGUI.ProgressBar(meter, EditorGUI.showMixedValue ? 0f : fraction, text);
                }

                var before = GUI.changed;
                using var numberChanged = new EditorGUI.ChangeCheckScope();
                var edited = DrawScalarNumber(field, type, value, unit, delayed, scalarBounds,
                    range != null && rangeBounds.IsFinite);
                if (numberChanged.changed && edited != null)
                    Value = ConstrainNumber(edited, scalarBounds, wrap, type);
                else if (edited == null)
                    GUI.changed = before;
                return true;
            }
            finally
            {
                if (property != null)
                    EditorGUI.EndProperty();
            }
        }

        private NumericBounds ScalarLimits() => new(GetAttribute<MinValueAttribute>()?.Minimum ?? double.NegativeInfinity,
            GetAttribute<MaxValueAttribute>()?.Maximum ?? double.PositiveInfinity);

        private NumericBounds SharedNumericBounds(double min, double max, string minMember, string maxMember,
            string rangeMember = null)
        {
            if (minMember == null && maxMember == null && rangeMember == null)
                return new NumericBounds(min, max);

            var bounds = new NumericBounds(double.NegativeInfinity, double.PositiveInfinity);
            foreach (var target in _tree.Targets)
            {
                var owner = OwnerOf(target);
                if (rangeMember != null)
                {
                    if (!NodinExpression.TryEvaluate(owner, rangeMember, out var result, out _) || result is not Vector2 pair)
                        return new NumericBounds(double.NaN, double.NaN);
                    bounds = bounds.Intersect(new NumericBounds(pair.x, pair.y));
                }
                else
                    bounds = bounds.Intersect(new NumericBounds(
                        NumericLimit(owner, minMember, min), NumericLimit(owner, maxMember, max)));
            }
            return bounds;
        }

        private static double NumericLimit(object owner, string member, double fallback)
        {
            if (member == null)
                return fallback;
            return NodinExpression.TryEvaluate(owner, member, out var value, out _) && IsScalarNumber(value?.GetType())
                ? Convert.ToDouble(value, NumericCulture) : double.NaN;
        }

        private static void DrawNumericComponents(Rect field, SerializedProperty property, int count, NumericBounds bounds,
            WrapAttribute wrap, bool delayed)
        {
            var mixed = EditorGUI.showMixedValue;
            try
            {
                var width = Mathf.Max(0, (field.width - 3 * (count - 1)) / count);
                for (var index = 0; index < count; index++)
                {
                    var name = VectorComponents[index];
                    using var component = property.FindPropertyRelative(name);
                    if (component == null)
                        continue;

                    var rect = new Rect(field.x + index * (width + 3), field.y, width, field.height);
                    var caption = rect;
                    caption.width = Mathf.Min(width * .3f, 12);
                    EditorGUI.LabelField(caption, name.ToUpperInvariant());
                    rect.xMin = caption.xMax;
                    EditorGUI.showMixedValue = component.hasMultipleDifferentValues;

                    var value = component.boxedValue;
                    var type = value.GetType();
                    using var changed = new EditorGUI.ChangeCheckScope();
                    var edited = DrawNumericField(rect, type, value, null, delayed);
                    if (changed.changed && edited != null)
                        component.boxedValue = ConstrainNumber(edited, bounds, wrap, type);
                }
            }
            finally
            {
                EditorGUI.showMixedValue = mixed;
            }
        }

        private Vector2? DrawMinMax(Rect field, Vector2 value, MinMaxSliderAttribute options, bool delayed)
        {
            Vector2? next = null;
            var bounds = SharedNumericBounds(options.Minimum, options.Maximum, options.MinimumMember,
                options.MaximumMember, options.RangeMember);
            if (!bounds.IsFinite || bounds.Minimum < -float.MaxValue || bounds.Maximum > float.MaxValue)
            {
                using var disabled = new EditorGUI.DisabledScope(_tree.IsMulti || bounds.Minimum > bounds.Maximum);
                using var changed = new EditorGUI.ChangeCheckScope();
                var edited = EditorGUI.Vector2Field(field, GUIContent.none, value);
                return changed.changed ? edited : null;
            }

            var minimum = (float)bounds.Minimum;
            var maximum = (float)bounds.Maximum;
            var low = float.IsNaN(value.x) ? minimum : Mathf.Clamp(value.x, minimum, maximum);
            var high = float.IsNaN(value.y) ? maximum : Mathf.Clamp(value.y, low, maximum);
            if (options.ShowFields)
            {
                var gap = Mathf.Clamp(field.width * .05f, 0f, 3f);
                var width = Mathf.Clamp(field.width / 4f, 0f, 58f);
                var lowRect = new Rect(field.x, field.y, width, field.height);
                var highRect = new Rect(field.xMax - width, field.y, width, field.height);
                field.xMin = lowRect.xMax + gap;
                field.xMax = highRect.xMin - gap;
                // A slider gesture sets a new shared pair. Exact fields edit one endpoint of a single pair.
                using (new EditorGUI.DisabledScope(EditorGUI.showMixedValue))
                {
                    using (var changed = new EditorGUI.ChangeCheckScope())
                    {
                        var edited = delayed ? EditorGUI.DelayedFloatField(lowRect, value.x) : EditorGUI.FloatField(lowRect, value.x);
                        if (changed.changed)
                            next = new Vector2(float.IsNaN(edited) ? low : Mathf.Clamp(edited, minimum, high), high);
                    }
                    using (var changed = new EditorGUI.ChangeCheckScope())
                    {
                        var edited = delayed ? EditorGUI.DelayedFloatField(highRect, value.y) : EditorGUI.FloatField(highRect, value.y);
                        if (changed.changed)
                            next = new Vector2(low, float.IsNaN(edited) ? high : Mathf.Clamp(edited, low, maximum));
                    }
                }
            }

            using var slider = new EditorGUI.ChangeCheckScope();
            using (new EditorGUI.DisabledScope(minimum == maximum))
                EditorGUI.MinMaxSlider(field, GUIContent.none, ref low, ref high, minimum, maximum);
            return slider.changed ? new Vector2(low, high) : next;
        }

        private static Enum DrawPagedEnum(Rect field, Type type, Enum value)
        {
            var values = Enum.GetValues(type).Cast<Enum>().Distinct().ToArray();
            if (values.Length == 0)
                return null;

            var width = Mathf.Clamp(field.width / 4f, 0f, 22f);
            var gap = Mathf.Clamp(field.width * .05f, 0f, 2f);
            var previous = new Rect(field.x, field.y, width, field.height);
            var following = new Rect(field.xMax - width, field.y, width, field.height);
            field.xMin = previous.xMax + gap;
            field.xMax = following.xMin - gap;
            var index = Array.IndexOf(values, value);
            using (new EditorGUI.DisabledScope(values.Length == 1 && index == 0 && !EditorGUI.showMixedValue))
            {
                if (GUI.Button(previous, new GUIContent("‹", "Previous value"), EditorStyles.miniButton))
                    return values[index < 0 ? values.Length - 1 : (index + values.Length - 1) % values.Length];
                if (GUI.Button(following, new GUIContent("›", "Next value"), EditorStyles.miniButton))
                    return values[(index + 1) % values.Length];
            }

            using var change = new EditorGUI.ChangeCheckScope();
            var next = EditorGUI.EnumPopup(field, value);
            return change.changed ? next : null;
        }

        private static object DrawScalarNumber(Rect field, Type type, object value, UnitAttribute unit, bool delayed,
            NumericBounds range, bool showSlider)
        {
            object sliderValue = null;
            if (showSlider && range.IsFinite && range.Minimum < range.Maximum)
            {
                var slider = field;
                var gap = Mathf.Clamp(field.width * .05f, 0f, 5f);
                field.xMin = field.xMax - Mathf.Min(field.width * .5f, Mathf.Clamp(field.width / 3f, 50f, 100f));
                slider.xMax = field.xMin - gap;
                using var changed = new EditorGUI.ChangeCheckScope();
                var fraction = GUI.HorizontalSlider(slider, NumericFraction(
                    Convert.ToDouble(value, NumericCulture), range), 0f, 1f);
                if (changed.changed)
                {
                    var number = InterpolateNumericRange(range, fraction);
                    sliderValue = type == typeof(float) || type == typeof(double) ? (object)number : NumericDecimal(number);
                }
            }

            using var fieldChanged = new EditorGUI.ChangeCheckScope();
            var edited = DrawNumericField(field, type, value, unit, delayed);
            return fieldChanged.changed ? edited : sliderValue;
        }

        private static object DrawNumericField(Rect field, Type type, object value, UnitAttribute unit, bool delayed)
        {
            if (unit != null || type == typeof(ulong) || type == typeof(decimal) ||
                delayed && type != typeof(float) && type != typeof(double) && type != typeof(int))
            {
                using var change = new EditorGUI.ChangeCheckScope();
                var text = EditorGUI.DelayedTextField(field, NumericText(value, unit));
                if (change.changed)
                {
                    var parsed = ParseNumericText(text, type, unit);
                    GUI.changed = parsed != null;
                    return parsed;
                }
                return null;
            }

            if (type == typeof(float))
                return delayed ? EditorGUI.DelayedFloatField(field, (float)(value ?? 0f)) :
                    EditorGUI.FloatField(field, (float)(value ?? 0f));
            if (type == typeof(double))
                return delayed ? EditorGUI.DelayedDoubleField(field, (double)(value ?? 0d)) :
                    EditorGUI.DoubleField(field, (double)(value ?? 0d));
            if (type == typeof(int))
                return delayed ? EditorGUI.DelayedIntField(field, (int)(value ?? 0)) :
                    EditorGUI.IntField(field, (int)(value ?? 0));

            return EditorGUI.LongField(field, Convert.ToInt64(value, NumericCulture));
        }

        private static bool UsesNumericUnitConversion(UnitAttribute unit) =>
            unit != null && unit.DisplayMultiplier > 0 && !double.IsInfinity(unit.DisplayMultiplier) &&
            unit.DisplayMultiplier != 1;

        private static string NumericText(object value, UnitAttribute unit)
        {
            var text = Convert.ToString(value, NumericCulture);
            if (UsesNumericUnitConversion(unit) && value != null)
            {
                var multiplier = NumericDecimal(unit.DisplayMultiplier);
                if (value is not (float or double) && multiplier != 0)
                {
                    var number = Convert.ToDecimal(value, NumericCulture);
                    if (multiplier <= 1 || Math.Abs(number) <= decimal.MaxValue / multiplier)
                        text = (number * multiplier).ToString(NumericCulture);
                    else
                        return text + " " + unit.StoredUnit;
                }
                else
                {
                    var display = Convert.ToDouble(value, NumericCulture) * unit.DisplayMultiplier;
                    if (double.IsInfinity(display) && !double.IsInfinity(Convert.ToDouble(value, NumericCulture)))
                        return text + " " + unit.StoredUnit;
                    text = display.ToString("R", NumericCulture);
                }
            }
            return unit == null || string.IsNullOrEmpty(unit.DisplayUnit) ? text : text + " " + unit.DisplayUnit;
        }

        private static object ParseNumericText(string text, Type type, UnitAttribute unit)
        {
            text = text.Trim();
            var multiplier = UsesNumericUnitConversion(unit) ? unit.DisplayMultiplier : 1d;
            var displaySuffix = !string.IsNullOrEmpty(unit?.DisplayUnit) &&
                text.EndsWith(unit.DisplayUnit, StringComparison.Ordinal);
            var storedSuffix = !string.IsNullOrEmpty(unit?.StoredUnit) &&
                text.EndsWith(unit.StoredUnit, StringComparison.Ordinal);
            if (storedSuffix && (!displaySuffix || unit.StoredUnit.Length > unit.DisplayUnit.Length))
            {
                text = text[..^unit.StoredUnit.Length].TrimEnd();
                multiplier = 1;
            }
            else if (displaySuffix)
                text = text[..^unit.DisplayUnit.Length].TrimEnd();

            if (type == typeof(float) || type == typeof(double))
                return double.TryParse(text, NumberStyles.Float, NumericCulture, out var floating)
                    ? (object)(floating / multiplier) : null;

            if (!decimal.TryParse(text, NumberStyles.Float, NumericCulture, out var number))
                return null;
            var divisor = NumericDecimal(multiplier);
            if (divisor == 0 || divisor < 1 && Math.Abs(number) > decimal.MaxValue * divisor)
                return null;
            return number / divisor;
        }

        private static float NumericFraction(double value, NumericBounds bounds)
        {
            if (double.IsNaN(value) || value <= bounds.Minimum)
                return 0f;
            if (value >= bounds.Maximum)
                return 1f;
            var width = bounds.Maximum - bounds.Minimum;
            return double.IsInfinity(width)
                ? (float)((value * .5 - bounds.Minimum * .5) / (bounds.Maximum * .5 - bounds.Minimum * .5))
                : (float)((value - bounds.Minimum) / width);
        }

        private static double InterpolateNumericRange(NumericBounds bounds, float fraction) =>
            bounds.Minimum * (1d - fraction) + bounds.Maximum * fraction;

        private void ApplyNumericConstraints()
        {
            if (_tree.EditsDisabled || !CanWrite)
                return;
            var wrap = GetAttribute<WrapAttribute>();
            var limits = ScalarLimits();
            var range = GetAttribute<PropertyRangeAttribute>();
            var progress = GetAttribute<ProgressBarAttribute>();
            if (wrap == null && range == null && progress == null &&
                double.IsNegativeInfinity(limits.Minimum) && double.IsPositiveInfinity(limits.Maximum))
                return;
            var property = SerializedProperty;
            if (property?.hasMultipleDifferentValues == true)
                return;
            var current = Value;
            if (IsScalarNumber(current?.GetType()))
            {
                if (range != null && GetAttribute<RangeAttribute>() == null)
                {
                    var bounds = SharedNumericBounds(range.Minimum, range.Maximum, range.MinimumMember, range.MaximumMember);
                    if (bounds.IsFinite)
                        limits = limits.Intersect(bounds);
                }
                if (progress != null && GetAttribute<RangeAttribute>() == null)
                {
                    var bounds = SharedNumericBounds(progress.Minimum, progress.Maximum, progress.MinimumMember, progress.MaximumMember);
                    if (bounds.IsFinite)
                        limits = limits.Intersect(bounds);
                }
            }
            var constrained = current switch
            {
                Vector2 vector => new Vector2(ConstrainComponent(vector.x, limits, wrap), ConstrainComponent(vector.y, limits, wrap)),
                Vector3 vector => new Vector3(ConstrainComponent(vector.x, limits, wrap), ConstrainComponent(vector.y, limits, wrap),
                    ConstrainComponent(vector.z, limits, wrap)),
                Vector4 vector => new Vector4(ConstrainComponent(vector.x, limits, wrap), ConstrainComponent(vector.y, limits, wrap),
                    ConstrainComponent(vector.z, limits, wrap), ConstrainComponent(vector.w, limits, wrap)),
                Vector2Int vector => new Vector2Int(ConstrainComponent(vector.x, limits, wrap), ConstrainComponent(vector.y, limits, wrap)),
                Vector3Int vector => new Vector3Int(ConstrainComponent(vector.x, limits, wrap), ConstrainComponent(vector.y, limits, wrap),
                    ConstrainComponent(vector.z, limits, wrap)),
                _ => ConstrainNumber(current, limits, wrap)
            };
            if (!Equals(current, constrained))
                Value = constrained;
        }

        private static T ConstrainComponent<T>(T value, NumericBounds limits, WrapAttribute wrap) =>
            (T)ConstrainNumber(value, limits, wrap);

        private static object ConstrainNumber(object value, NumericBounds limits, WrapAttribute wrap, Type type = null)
        {
            type ??= value?.GetType();
            if (value == null || !IsScalarNumber(type))
                return value;

            if (type == typeof(float) || type == typeof(double))
            {
                var number = Convert.ToDouble(value, NumericCulture);
                var singlePrecision = type == typeof(float);
                if (wrap != null)
                {
                    var minimum = singlePrecision ? (float)wrap.Minimum : wrap.Minimum;
                    var maximum = singlePrecision ? (float)wrap.Maximum : wrap.Maximum;
                    var width = maximum - minimum;
                    if (width > 0 && !double.IsInfinity(width) && !double.IsInfinity(number) &&
                        (number < minimum || number > maximum))
                    {
                        var numberRemainder = number % width;
                        var limitRemainder = minimum % width;
                        if (numberRemainder < 0) numberRemainder += width;
                        if (limitRemainder < 0) limitRemainder += width;
                        var offset = numberRemainder - limitRemainder;
                        number = minimum + (offset < 0 ? offset + width : offset);
                    }
                }

                if (limits.IsValid && !double.IsNaN(number))
                    number = Math.Clamp(number, limits.Minimum, limits.Maximum);
                return singlePrecision ? (object)(float)number : number;
            }

            var integer = type != typeof(decimal);
            var numberDecimal = Convert.ToDecimal(value, NumericCulture);
            if (integer)
                numberDecimal = decimal.Round(numberDecimal, MidpointRounding.AwayFromZero);
            if (wrap != null && !double.IsInfinity(wrap.Minimum) && !double.IsInfinity(wrap.Maximum) &&
                !double.IsNaN(wrap.Minimum) && !double.IsNaN(wrap.Maximum))
            {
                var lower = integer ? NumericIntegerBound(Math.Ceiling(wrap.Minimum)) : NumericDecimal(wrap.Minimum);
                var upper = integer ? NumericIntegerBound(Math.Floor(wrap.Maximum)) : NumericDecimal(wrap.Maximum);
                // Restrict the period so decimal subtraction and the inclusive integer width cannot overflow.
                if ((lower < upper || integer && lower == upper) && lower > decimal.MinValue / 2 && upper < decimal.MaxValue / 2 &&
                    (numberDecimal < lower || numberDecimal > upper))
                {
                    var width = upper - lower + (integer ? 1m : 0m);
                    var numberRemainder = numberDecimal % width;
                    var limitRemainder = lower % width;
                    if (numberRemainder < 0) numberRemainder += width;
                    if (limitRemainder < 0) limitRemainder += width;
                    var offset = numberRemainder - limitRemainder;
                    numberDecimal = lower + (offset < 0 ? offset + width : offset);
                }
            }

            if (limits.IsValid)
            {
                var lower = integer ? NumericIntegerBound(Math.Ceiling(limits.Minimum)) : NumericDecimal(limits.Minimum);
                var upper = integer ? NumericIntegerBound(Math.Floor(limits.Maximum)) : NumericDecimal(limits.Maximum);
                if (lower <= upper)
                    numberDecimal = Math.Clamp(numberDecimal, lower, upper);
            }
            return NumericFromDecimal(numberDecimal, type);
        }

        private static bool IsScalarNumber(Type type) => type != null && !type.IsEnum &&
            Type.GetTypeCode(type) is >= TypeCode.SByte and <= TypeCode.Decimal;

        private static decimal NumericDecimal(double value)
        {
            if (double.IsNaN(value))
                return 0m;
            if (value <= (double)decimal.MinValue)
                return decimal.MinValue;
            if (value >= (double)decimal.MaxValue)
                return decimal.MaxValue;
            return decimal.Parse(value.ToString("R", NumericCulture), NumberStyles.Float, NumericCulture);
        }

        private static decimal NumericIntegerBound(double value)
        {
            if (value >= 0 && value < 18446744073709551616d)
                return (ulong)value;
            if (value < 0 && value >= long.MinValue)
                return (long)value;
            return NumericDecimal(value);
        }

        private static object NumericFromDecimal(decimal value, Type type)
        {
            if (type == typeof(decimal))
                return value;
            return Type.GetTypeCode(type) switch
            {
                TypeCode.SByte => (object)(sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue),
                TypeCode.Byte => (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue),
                TypeCode.Int16 => (short)Math.Clamp(value, short.MinValue, short.MaxValue),
                TypeCode.UInt16 => (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue),
                TypeCode.Int32 => (int)Math.Clamp(value, int.MinValue, int.MaxValue),
                TypeCode.UInt32 => (uint)Math.Clamp(value, uint.MinValue, uint.MaxValue),
                TypeCode.Int64 => (long)Math.Clamp(value, long.MinValue, long.MaxValue),
                TypeCode.UInt64 => (ulong)Math.Clamp(value, ulong.MinValue, ulong.MaxValue),
                _ => value
            };
        }
    }
}
