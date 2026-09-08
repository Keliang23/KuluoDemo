using UnityEngine;

namespace Kuluo.Sokoban
{
    [CreateAssetMenu(menuName = "Sokoban/Level", fileName = "Level")]
    public sealed class SokobanLevel : ScriptableObject
    {
        [Tooltip("Campaign order. Existing saves use this order; keep it stable.")]
        public int order;
        public string lesson;
        public LevelData data;
    }
}
