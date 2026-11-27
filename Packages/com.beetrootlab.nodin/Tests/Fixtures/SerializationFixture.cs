using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nodin.Editor.Tests
{
    public sealed class SerializationFixture : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [LabelText("Distance")] public int distance;
            [ShowInInspector] public int Distance { get => distance; set => distance = value; }
        }

        [Serializable]
        public sealed class MapMarker
        {
            public Color tint = Color.white;
        }

        [Serializable]
        public sealed class UnavailableMarker
        {
            public int id;
            public UnavailableMarker() => throw new InvalidOperationException("Marker initialization failed.");
        }

        public List<UnavailableMarker> unavailableMarkers = new();

        [TableList]
        public List<MapMarker> markers = new() { new MapMarker { tint = Color.red } };

        [BoxGroup("Route")] public List<Entry> entries = new() { new Entry { distance = 3 } };
        [LabelText("Title"), OnValueChanged(nameof(TitleChanged))] public string title = "Hello";
        public int callbacks;
        [EnumToggleButtons] public FogMode mode = FogMode.Linear;
        [EnumToggleButtons] public Flags settings;

        [System.Flags]
        public enum Flags { A = 1, B = 2, C = 4 }

        [Button, LabelText("@ActionLabel")]
        private void SampleAction(int count) => callbacks += count;

        [Button]
        private void SampleAction(bool active) => callbacks = active ? 1 : 0;

        private string ActionLabel => "Change " + title;
        private void TitleChanged() => callbacks++;
    }
}
