using UnityEngine;
namespace Kuluo.Sokoban
{
    public sealed class SokobanPieceView : MonoBehaviour
    {
        public SpriteRenderer body;
        public GameObject closedBars;
        public Color activeColor = new Color(.412f,.875f,.753f);
        Color original;
        bool initialized;
        public void State(bool active, bool occupied = false)
        {
            if (!initialized) { original = body != null ? body.color : Color.white; initialized = true; }
            if (closedBars != null) closedBars.SetActive(!active && !occupied);
            else if (body != null) body.color = active ? activeColor : original;
        }
    }
}
