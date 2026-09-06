using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kuluo.Sokoban
{
    [Serializable]
    public class LevelData
    {
        public string title;
        public string hint;
        public string[] rows;

        public LevelData Copy() => new LevelData { title = title, hint = hint, rows = (string[])rows.Clone() };

        public string Validate()
        {
            if (rows == null || rows.Length < 3 || rows.Length > 14) return "地图高度应为 3–14 格。";
            int width = rows[0] == null ? 0 : rows[0].Length;
            if (width < 3 || width > 16) return "地图宽度应为 3–16 格。";
            int players = 0, boxes = 0, goals = 0;
            foreach (string row in rows)
            {
                if (row == null || row.Length != width) return "每行的长度需要一致。";
                foreach (char c in row)
                {
                    if ("# .@$*+".IndexOf(c) < 0) return "地图含有无法识别的元素。";
                    if (c == '@' || c == '+') players++;
                    if (c == '$' || c == '*') boxes++;
                    if (c == '.' || c == '*' || c == '+') goals++;
                }
            }
            if (players != 1) return "请放置且只放置一个玩家。";
            if (boxes == 0 || boxes != goals) return "至少放置一个箱子，且箱子与底座数量必须相等。";
            return null;
        }
    }

    public class BoardState
    {
        public int Width { get; }
        public int Height { get; }
        public Vector2Int Player;
        public List<Vector2Int> Boxes = new List<Vector2Int>();
        public HashSet<Vector2Int> Goals = new HashSet<Vector2Int>();
        public HashSet<Vector2Int> Walls = new HashSet<Vector2Int>();
        public int Moves;
        public int Pushes;

        public BoardState(LevelData data)
        {
            Width = data.rows[0].Length;
            Height = data.rows.Length;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var p = new Vector2Int(x, y);
                    char c = data.rows[y][x];
                    if (c == '#') Walls.Add(p);
                    if (c == '.' || c == '*' || c == '+') Goals.Add(p);
                    if (c == '$' || c == '*') Boxes.Add(p);
                    if (c == '@' || c == '+') Player = p;
                }
        }

        public bool IsWall(Vector2Int p) => p.x < 0 || p.y < 0 || p.x >= Width || p.y >= Height || Walls.Contains(p);
        public int Docked { get { int n = 0; foreach (var p in Boxes) if (Goals.Contains(p)) n++; return n; } }
        public bool Won => Boxes.Count > 0 && Docked == Boxes.Count;

        public bool TryMove(Vector2Int direction)
        {
            if (Math.Abs(direction.x) + Math.Abs(direction.y) != 1) return false;
            var next = Player + direction;
            if (IsWall(next)) return false;
            int box = Boxes.IndexOf(next);
            if (box >= 0)
            {
                var beyond = next + direction;
                if (IsWall(beyond) || Boxes.Contains(beyond)) return false;
                Boxes[box] = beyond;
                Pushes++;
            }
            Player = next;
            Moves++;
            return true;
        }

        public Snapshot Capture() => new Snapshot { player = Player, boxes = Boxes.ToArray(), moves = Moves, pushes = Pushes };
        public void Restore(Snapshot snapshot)
        {
            Player = snapshot.player;
            Boxes = new List<Vector2Int>(snapshot.boxes);
            Moves = snapshot.moves;
            Pushes = snapshot.pushes;
        }

        public struct Snapshot
        {
            public Vector2Int player;
            public Vector2Int[] boxes;
            public int moves, pushes;
        }
    }

    public static class DemoLevels
    {
        public static readonly LevelData[] All =
        {
            new LevelData {
                title = "基础关卡", hint = "把箱子推到目标点上。只能推，不能拉。",
                rows = new[] { "########", "#      #", "# @$ . #", "#      #", "#      #", "########" }
            }
        };
    }
}
