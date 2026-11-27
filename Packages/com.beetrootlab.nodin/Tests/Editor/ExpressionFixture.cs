using UnityEngine;

namespace Nodin.Editor.Tests
{
    public class ExpressionFixture
    {
        public enum MetadataType { Texture, Material }
        public class Signal { public Color color = Color.cyan; public Color StatusColor => Color.cyan; }
        public class ItemData { public string id = "item"; }
        public bool _hasAuthToken, controlledByTimeline, groupEnabled, useDefaults, useCustomSettings;
        public bool IsAndroid = true, IsWebGL, buildIsFinished = true, controlledBySignal = true, enabled = true;
        public int buildStep = 1, amount = 2, ChildrenCount = 2;
        public string id = "id", text = "text", categoryId = "category", itemId = "item", presetId = "preset", groupName = "group", entryId = "entry";
        public object[] items = { new object() };
        public FogMode fogMode = FogMode.Linear;
        public object SelectedObject = new object(), previewSettings = new object();
        public Object previewAsset;
        public MetadataType metadataType = MetadataType.Material;
        public ItemData itemData = new ItemData();
        public string Name => "fixture-name";
        private bool CanBuild() => true;
        private string GetBuildButtonLabel() => "fixture-build";
        private string GetTitle() => "fixture-title";
        private string HighlightTitle => "fixture-highlight";
        private string GetButtonName() => "fixture-button";
        private Color GetButtonColor() => Color.cyan;
        public Signal loading = new Signal();
        public Signal inEditor = new Signal();
        public Signal header = new Signal();
        public Signal thumbnail = new Signal();
        public Signal background = new Signal();
        public Signal foreground = new Signal();
        public Signal outline = new Signal();
        public Signal selection = new Signal();
        public Signal highlight = new Signal();
        public Signal help = new Signal();
        public Signal summary = new Signal();
        public Signal warning = new Signal();
        public Signal error = new Signal();
        public Signal primaryAction = new Signal();
        public Signal secondaryAction = new Signal();
        public Signal addAction = new Signal();
        public Signal removeAction = new Signal();
        public Signal categories = new Signal();
        public Signal labels = new Signal();
        public Signal icons = new Signal();
        public Signal preview = new Signal();
        public Signal navigation = new Signal();
        public Signal overlays = new Signal();
        public Signal audio = new Signal();
        public Signal Overview = new Signal();
        public Signal Input = new Signal();
        public Signal Output = new Signal();
        public Signal Defaults = new Signal();
        public Signal Overrides = new Signal();
        public Signal Rendering = new Signal();
        public Signal Lighting = new Signal();
        public Signal Materials = new Signal();
        public Signal Textures = new Signal();
        public Signal Geometry = new Signal();
        public Signal Animation = new Signal();
        public Signal Import = new Signal();
        public Signal Export = new Signal();
    }
}
