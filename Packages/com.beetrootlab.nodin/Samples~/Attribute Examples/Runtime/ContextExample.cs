using UnityEngine;

namespace Nodin.Examples
{
    [HideMonoScript]
    [HideNetworkBehaviourFields]
    [TypeInfoBox("Inspect this component on the supplied prefab, a variant, or a scene object.")]
    public sealed class ContextExample : MonoBehaviour
    {
        [RequiredIn(PrefabKind.PrefabInstance, ErrorMessage = "Set a station label on this prefab instance.")]
        public string instanceLabel = "";

        [ShowIn(PrefabKind.PrefabAsset)]
        public string prefabNote = "Visible on prefab assets.";

        [HideIn(PrefabKind.PrefabAsset)]
        public string sceneNote = "Visible outside prefab assets.";

        [EnableIn(PrefabKind.Variant | PrefabKind.NonPrefabInstance)]
        public int editableInVariant = 5;

        [DisableIn(PrefabKind.PrefabInstance)]
        public float baseFrequency = 12;

        [DisallowModificationsIn(PrefabKind.PrefabInstance)]
        public int fixedChannel = 4;

        [SceneObjectsOnly]
        public GameObject sceneTarget;

        [ChildGameObjectsOnly(IncludeSelf = false)]
        public Transform childTarget;

        [ShowIn(PrefabKind.InstanceInPrefab)]
        public string nestedInstanceNote = "Visible on the nested instance in Prefab Mode.";

        [InfoBox("Legacy networking fields apply only to hosts with the corresponding networking component.")]
        public bool checkInScene = true;
    }
}
