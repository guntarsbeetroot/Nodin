using System.IO;
using Nodin.Editor;
using UnityEditor;
using UnityEngine;

namespace Nodin.Examples
{
    [HideMonoScript]
    internal abstract class ExamplePage : ScriptableObject { }

    internal sealed class AttributeExamplesWindow : EditorWindow
    {
        private enum Page { Numbers, Layout, Conditions, Presentation, Validation, Collections, Callbacks, References, PrefabsAndScene }

        [SerializeField] private Page _page;
        private ExamplePage _example;
        private InlineDetailExample _inlineDetail;
        private Texture2D _previewTexture;
        private NodinPropertyTree _tree;
        private Vector2 _scroll;

        [MenuItem("Window/Nodin/Attribute Examples")]
        internal static void Open()
        {
            var window = GetWindow<AttributeExamplesWindow>("Nodin Examples");
            window.minSize = new Vector2(370, 320);
            window.Show();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var selected = (Page)EditorGUILayout.EnumPopup(_page, EditorStyles.toolbarPopup);
                var reset = _page != Page.PrefabsAndScene &&
                    GUILayout.Button("Reset page", EditorStyles.toolbarButton, GUILayout.Width(85));
                if (_page != selected || reset)
                {
                    ReleasePage();
                    _page = selected;
                    _scroll = Vector2.zero;
                    GUIUtility.ExitGUI();
                }
            }

            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;
            using var content = new EditorGUILayout.VerticalScope();
            if (_page == Page.PrefabsAndScene)
            {
                DrawContexts();
                return;
            }

            if (_tree == null)
                LoadPage();
            if (_page == Page.References && GUILayout.Button("Inspect inline target directly"))
                Selection.activeObject = _inlineDetail;

            _tree.Draw();
        }

        private void LoadPage()
        {
            _example = _page switch
            {
                Page.Numbers => CreateInstance<NumericExamples>(),
                Page.Layout => CreateInstance<LayoutExamples>(),
                Page.Conditions => CreateInstance<ConditionalExamples>(),
                Page.Presentation => CreateInstance<PresentationExamples>(),
                Page.Validation => CreateInstance<ValidationExamples>(),
                Page.Collections => CreateInstance<CollectionExamples>(),
                Page.Callbacks => CreateInstance<CallbackExamples>(),
                Page.References => CreateInstance<ReferenceExamples>(),
                _ => throw new System.ArgumentOutOfRangeException()
            };
            _example.hideFlags = HideFlags.DontSave;
            if (_example is ReferenceExamples references)
            {
                _inlineDetail = CreateInstance<InlineDetailExample>();
                _inlineDetail.name = "Inline detail example";
                _inlineDetail.hideFlags = HideFlags.DontSave;
                _previewTexture = CreatePreviewTexture();
                references.Setup(_inlineDetail, FindAsset<Material>("Nodin Sample Amber.mat"), _previewTexture);
            }

            _tree = NodinPropertyTree.Create(_example);
        }

        private static Texture2D CreatePreviewTexture()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Signal pattern",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point
            };
            var pixels = new Color32[size * size];
            var teal = new Color32(48, 165, 175, 255);
            var amber = new Color32(242, 167, 82, 255);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = (x / 4 + y / 4) % 2 == 0 ? teal : amber;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void DrawContexts()
        {
            EditorGUILayout.HelpBox(
                "Compare the same component on a prefab asset, on instances, and on an ordinary scene object. " +
                "Assign the Aerial child to Child Target to try the hierarchy filter.", MessageType.Info);
            DrawPrefab("Base prefab", "Signal Station.prefab");
            DrawPrefab("Variant", "Signal Station Variant.prefab");
            DrawPrefab("Nested instance", "Signal Compound.prefab", true);

            if (GUILayout.Button("Add ordinary scene object"))
            {
                var root = new GameObject("Example Signal Station", typeof(ContextExample));
                Undo.RegisterCreatedObjectUndo(root, "Add context example");
                var aerial = new GameObject("Aerial");
                Undo.RegisterCreatedObjectUndo(aerial, "Add aerial");
                Undo.SetTransformParent(aerial.transform, root.transform, "Parent aerial");
                Selection.activeGameObject = root;
            }
        }

        private void DrawPrefab(string label, string file, bool nested = false)
        {
            var prefab = FindAsset<GameObject>(file);
            using var group = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (prefab == null)
            {
                EditorGUILayout.HelpBox($"Import the sample asset {file}.", MessageType.Warning);
                return;
            }

            using var row = new EditorGUILayout.HorizontalScope();
            if (GUILayout.Button("Select asset"))
                Selection.activeObject = prefab;
            if (nested)
            {
                if (GUILayout.Button("Open Prefab Mode"))
                    AssetDatabase.OpenAsset(prefab);
            }
            else if (GUILayout.Button("Add scene instance"))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Add sample prefab instance");
                Selection.activeGameObject = instance;
            }
        }

        private T FindAsset<T>(string file) where T : Object
        {
            var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            var samplePath = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)).Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<T>($"{samplePath}/Demo Assets/{file}");
        }

        private void ReleasePage()
        {
            _tree?.Dispose();
            _tree = null;
            if (_example != null)
                DestroyImmediate(_example);
            if (_inlineDetail != null)
                DestroyImmediate(_inlineDetail);
            if (_previewTexture != null)
                DestroyImmediate(_previewTexture);
            _previewTexture = null;
            _example = null;
            _inlineDetail = null;
        }

        private void OnDisable() => ReleasePage();
    }
}
