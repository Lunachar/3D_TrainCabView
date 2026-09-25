using UnityEngine;

namespace SortingStation
{
    /// <summary>Sizes a station name board to its text once the text mesh exists, then removes itself.</summary>
    public sealed class SignBoardFitter : MonoBehaviour
    {
        private TextMesh text;

        public void Configure(TextMesh label)
        {
            text = label;
            CabWorld3DPrototypeFactory.FitSignBoard(transform, text);
        }

        private void LateUpdate()
        {
            if (text == null)
            {
                Destroy(this);
                return;
            }
            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.localBounds.size.x < 0.01f) return;
            CabWorld3DPrototypeFactory.FitSignBoard(transform, text);
            Destroy(this);
        }
    }
}
