using UnityEditor;
using UnityEngine;

namespace Nodin.Editor
{
    public class NodinBase : UnityEditor.Editor
    {
        private NodinPropertyTree _tree;
        protected NodinPropertyTree PropertyTree => _tree ??= NodinPropertyTree.Create(serializedObject);

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;
            if (PropertyTree.HasCustomization())
                PropertyTree.Draw();
            else
                DrawDefaultInspector();
        }

        protected virtual void OnDisable()
        {
            _tree?.Dispose();
            _tree = null;
        }
    }

    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true), CanEditMultipleObjects]
    internal sealed class NodinBehaviourEditor : NodinBase { }

    [CustomEditor(typeof(ScriptableObject), true, isFallback = true), CanEditMultipleObjects]
    internal sealed class NodinAssetEditor : NodinBase { }

    public class NodinWindow : EditorWindow
    {
        private NodinPropertyTree _tree;
        private Vector2 _scrollPosition;

        protected NodinPropertyTree PropertyTree => _tree ??= NodinPropertyTree.Create(this);

        protected virtual void OnGUI()
        {
            using var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition);
            _scrollPosition = scroll.scrollPosition;
            DrawContent();
        }

        protected virtual void DrawContent() => PropertyTree.Draw();

        protected virtual void OnDisable()
        {
            _tree?.Dispose();
            _tree = null;
        }
    }
}
