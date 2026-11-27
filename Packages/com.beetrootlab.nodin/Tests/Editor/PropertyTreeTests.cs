using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nodin.Editor.Tests
{
    public sealed class PropertyTreeTests
    {
        private const string Entry = "entries.Array.data[0]";
        private SerializationFixture _first;
        private SerializationFixture _second;
        private GameObject _instance;
        private string _prefabPath;

        [SetUp]
        public void SetUp()
        {
            _first = ScriptableObject.CreateInstance<SerializationFixture>();
            _second = ScriptableObject.CreateInstance<SerializationFixture>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
            Object.DestroyImmediate(_first);
            Object.DestroyImmediate(_second);
            if (_prefabPath != null) AssetDatabase.DeleteAsset(_prefabPath);
        }

        [Test]
        public void UnityPropertyPathEditsAllTargetsAndCanUndo()
        {
            var a = _first;
            var b = _second;
            b.entries[0] = new SerializationFixture.Entry { distance = 8 };

            using var serialized = new SerializedObject(new Object[] { a, b });
            using var tree = NodinPropertyTree.Create(serialized);
            Undo.IncrementCurrentGroup();
            tree.BeginDraw(true);
            try
            {
                var nested = tree.GetPropertyAtUnityPath(Entry);
                Assert.That(nested.Children.Select(child => child.Name), Does.Contain("distance"));
                var property = tree.GetPropertyAtUnityPath(Entry + ".distance");
                Assert.That(property.SerializedProperty.hasMultipleDifferentValues, Is.True);
                Assert.That(property.GetAttribute<LabelTextAttribute>().Text, Is.EqualTo("Distance"));
                property.Value = 23;
            }
            finally { tree.EndDraw(); }
            Assert.That(a.entries[0].distance, Is.EqualTo(23));
            Assert.That(b.entries[0].distance, Is.EqualTo(23));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(a.entries[0].distance, Is.EqualTo(3));
            Assert.That(b.entries[0].distance, Is.EqualTo(8));
        }

        [Test]
        public void ReflectedSetterWritesBackNestedStruct()
        {
            var fixture = _first;
            using var tree = NodinPropertyTree.Create(fixture);
            tree.BeginDraw();
            try
            {
                var property = tree.GetPropertyAtUnityPath(Entry).Children.Single(child => child.Name == "Distance");
                property.Value = 17;
            }
            finally { tree.EndDraw(); }
            Assert.That(fixture.entries[0].distance, Is.EqualTo(17));
        }

        [Test]
        public void SerializedNestedEditRecordsPrefabOverride()
        {
            const string positionX = "m_LocalPosition.x";
            _prefabPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Nodin.Test.prefab");
            var source = new GameObject("Route");
            GameObject prefab;
            try
            {
                source.transform.localPosition = new Vector3(3f, 0f, 0f);
                prefab = PrefabUtility.SaveAsPrefabAsset(source, _prefabPath);
            }
            finally { Object.DestroyImmediate(source); }

            _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            using var tree = NodinPropertyTree.Create(_instance.transform);
            tree.BeginDraw();
            try { tree.GetPropertyAtUnityPath(positionX).Value = 42f; }
            finally { tree.EndDraw(); }

            using var current = new SerializedObject(_instance.transform);
            Assert.That(current.FindProperty(positionX).prefabOverride, Is.True);
            Assert.That(PrefabUtility.GetPropertyModifications(_instance)
                .Any(modification => modification.propertyPath == positionX && modification.value == "42"), Is.True);
            Assert.That(_instance.transform.localPosition.x, Is.EqualTo(42f));
            Assert.That(prefab.transform.localPosition.x, Is.EqualTo(3f));
        }

        [Test]
        public void OverloadsHaveDistinctPathsAndHonorDynamicLabel()
        {
            var fixture = _first;
            using var tree = NodinPropertyTree.Create(fixture);
            var overloads = tree.RootProperty.Children.Where(child => child.Name == "SampleAction").ToArray();
            Assert.That(overloads.Select(child => child.Path).Distinct().Count(), Is.EqualTo(2));
            Assert.That(overloads.Single(child => child.Path.EndsWith("Int32)")).InspectorLabel.text, Is.EqualTo("Change Hello"));
        }

        [Test]
        public void EnumValuesAreTypedAndPerTargetFlagEditsPreserveOtherBits()
        {
            var a = _first;
            var b = _second;
            a.settings = SerializationFixture.Flags.A | SerializationFixture.Flags.B;
            b.settings = SerializationFixture.Flags.A | SerializationFixture.Flags.C;
            using var serialized = new SerializedObject(new Object[] { a, b });
            using var tree = NodinPropertyTree.Create(serialized);
            tree.BeginDraw();
            try
            {
                var mode = tree.GetPropertyAtUnityPath("mode");
                Assert.That(mode.Value, Is.EqualTo(FogMode.Linear));
                mode.Value = FogMode.ExponentialSquared;
                var flags = tree.GetPropertyAtUnityPath("settings");
                NodinValueControls.SetFlagForAllTargets(flags, (int)SerializationFixture.Flags.A, false);
            }
            finally { tree.EndDraw(); }
            Assert.That(a.mode, Is.EqualTo(FogMode.ExponentialSquared));
            Assert.That(b.mode, Is.EqualTo(FogMode.ExponentialSquared));
            Assert.That(a.settings, Is.EqualTo(SerializationFixture.Flags.B));
            Assert.That(b.settings, Is.EqualTo(SerializationFixture.Flags.C));
        }

        [Test]
        public void CallbackOnlyRunsOnChangedTargetsAndSharesUndo()
        {
            var a = _first;
            var b = _second;
            b.title = "Ready";
            using var serialized = new SerializedObject(new Object[] { a, b });
            using var tree = NodinPropertyTree.Create(serialized);
            Undo.IncrementCurrentGroup();
            tree.BeginDraw();
            try
            {
                var title = tree.GetPropertyAtUnityPath("title");
                var before = title.SnapshotValues();
                title.Value = "Ready";
                title.NotifyChanged(before);
                title.NotifyChanged(title.SnapshotValues());
            }
            finally { tree.EndDraw(); }
            Assert.That(a.callbacks, Is.EqualTo(1));
            Assert.That(b.callbacks, Is.Zero);
            Assert.That(a.title, Is.EqualTo("Ready"));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(a.title, Is.EqualTo("Hello"));
            Assert.That(b.title, Is.EqualTo("Ready"));
            Assert.That(a.callbacks, Is.Zero);
        }

        [Test]
        public void ChangesAppliedIncludesDeferredEditsAndCoalescesDuringDraw()
        {
            var fixture = _first;
            using var tree = NodinPropertyTree.Create(fixture);
            var notifications = 0;
            tree.ChangesApplied += () => notifications++;

            tree.BeginDraw();
            tree.GetPropertyAtUnityPath(Entry + ".distance").Value = 15;
            tree.ApplyModifiedProperties();
            Assert.That(notifications, Is.Zero);
            tree.EndDraw();
            Assert.That(notifications, Is.EqualTo(1));

            // Deferred menu callbacks run after EndDraw.
            tree.GetPropertyAtUnityPath(Entry + ".distance").Value = 18;
            tree.ApplyModifiedProperties();
            Assert.That(fixture.entries[0].distance, Is.EqualTo(18));
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void AddedTableRowsUseDeclaredDefaults()
        {
            var fixture = _first;
            using var tree = NodinPropertyTree.Create(fixture);
            tree.BeginDraw();
            try
            {
                var rows = tree.GetPropertyAtUnityPath("markers").SerializedProperty;
                NodinCollections.AppendElement(rows, typeof(SerializationFixture.MapMarker));
            }
            finally { tree.EndDraw(); }

            Assert.That(fixture.markers.Count, Is.EqualTo(2));
            Assert.That(fixture.markers[0].tint, Is.EqualTo(Color.red));
            Assert.That(fixture.markers[1].tint, Is.EqualTo(Color.white));
        }

        [Test]
        public void FailedInitializerDoesNotAppendARow()
        {
            var fixture = _first;
            using var tree = NodinPropertyTree.Create(fixture);
            tree.BeginDraw();
            try
            {
                var rows = tree.GetPropertyAtUnityPath("unavailableMarkers").SerializedProperty;
                Assert.Throws<System.Reflection.TargetInvocationException>(
                    () => NodinCollections.AppendElement(rows, typeof(SerializationFixture.UnavailableMarker)));
            }
            finally { tree.EndDraw(); }

            Assert.That(fixture.unavailableMarkers, Is.Empty);
        }

        [Test]
        public void BeginDrawFalseLeavesApplyToCaller()
        {
            var fixture = _first;
            using var serialized = new SerializedObject(fixture);
            using var tree = NodinPropertyTree.Create(serialized);
            tree.BeginDraw(false);
            try { tree.GetPropertyAtUnityPath("title").Value = "Edited"; }
            finally { tree.EndDraw(); }
            Assert.That(fixture.title, Is.EqualTo("Hello"));
            Assert.That(serialized.ApplyModifiedProperties(), Is.True);
            Assert.That(fixture.title, Is.EqualTo("Edited"));
        }
    }
}
