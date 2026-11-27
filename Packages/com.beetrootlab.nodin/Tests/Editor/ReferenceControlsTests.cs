using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nodin.Editor.Tests
{
    public sealed class ReferenceControlsTests
    {
        [Test]
        public void ReplacingMixedManagedTypesPreservesExistingValuesAndSupportsUndo()
        {
            var unchanged = ScriptableObject.CreateInstance<ReferenceControlsFixture>();
            var replaced = ScriptableObject.CreateInstance<ReferenceControlsFixture>();
            var empty = ScriptableObject.CreateInstance<ReferenceControlsFixture>();
            try
            {
                unchanged.reference = new ReferenceControlsFixture.Preferred { Number = 17 };
                replaced.reference = new ReferenceControlsFixture.Other();
                using var serialized = new SerializedObject(new Object[] { unchanged, replaced, empty });
                using var tree = NodinPropertyTree.Create(serialized);
                var node = tree.GetPropertyAtUnityPath(nameof(ReferenceControlsFixture.reference));

                node.ApplyManagedReferenceType(typeof(ReferenceControlsFixture.Preferred), null);

                Assert.That(((ReferenceControlsFixture.Preferred)unchanged.reference).Number, Is.EqualTo(17));
                Assert.That(replaced.reference, Is.TypeOf<ReferenceControlsFixture.Preferred>());
                Assert.That(empty.reference, Is.TypeOf<ReferenceControlsFixture.Preferred>());
                Assert.That(replaced.reference, Is.Not.SameAs(empty.reference));

                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(((ReferenceControlsFixture.Preferred)unchanged.reference).Number, Is.EqualTo(17));
                Assert.That(replaced.reference, Is.TypeOf<ReferenceControlsFixture.Other>());
                Assert.That(empty.reference, Is.Null);
            }
            finally
            {
                Undo.ClearUndo(unchanged);
                Undo.ClearUndo(replaced);
                Undo.ClearUndo(empty);
                Object.DestroyImmediate(unchanged);
                Object.DestroyImmediate(replaced);
                Object.DestroyImmediate(empty);
            }
        }
    }

    public sealed class ReferenceControlsFixture : ScriptableObject
    {
        [SerializeReference]
        public IReference reference;

        public interface IReference { }

        [Serializable]
        public sealed class Preferred : IReference
        {
            public int Number;
        }

        [Serializable]
        public sealed class Other : IReference { }
    }
}
