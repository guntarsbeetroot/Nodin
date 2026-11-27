using System;
using UnityEngine;

namespace Nodin.Examples
{
    internal sealed class LayoutExamples : ExamplePage
    {
        [TitleGroup("Observatory")]
        [BoxGroup("Observatory/Location")]
        public string station = "Cinder Ridge";

        [BoxGroup("Observatory/Location"), PropertyOrder(-1)]
        public string region = "North Range";

        [FoldoutGroup("Schedule", expanded: true)]
        [HorizontalGroup("Schedule/Window", 0.5f, Gap = 4), LabelWidth(45)]
        public int start = 18;

        [HorizontalGroup("Schedule/Window", 0.5f), LabelWidth(45)]
        public int end = 23;

        [TabGroup("Equipment", "Optics")]
        public float aperture = 4.5f;

        [TabGroup("Equipment", "Mount")]
        public float trackingRate = 1.25f;

        [VerticalGroup("Readings")]
        [PropertySpace(12, 4)]
        public int expectedCaptures = 20;

        [VerticalGroup("Readings")]
        [InlineProperty(LabelWidth = 80)]
        public Coordinates coordinates = new() { elevation = 120, bearing = 15 };

        [Serializable]
        public struct Coordinates
        {
            public int elevation;
            public int bearing;
        }

        [ToggleGroup(nameof(recording), "Capture")]
        public bool recording = true;

        [ToggleGroup(nameof(recording))]
        public string outputLabel = "Session A";

        [InlineButton(nameof(ResetCount), "Reset")]
        public int recordedFrames = 2;

        [Button("Record frame"), ButtonGroup("Capture Actions")]
        private void RecordFrame() => recordedFrames++;

        [ButtonGroup("Capture Actions")]
        private void ResetCount() => recordedFrames = 0;

        [ResponsiveButtonGroup("Shortcuts", DefaultButtonSize = 150)]
        private void SetMorning() => start = 6;

        [ResponsiveButtonGroup("Shortcuts", DefaultButtonSize = 150)]
        private void SetEvening() => start = 18;

        [ResponsiveButtonGroup("Shortcuts", DefaultButtonSize = 150)]
        private void UseDefaultSchedule() => (start, end) = (18, 23);
    }
}
