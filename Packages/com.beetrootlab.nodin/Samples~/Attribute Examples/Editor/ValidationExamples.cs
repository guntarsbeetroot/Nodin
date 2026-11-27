using System;
using UnityEngine;

namespace Nodin.Examples
{
    internal sealed class ValidationExamples : ExamplePage
    {
        [Required("Enter a run identifier.")]
        public string runId = "";

        [ValidateInput(nameof(ValidatePort), "Enter a port in the allowed range.", InfoMessageType.Warning)]
        public int port = 90;

        public int expectedCheckpoints = 3;

        [RequiredListLength(nameof(expectedCheckpoints))]
        [ListDrawerSettings(ShowFoldout = false)]
        public string[] checkpoints = { "Arrival", "Departure" };

        [ShowDrawerChain, ShowPropertyResolver]
        [PropertyRange(0, 10)]
        public float diagnosticValue = 4;

        [SuppressInvalidAttributeError, Range(0, 10)]
        [PropertyTooltip("The numeric range is deliberately applied to a string. The diagnostic is suppressed.")]
        public string incompatibleRange = "Plain text";

        private bool ValidatePort(int value, ref string message, ref InfoMessageType type)
        {
            if (value is >= 1024 and <= 49151)
                return true;
            message = $"{value} is outside the allowed range (1024–49151).";
            type = value < 0 ? InfoMessageType.Error : InfoMessageType.Warning;
            return false;
        }
    }
}
