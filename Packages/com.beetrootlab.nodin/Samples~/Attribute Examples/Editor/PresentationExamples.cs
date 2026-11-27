using UnityEditor;
using UnityEngine;

namespace Nodin.Examples
{
    [TypeInfoBox("These controls change how the same serialized data appears in the inspector.")]
    internal sealed class PresentationExamples : ExamplePage
    {
        [Title("Dispatch notes", "Presentation and Unity drawer examples", TitleAlignments.Split)]
        [LabelText("Call sign")]
        [PropertyTooltip("$CallsignHelp")]
        [LabelWidth(75)]
        public string stationName = "Kestrel";

        [HideLabel, DisplayAsString]
        public string log = "Backup channel standing by";

        [GUIColor(0.68f, 0.86f, 1)]
        [ReadOnly]
        public int sessionId = 184;

        [ShowInInspector, EnableGUI, DisplayAsString]
        private string Status => $"{stationName} / {sessionId}";

        [Indent(1)]
        [InfoBox("Dispatch is paused until a supervisor approves it.", InfoMessageType.Warning, nameof(paused))]
        public bool paused = true;

        [DetailedInfoBox("About this dispatch", "The notes below are local to this sample. Edit them to compare the available text controls.")]
        [MultiLineProperty(3)]
        public string dispatch = "Check the eastern relay.\nIf the signal drops, switch channels.";

        [Space(8), Multiline(2)]
        public string unityMultiline = "A fixed-height Unity control";

        [TextArea(2, 4), DrawWithUnity]
        public string unityTextArea = "This control uses the Unity drawer.";

        [CustomValueDrawer(nameof(DrawSignal))]
        public float signalStrength = 0.7f;

        [FilePath(ParentFolder = "Assets", Extensions = "unity", RequireExistingPath = true)]
        public string sceneFile = "";

        [FolderPath(RequireExistingPath = true)]
        public string outputFolder = "Assets";

        private string CallsignHelp => $"The current station is {stationName}.";

        private float DrawSignal(float value, GUIContent label) => EditorGUILayout.Slider(label, value, 0, 1);
    }
}
