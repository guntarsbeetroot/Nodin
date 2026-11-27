using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nodin.Examples
{
    internal sealed class ReferenceExamples : ExamplePage
    {
        [AssetList(AssetNamePrefix = "Nodin Sample", AutoPopulate = true)]
        [ListDrawerSettings(Expanded = true)]
        public List<Material> sampleMaterials = new();

        [AssetSelector("Nodin Sample t:Material", DropdownTitle = "sample material")]
        public Material selectedMaterial;

        [PreviewField(52, ObjectFieldAlignment.Left)]
        public Texture2D previewTexture;

        [AssetsOnly]
        public Material persistentMaterial;

        [InlineEditor(Expanded = true, ObjectFieldMode = InlineEditorObjectFieldModes.Hidden)]
        public InlineDetailExample inlineDetails;

        [ColorPalette(nameof(StationColors), ShowAlpha = false)]
        public Color panelColor = new(0.23f, 0.68f, 0.71f);

        [SerializeReference, TypeFilter(nameof(AntennaTypes))]
        [PolymorphicDrawerSettings(ShowBaseType = true)]
        public Antenna searchAntenna = new Dish();

        [SerializeReference, HideReferenceObjectPicker]
        public Antenna fixedAntenna = new Loop();

        [SerializeReference, PropertyOrder(2)]
        public Antenna sharedAntenna;

        [SerializeReference, HideDuplicateReferenceBox, PropertyOrder(3)]
        public Antenna sharedWithoutNotice;

        [ShowInInspector, TypeDrawerSettings(BaseType = typeof(Antenna), Filter = TypeInclusionFilter.IncludeConcreteTypes)]
        [TypeSelectorSettings(ShowCategories = true, PreferNamespaces = false, ShowNoneItem = false)]
        public Type AntennaType { get; set; } = typeof(Dish);

        private IEnumerable<string> StationColors => new[] { "#3BADB5", "#F2AB58", "#6870CA", "#29344B" };

        private IEnumerable<Type> AntennaTypes => new[] { typeof(Dish), typeof(Loop) };

        internal void Setup(InlineDetailExample detail, Material material, Texture2D texture)
        {
            inlineDetails = detail;
            previewTexture = texture;
            selectedMaterial = persistentMaterial = material;
            sharedAntenna = sharedWithoutNotice = searchAntenna;
        }

        [Serializable]
        public abstract class Antenna
        {
            public int gain = 8;
        }

        [Serializable, TypeRegistryItem(Name = "Parabolic dish", CategoryPath = "Telemetry", Priority = 1)]
        public sealed class Dish : Antenna
        {
            public float diameter = 1.25f;
        }

        [Serializable, TypeRegistryItem(Name = "Compact loop", CategoryPath = "Telemetry", Priority = 2)]
        public sealed class Loop : Antenna
        {
            public int turns = 3;
        }
    }
}
