using System;
using UnityEngine;

namespace Nodin.Examples
{
    internal sealed class NumericExamples : ExamplePage
    {
        [Serializable, Flags]
        public enum Sensors { None = 0, Camera = 1, Thermal = 2, Depth = 4 }

        public enum Calibration { Manual, Assisted, Automatic }

        [Title("Power limits")]
        [MinValue(1)]
        public int minimumCycles = 3;

        [MaxValue(48)]
        public int maximumBays = 12;

        [Range(0, 1)]
        public float unityRange = 0.65f;

        public float ceiling = 72;

        [PropertyRange(0, nameof(ceiling))]
        public float output = 32;

        [ProgressBar(0, nameof(ceiling), Height = 20)]
        public float batteryCharge = 44;

        [MinMaxSlider(-20, 60, true)]
        public Vector2 operatingTemperature = new(4, 28);

        [Unit("m", "cm", 100)]
        public float spacingInMeters = 0.375f;

        [Wrap(0, 360), SuffixLabel("degrees")]
        public float antennaHeading = 24;

        [Title("Input modes")]
        [DelayedProperty, OnValueChanged(nameof(CountCommit))]
        public string channelName = "Orbital";

        [ShowInInspector, ReadOnly]
        public int CommittedEdits { get; private set; }

        [EnumPaging]
        public Calibration calibration = Calibration.Assisted;

        [EnumToggleButtons]
        public Sensors enabledSensors = Sensors.Camera | Sensors.Depth;

        [ToggleLeft]
        public bool allowManualInput = true;

        private void CountCommit() => CommittedEdits++;
    }
}
