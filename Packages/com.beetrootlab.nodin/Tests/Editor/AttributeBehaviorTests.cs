using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nodin.Editor.Tests
{
    public sealed class AttributeBehaviorFixture : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int amount;

            [OnInspectorDispose(nameof(Dispose))]
            public int marker;

            private void Dispose() => marker++;
        }

        public Entry entry;
        public bool visible = true;
        public int initialized;
        public int disposed;
        public int countBefore = -1;
        public int countAfter = -1;

        [OnCollectionChanged(nameof(Before), nameof(After))]
        public int[] numbers = { 1, 2 };

        [OnStateUpdate(nameof(UpdateState))]
        public int conditional;

        [OnInspectorInit]
        private void Initialize() => initialized++;

        [OnInspectorDispose]
        private void Dispose() => disposed++;

        private void UpdateState(NodinPropertyState state) => state.Visible = visible;

        private void Before(CollectionChangeInfo change, int[] list) => countBefore = list.Length;
        private void After(CollectionChangeInfo change, int[] list) => countAfter = list.Length;
    }

    public sealed class AttributeBehaviorTests
    {
        private AttributeBehaviorFixture _fixture;

        [SetUp]
        public void SetUp() => _fixture = ScriptableObject.CreateInstance<AttributeBehaviorFixture>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_fixture);

        [Test]
        public void InspectorLifecyclesRunOnceAndDisposeUsesCurrentStruct()
        {
            using var tree = NodinPropertyTree.Create(_fixture);
            tree.BeginDraw(false);
            tree.EndDraw();
            Assert.That(_fixture.initialized, Is.Zero);

            tree.BeginDraw();
            tree.GetPropertyAtUnityPath("entry.marker").UpdateLifecycle();
            tree.GetPropertyAtUnityPath("entry.amount").Value = 15;
            tree.EndDraw();

            tree.BeginDraw();
            tree.GetPropertyAtUnityPath("entry.marker").UpdateLifecycle();
            tree.EndDraw();
            tree.Dispose();
            tree.Dispose();

            Assert.That(_fixture.initialized, Is.EqualTo(1));
            Assert.That(_fixture.disposed, Is.EqualTo(1));
            Assert.That(_fixture.entry.amount, Is.EqualTo(15));
            Assert.That(_fixture.entry.marker, Is.EqualTo(1));
        }

        [Test]
        public void StateUpdateChangesVisibilityBetweenDraws()
        {
            using var tree = NodinPropertyTree.Create(_fixture);
            tree.BeginDraw();
            Assert.That(tree.GetPropertyAtUnityPath("conditional").IsVisible(), Is.True);
            tree.EndDraw();
            _fixture.visible = false;
            tree.BeginDraw();
            Assert.That(tree.GetPropertyAtUnityPath("conditional").IsVisible(), Is.False);
            tree.EndDraw();
        }

        [Test]
        public void ReflectedSequenceEditsPreserveEnumerationOrder()
        {
            var stack = new Stack<int>(new[] { 3, 2, 1 });
            var queue = new Queue<int>(new[] { 1, 2, 3 });
            var linked = new LinkedList<int>(new[] { 1, 2, 3 });
            NodinCollections.ReplaceSequenceElement(stack, typeof(Stack<>), 1, 9);
            NodinCollections.ReplaceSequenceElement(queue, typeof(Queue<>), 1, 9);
            NodinCollections.ReplaceSequenceElement(linked, typeof(LinkedList<>), 1, 9);
            Assert.That(stack, Is.EqualTo(new[] { 1, 9, 3 }));
            Assert.That(queue, Is.EqualTo(new[] { 1, 9, 3 }));
            Assert.That(linked, Is.EqualTo(new[] { 1, 9, 3 }));
        }

        [Test]
        public void ElementEditPreservesBeforeHookChanges()
        {
            using var tree = NodinPropertyTree.Create(_fixture);
            tree.BeginDraw();
            var node = tree.GetPropertyAtUnityPath("numbers.Array.data[1]");
            node.Value = 7;
            node.NotifySerializedCollectionElement();
            tree.EndDraw();

            Assert.That(_fixture.numbers, Is.EqualTo(new[] { 1, 7 }));
            Assert.That(_fixture.countBefore, Is.EqualTo(2));
            Assert.That(_fixture.countAfter, Is.EqualTo(2));
        }

        [Test]
        public void SerializedCollectionHooksSeeOldAndNewValuesAndUndoTogether()
        {
            using var tree = NodinPropertyTree.Create(_fixture);
            Undo.IncrementCurrentGroup();
            tree.BeginDraw();
            var node = tree.GetPropertyAtUnityPath("numbers");
            NodinCollections.EditArray(node, node.SerializedProperty, new CollectionChangeInfo(CollectionChangeType.Add, 2),
                array => NodinCollections.AppendElement(array, typeof(int)));
            tree.EndDraw();

            Assert.That(_fixture.countBefore, Is.EqualTo(2));
            Assert.That(_fixture.countAfter, Is.EqualTo(3));
            Assert.That(_fixture.numbers.Length, Is.EqualTo(3));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(_fixture.numbers.Length, Is.EqualTo(2));
            Assert.That(_fixture.countBefore, Is.EqualTo(-1));
            Assert.That(_fixture.countAfter, Is.EqualTo(-1));
        }
    }
}
