using UnityEngine;

namespace Nodin.Examples
{
    [TypeInfoBox("Select this target separately to see how its inline-only controls change.")]
    public sealed class InlineDetailExample : ScriptableObject
    {
        [ShowInInlineEditors]
        public string inlineNote = "Sending updates to the host.";

        [HideInInlineEditors]
        public string inOwnInspector = "Visible when inspected directly.";

        [DisableInInlineEditors]
        public int directEditCount = 4;
    }
}
