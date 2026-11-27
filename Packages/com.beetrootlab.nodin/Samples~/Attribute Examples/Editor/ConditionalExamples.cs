using System;
using UnityEngine;

namespace Nodin.Examples
{
    internal sealed class ConditionalExamples : ExamplePage
    {
        [ToggleLeft]
        public bool advanced;

        [ToggleLeft]
        public bool locked;

        [ShowIf(nameof(advanced))]
        public string advancedNotes = "Check the backup channel.";

        [HideIf(nameof(advanced))]
        public string simplifiedNotes = "Default operating profile";

        [EnableIf(nameof(advanced))]
        public int manualRate = 10;

        [DisableIf("@locked || !advanced")]
        public int protectedRate = 15;

        [ShowIfGroup("Advanced Options", Condition = nameof(advanced))]
        [BoxGroup("Advanced Options/Filter")]
        public float signalThreshold = 0.35f;

        [HideIfGroup("Normal Options", Condition = nameof(advanced))]
        [BoxGroup("Normal Options/Filter")]
        public bool automaticThreshold = true;

        [HideInPlayMode]
        public string editModeNote = "Edit mode only";

        [HideInEditorMode]
        public string playModeNote = "Play mode only";

        [DisableInPlayMode]
        public int startupDelay = 4;

        [DisableInEditorMode]
        public int liveIterations = 6;

        [Toggle(nameof(Receiver.enabled))]
        public Receiver receiver = new();

        [Serializable]
        public sealed class Receiver
        {
            public bool enabled = true;
            public string channel = "Telemetry";
            public int frequency = 9;
        }
    }
}
