using System;
using System.Diagnostics;

namespace Nodin
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class AssetSelectorAttribute : Attribute
    {
        public AssetSelectorAttribute(string filter = null) => Filter = filter;

        public string Filter { get; set; }
        public string Paths { get; set; }
        public string DropdownTitle { get; set; }
        public bool FlattenTreeView { get; set; }
        public bool DrawDropdownForListElements { get; set; } = true;
        public bool IsUniqueList { get; set; } = true;
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class AssetListAttribute : Attribute
    {
        public string Path { get; set; }
        public string AssetNamePrefix { get; set; }
        public string Tags { get; set; }
        public string LayerNames { get; set; }
        public string CustomFilterMethod { get; set; }
        public bool AutoPopulate { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class ChildGameObjectsOnlyAttribute : Attribute
    {
        public bool IncludeSelf { get; set; } = true;
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class ColorPaletteAttribute : Attribute
    {
        public ColorPaletteAttribute(string paletteName = null) => PaletteName = paletteName;

        public string PaletteName { get; }
        public bool ShowAlpha { get; set; } = true;
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Interface,
        Inherited = true)]
    public sealed class HideReferenceObjectPickerAttribute : Attribute { }

    public enum NonDefaultConstructorPreference
    {
        Exclude,
        ConstructIdeal,
        PreferUninitialized,
        LogWarning
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class PolymorphicDrawerSettingsAttribute : Attribute
    {
        public bool ShowBaseType { get; set; }
        public bool ReadOnlyIfNotNullReference { get; set; }
        public NonDefaultConstructorPreference NonDefaultConstructorPreference { get; set; }
        public string CreateInstanceFunction { get; set; }
    }

    [Flags]
    public enum TypeInclusionFilter
    {
        IncludeConcreteTypes = 1,
        IncludeInterfaces = 2,
        IncludeAbstracts = 4,
        IncludeGenerics = 8,
        All = IncludeConcreteTypes | IncludeInterfaces | IncludeAbstracts | IncludeGenerics
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class TypeDrawerSettingsAttribute : Attribute
    {
        public Type BaseType { get; set; }
        public TypeInclusionFilter Filter { get; set; } = TypeInclusionFilter.All;
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class TypeFilterAttribute : Attribute
    {
        public TypeFilterAttribute(string member) => Member = member;

        public string Member { get; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class TypeSelectorSettingsAttribute : Attribute
    {
        public bool ShowCategories { get; set; } = true;
        public bool PreferNamespaces { get; set; } = true;
        public bool ShowNoneItem { get; set; } = true;
        public string FilterTypesFunction { get; set; }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum,
        Inherited = false)]
    public sealed class TypeRegistryItemAttribute : Attribute
    {
        public string Name { get; set; }
        public string CategoryPath { get; set; }
        public int Priority { get; set; }
    }
}
