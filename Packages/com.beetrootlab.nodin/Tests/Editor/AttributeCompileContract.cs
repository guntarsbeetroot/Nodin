// Compiles representative uses of attribute constructors and named arguments.
namespace Nodin.Editor.Tests
{
    internal enum LODPreset { Custom }

    public sealed class AttributeCompileContract
    {
        [ShowIf("Value")]
        public string Contract0;

        [ShowIf("Value", UnityEngine.FogMode.Linear)]
        public string Contract1;

        [ShowIf("Value", LODPreset.Custom)]
        public string Contract2;

        [Button]
        private void Contract3(int amount = 1) { }

        [Button(ButtonSizes.Large)]
        private void Contract4(int amount = 1) { }

        [Button("Value", ButtonSizes.Large)]
        private void Contract5(int amount = 1) { }

        [Button("Value")]
        private void Contract6(int amount = 1) { }

        [Button(ButtonSizes.Gigantic)]
        private void Contract7(int amount = 1) { }

        [Button("Value", ButtonSizes.Medium)]
        private void Contract8(int amount = 1) { }

        [Button("Value", ButtonSizes.Gigantic)]
        private void Contract9(int amount = 1) { }

        [Button(ButtonSizes.Large, Name = "Value")]
        private void Contract10(int amount = 1) { }

        [Button(ButtonSizes.Small)]
        private void Contract11(int amount = 1) { }

        [EnableIf("Value")]
        public string Contract12;

        [ReadOnly]
        public string Contract13;

        [Searchable]
        public string Contract14;

        [Searchable(FuzzySearch = false)]
        public string Contract15;

        [ListDrawerSettings]
        public string Contract16;

        [ListDrawerSettings(Expanded = true, DraggableItems = false, HideAddButton = true, HideRemoveButton = true, NumberOfItemsPerPage = 20)]
        public string Contract17;

        [ListDrawerSettings(ShowIndexLabels = true, Expanded = true)]
        public string Contract18;

        [ListDrawerSettings(ListElementLabelName = "Value", DefaultExpandedState = false, ShowPaging = true, NumberOfItemsPerPage = 10)]
        public string Contract19;

        [ListDrawerSettings(ListElementLabelName = "Value")]
        public string Contract20;

        [ListDrawerSettings(ShowPaging = true, NumberOfItemsPerPage = 10)]
        public string Contract21;

        [ListDrawerSettings(DefaultExpandedState = false, ShowPaging = true, NumberOfItemsPerPage = 10)]
        public string Contract22;

