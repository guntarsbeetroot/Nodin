using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nodin.Examples
{
    internal sealed class CallbackExamples : ExamplePage
    {
        [CustomContextMenu("Restore default", nameof(RestoreDefault))]
        [PropertyTooltip("Right-click to restore the default name.")]
        [OnValueChanged(nameof(OnChanged))]
        public string layoutName = "Beacon";

        [OnCollectionChanged(nameof(BeforeCollectionChange), nameof(AfterCollectionChange))]
        [ListDrawerSettings(Expanded = true)]
        [DisableContextMenu(disableForMember: false, disableCollectionElements: true)]
        public List<string> tags = new() { "Outer", "Inner" };

        [ToggleLeft]
        [OnInspectorGUI(nameof(DrawEventLog), append: true)]
        public bool showDetail;

        [OnStateUpdate(nameof(UpdateDetail))]
        public string detail = "Shown by the state callback.";

        private string _lastEvent = "Waiting for initialization";

        [OnInspectorInit]
        private void InitializePage() => _lastEvent = "This page's inspector initialized.";

        [OnInspectorDispose]
        private void DisposePage() => Debug.Log("The callback example's inspector was disposed.");

        private void RestoreDefault()
        {
            layoutName = "Beacon";
            _lastEvent = "Restored from the context menu.";
        }

        private void OnChanged() => _lastEvent = $"Changed to {layoutName}";

        private void BeforeCollectionChange(CollectionChangeInfo change, List<string> current) =>
            _lastEvent = $"{change.ChangeType} at {change.Index}, starting from {current.Count} items";

        private void AfterCollectionChange(CollectionChangeInfo change, List<string> current) =>
            _lastEvent += $" → {current.Count} items";

        private void UpdateDetail(NodinPropertyState state) => state.Visible = showDetail;

        private void DrawEventLog() => EditorGUILayout.HelpBox(_lastEvent, MessageType.Info);
    }
}
