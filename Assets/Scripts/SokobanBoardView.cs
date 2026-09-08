using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Kuluo.Sokoban
{
    public sealed class SokobanBoardView : MonoBehaviour
    {
        [Header("Tiles and Prefabs")]
        public Tilemap floorMap;
        public Tilemap wallMap;
        public Tilemap iceMap;
        public TileBase floorTile, wallTile, iceTile;
        public GameObject playerPrefab, boxPrefab, goalPrefab, platePrefab, doorPrefab;
        public Transform pieces;
        public Camera boardCamera;
        [Header("Editor Preview")]
        public SokobanLevel previewLevel;
        public float padding = .5f;
        string layoutKey;
        int width, height;
        Transform player;
        readonly List<SokobanPieceView> boxes = new List<SokobanPieceView>();
        readonly Dictionary<Vector2Int, SokobanPieceView> plates = new Dictionary<Vector2Int, SokobanPieceView>();
        readonly Dictionary<Vector2Int, SokobanPieceView> doors = new Dictionary<Vector2Int, SokobanPieceView>();
        static Vector3 Position(Vector2 p) => new Vector3(p.x + .5f, -p.y - .5f, 0);

        public void Show(LevelData data, BoardState state)
        {
            string key = string.Join("\n", data.rows) + "|" + (data.terrain == null ? "" : string.Join("\n", data.terrain));
            if (layoutKey != key) { Build(data); layoutKey = key; }
            if (state == null) return;
            foreach (var p in plates) p.Value.State(state.Player == p.Key || state.Boxes.Contains(p.Key));
            foreach (var p in doors) p.Value.State(state.DoorsOpen, state.Player == p.Key || state.Boxes.Contains(p.Key));
            for (int i = 0; i < boxes.Count; i++) boxes[i].State(state.Goals.Contains(state.Boxes[i]));
        }
        public void Animate(BoardState state, Vector2 fromPlayer, Vector2Int[] fromBoxes, float t)
        {
            if (player != null) player.localPosition = Position(Vector2.Lerp(fromPlayer, state.Player, t));
            for (int i = 0; i < boxes.Count; i++) boxes[i].transform.localPosition = Position(Vector2.Lerp(fromBoxes[i], state.Boxes[i], t));
        }
        public bool TryCell(Vector2 uv, out Vector2Int cell)
        {
            var p = boardCamera.ViewportToWorldPoint(new Vector3(uv.x, uv.y, -boardCamera.transform.localPosition.z));
            p = transform.InverseTransformPoint(p);
            cell = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(-p.y));
            return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        }
        [ContextMenu("Refresh Level Preview")]
        public void Preview()
        {
            if (previewLevel == null) return;
            layoutKey = null; Show(previewLevel.data, null);
        }
        void Build(LevelData data)
        {
            floorMap.ClearAllTiles(); wallMap.ClearAllTiles(); iceMap.ClearAllTiles();
            for (int i = pieces.childCount - 1; i >= 0; i--)
            {
                var child = pieces.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            boxes.Clear(); plates.Clear(); doors.Clear(); player = null;
            width = data.rows[0].Length; height = data.rows.Length;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                char c = data.rows[y][x]; var at = new Vector2Int(x,y); var tile = new Vector3Int(x,-y-1,0);
                floorMap.SetTile(tile, floorTile);
                if (c == '#') wallMap.SetTile(tile, wallTile);
                else
                {
                    char terrain = data.TerrainAt(x,y);
                    if (terrain == 'I') iceMap.SetTile(tile, iceTile);
                    if (terrain == 'S') plates.Add(at, Spawn(platePrefab, at).GetComponent<SokobanPieceView>());
                    if (terrain == 'D') doors.Add(at, Spawn(doorPrefab, at).GetComponent<SokobanPieceView>());
                    if (c == '.' || c == '*' || c == '+') Spawn(goalPrefab, at);
                    if (c == '$' || c == '*') { var b = Spawn(boxPrefab, at).GetComponent<SokobanPieceView>(); boxes.Add(b); b.State(c == '*'); }
                    if (c == '@' || c == '+') player = Spawn(playerPrefab, at).transform;
                }
            }
            boardCamera.transform.localPosition = new Vector3(width / 2f, -height / 2f, -10);
            float aspect = boardCamera.targetTexture != null ? (float)boardCamera.targetTexture.width / boardCamera.targetTexture.height : 1.42f;
            boardCamera.aspect = aspect;
            boardCamera.orthographicSize = Mathf.Max(height / 2f + padding, (width / 2f + padding) / aspect);
        }
        GameObject Spawn(GameObject prefab, Vector2Int at)
        {
            GameObject result;
#if UNITY_EDITOR
            if (!Application.isPlaying) result = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, pieces);
            else
#endif
                result = Instantiate(prefab, pieces);
            result.name = prefab.name + "_" + at.x + "_" + at.y;
            result.transform.localPosition = Position(at);
            return result;
        }
    }
}