        [ListDrawerSettings(ShowIndexLabels = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
        public string Contract23;

        [ListDrawerSettings(ListElementLabelName = "Value", DefaultExpandedState = false,
        ShowPaging = false)]
        public string Contract24;

        [ListDrawerSettings(ListElementLabelName = "Value", DefaultExpandedState = false,
            ShowIndexLabels = true, ShowPaging = false)]
        public string Contract25;

        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "Value", NumberOfItemsPerPage = 10, DefaultExpandedState = false)]
        public string Contract26;

        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "Value", DefaultExpandedState = false)]
        public string Contract27;

        [ListDrawerSettings(ShowIndexLabels = true)]
        public string Contract28;

        [ListDrawerSettings(ShowIndexLabels = true, DefaultExpandedState = true)]
        public string Contract29;

        [ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 10, DefaultExpandedState = false)]
        public string Contract30;

        [ListDrawerSettings(ListElementLabelName = "Value", DefaultExpandedState = false)]
        public string Contract31;

        [ListDrawerSettings(ShowIndexLabels = true, NumberOfItemsPerPage = 10)]
        public string Contract32;

        [ListDrawerSettings(ShowIndexLabels = true, DefaultExpandedState = false)]
        public string Contract33;

        [ListDrawerSettings(ListElementLabelName = "Value", DefaultExpandedState = false, ShowPaging = true, NumberOfItemsPerPage = 20)]
        public string Contract34;

        [ListDrawerSettings(ListElementLabelName = "Value", ShowPaging = true, NumberOfItemsPerPage = 10)]
        public string Contract35;

        [Required]
        public string Contract36;

        [LabelText("Value")]
        public string Contract37;

        [FoldoutGroup("Value")]
        public string Contract38;

        [FoldoutGroup("Value", Order = 2)]
        public string Contract39;

        [FoldoutGroup("Value" + "Value")]
        public string Contract40;

        [FoldoutGroup("Value", false, order: 2)]
        public string Contract41;

        [FoldoutGroup("Value", false, order: 3)]
        public string Contract42;

        [FoldoutGroup("Value", false, order: 4)]
        public string Contract43;

        [FoldoutGroup("Value", GroupName = "Value")]
        public string Contract44;

        [FoldoutGroup("Value", expanded: true)]
        public string Contract45;

        [AssetsOnly]
        public string Contract46;

        [BoxGroup("Value" + "Value")]
        public string Contract47;

        [BoxGroup("Value")]
        public string Contract48;

        [BoxGroup("Value", Order = 0)]
        public string Contract49;

        [BoxGroup("Value", CenterLabel = true)]
        public string Contract50;

        [BoxGroup("Value", order: 0)]
        public string Contract51;

        [BoxGroup("Value", order: 1)]
        public string Contract52;

        [BoxGroup("Value", ShowLabel = true, CenterLabel = true)]
        public string Contract53;

        [BoxGroup("Value", ShowLabel = false, Order = -10)]
        public string Contract54;

        [BoxGroup("Value", CenterLabel = true, Order = 10)]
        public string Contract55;

        [BoxGroup("Value", false)]
        public string Contract56;

        [InlineProperty]
        public string Contract57;

        [HideLabel]
        public string Contract58;

        [ShowInInspector]
        public string Contract59;

        [ButtonGroup("Value")]
        public string Contract60;

        [TitleGroup("Value", "Value")]
        public string Contract61;

        [TitleGroup("Value")]
        public string Contract62;

        [SceneObjectsOnly]
        public string Contract63;

        [GUIColor(0.4f, 0.8f, 1f)]
        public string Contract64;

        [GUIColor(0.4f, 0.9f, 0.4f)]
        public string Contract65;

        [GUIColor(1, 1, 1, 0.5f)]
        public string Contract66;

        [GUIColor(1, 0.75f, 0.75f)]
        public string Contract67;

        [GUIColor("Value")]
        public string Contract68;

        [GUIColor(0.75f, 0.75f, 0.75f, 1)]
        public string Contract69;

        [GUIColor(0, 1, 0, 1)]
        public string Contract70;

        [GUIColor(1, 0, 0, 1)]
        public string Contract71;

        [GUIColor(0, 1, 0)]
        public string Contract72;

        [GUIColor(1, 0, 0)]
        public string Contract73;

        [GUIColor(0.3f, 1f, 0.3f)]
        public string Contract74;

        [GUIColor(0.4f, 0.7f, 1f)]
        public string Contract75;

        [GUIColor(1f, 0.3f, 0.3f)]
        public string Contract76;

        [GUIColor(1f, 0.5f, 0.3f)]
        public string Contract77;

        [GUIColor(0.1f, 1, 0.1f)]
        public string Contract78;

        [GUIColor(0.1f, 1f, 0.1f, 1)]
        public string Contract79;

        [InlineEditor]
        public string Contract80;

        [InlineEditor(Expanded = true)]
        public string Contract81;

        [InlineEditor(ObjectFieldMode = InlineEditorObjectFieldModes.Hidden)]
        public string Contract82;

        [PropertyOrder]
        public string Contract83;

        [PropertyOrder(10)]
        public string Contract84;

        [PropertyOrder(-10)]
        public string Contract85;

        [PropertyOrder(1)]
        public string Contract86;

        [PropertyOrder(1.5f)]
        public string Contract87;

        [PropertyOrder(1.51f)]
        public string Contract88;

        [PropertyOrder(1.6f)]
        public string Contract89;

        [PropertyOrder(1.7f)]
        public string Contract90;

        [PropertyOrder(2)]
        public string Contract91;

        [PropertyOrder(2.1f)]
        public string Contract92;

        [PropertyOrder(2.15f)]
        public string Contract93;

        [PropertyOrder(2.2f)]
        public string Contract94;

        [PropertyOrder(2.21f)]
        public string Contract95;

        [PropertyOrder(2.22f)]
        public string Contract96;

        [PropertyOrder(3)]
        public string Contract97;

        [PropertyOrder(5)]
        public string Contract98;

        [PropertyOrder(5.1f)]
        public string Contract99;

        [PropertyOrder(6)]
        public string Contract100;

        [PropertyOrder(-20)]
        public string Contract101;

        [PropertyOrder(-15)]
        public string Contract102;

        [PropertyOrder(-100)]
        public string Contract103;

        [PropertyOrder(0)]
        public string Contract104;

        [PropertyOrder(-2)]
        public string Contract105;

        [PropertyOrder(-1)]
        public string Contract106;

        [TabGroup("Value", "Value")]
        public string Contract107;

        [TabGroup("Value", "Value", order: 0)]
        public string Contract108;

        [TabGroup("Value")]
        public string Contract109;

        [ToggleLeft]
        public string Contract110;

        [HideIf("Value")]
        public string Contract111;

        [ValueDropdown("Value")]
        public string Contract112;

        [ValueDropdown("Value", IsUniqueList = true)]
        public string Contract113;

        [InfoBox("Value")]
        public string Contract114;

        [InfoBox("Value", InfoMessageType.None)]
        public string Contract115;

        [InfoBox("Value", InfoMessageType.Info)]
        public string Contract116;

        [InfoBox("Value", InfoMessageType.Warning)]
        public string Contract117;

        [InfoBox("Value", InfoMessageType.None, "Value")]
        public string Contract118;

        [InfoBox("Value", InfoMessageType.Info, "Value")]
        public string Contract119;

        [CustomValueDrawer("Value")]
        public string Contract120;

        [MinValue(0f)]
        public string Contract121;

        [MinValue(0)]
        public string Contract122;

        [MinValue(1)]
        public string Contract123;

        [SuffixLabel("Value", true)]
        public string Contract124;

        [SuffixLabel("Value", Overlay = true)]
        public string Contract125;

        [HorizontalGroup("Value", Width = 56)]
        public string Contract126;

        [HorizontalGroup("Value", Width = 104)]
        public string Contract127;

        [HorizontalGroup("Value")]
        public string Contract128;

        [HorizontalGroup("Value", Width = 115)]
        public string Contract129;

        [HorizontalGroup("Value", Width = 120)]
        public string Contract130;

        [HorizontalGroup("Value", 62)]
        public string Contract131;

        [HorizontalGroup("Value", Width = 0.35f)]
        public string Contract132;

        [HorizontalGroup("Value", Width = 0.3f)]
        public string Contract133;

        [HorizontalGroup("Value", Width = 0.15f)]
        public string Contract134;

        [HorizontalGroup("Value", Width = 0.65f)]
        public string Contract135;

        [HorizontalGroup("Value", Width = 0.5f)]
        public string Contract136;

        [HorizontalGroup("Value", Width = 0.7f)]
        public string Contract137;

        [HorizontalGroup("Value", Width = 22)]
        public string Contract138;

        [HorizontalGroup("Value", Width = 90)]
        public string Contract139;

        [OnInspectorGUI]
        private void Contract140(int amount = 1) { }

        [DisableIf("Value")]
        public string Contract141;

        [OnValueChanged("Value")]
        public string Contract142;

        [DisableInEditorMode]
        public string Contract143;

        [Title("Value")]
        public string Contract144;

        [Title("Value", "Value")]
        public string Contract145;

        [PreviewField]
        public string Contract146;

        [PreviewField(48)]
        public string Contract147;

        [PreviewField(ObjectFieldAlignment.Left)]
        public string Contract148;

        [PreviewField(64)]
        public string Contract149;

        [PreviewField(80, ObjectFieldAlignment.Left)]
        public string Contract150;

        [PreviewField(40)]
        public string Contract151;

        [EnumToggleButtons]
        public string Contract152;

        [InlineButton("Value", "Value")]
        public string Contract153;

        [InlineButton("Value", Label = "Value")]
        public string Contract154;

        [HideInEditorMode]
        public string Contract155;

        [TableList]
        public string Contract156;

        [TableList(ShowPaging = true, NumberOfItemsPerPage = 10)]
        public string Contract157;

        [TableList(AlwaysExpanded = true)]
        public string Contract158;

        [TableList(AlwaysExpanded = true, ShowPaging = true, NumberOfItemsPerPage = 10)]
        public string Contract159;

        [TableList(AlwaysExpanded = true, ShowPaging = true, NumberOfItemsPerPage = 5)]
        public string Contract160;

        [DisplayAsString]
        public string Contract161;

        [DisplayAsString(false)]
        public string Contract162;

        [Toggle("Value")]
        public string Contract163;

        [TableColumnWidth(100)]
        public string Contract164;

        [TableColumnWidth(160)]
        public string Contract165;

        [LabelWidth(75)]
        public string Contract166;

        [VerticalGroup("Value")]
        public string Contract167;

        [FolderPath]
        public string Contract168;

        [FolderPath(ParentFolder = "Value")]
        public string Contract169;

        [FilePath]
        public string Contract170;

        [FilePath(Extensions = "Value", AbsolutePath = true)]
        public string Contract171;

        [FilePath(ParentFolder = "Value", Extensions = "Value")]
        public string Contract172;

        [ValidateInput("Value")]
        public string Contract173;

        [ValidateInput("Value", "Value")]
        public string Contract174;

        [MultiLineProperty]
        public string Contract175;

        [MultiLineProperty(12)]
        public string Contract176;

        [PropertySpace]
        public string Contract177;

        [PropertySpace(0, 10)]
        public string Contract178;
    }
}
