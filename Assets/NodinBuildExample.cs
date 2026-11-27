using Nodin;
using UnityEngine;

public sealed class NodinBuildExample : MonoBehaviour
{
    [SerializeField, BoxGroup("Movement"), SuffixLabel("m/s")]
    private float speed = 1;

    public float Speed => speed;

    [Button("Reset speed", ButtonSizes.Medium)]
    private void ResetSpeed() => speed = 1;
}
