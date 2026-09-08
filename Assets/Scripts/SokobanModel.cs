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
        // Optional terrain layer: I = ice, S = pressure plate, D = door.
        // Missing layer keeps older custom maps compatible.
        public string[] terrain;

        public LevelData Copy() => new LevelData { title = title, hint = hint, rows = (string[])rows.Clone(), terrain = terrain == null ? null : (string[])terrain.Clone() };
        public char TerrainAt(int x, int y) => terrain == null || terrain.Length == 0 ? ' ' : terrain[y][x];
        public void SetTerrain(int x, int y, char value)
        {
            if (terrain == null || terrain.Length == 0)
            {
                terrain = new string[rows.Length];
                for (int i = 0; i < rows.Length; i++) terrain[i] = new string(' ', rows[0].Length);
            }
            var line = terrain[y].ToCharArray(); line[x] = value; terrain[y] = new string(line);
        }

        public string ValidateLayout()
        {
            if (rows == null || rows.Length < 3 || rows.Length > 14) return "地图高度应为 3–14 格。";
            int width = rows[0] == null ? 0 : rows[0].Length;
            if (width < 3 || width > 16) return "地图宽度应为 3–16 格。";
            foreach (string row in rows)
            {
                if (row == null || row.Length != width) return "每行的长度需要一致。";
                foreach (char c in row)
                    if ("# .@$*+".IndexOf(c) < 0) return "地图含有无法识别的元素。";
            }
            if (terrain != null && terrain.Length > 0)
            {
                if (terrain.Length != rows.Length) return "地形层的高度需要与地图一致。";
                for (int y = 0; y < rows.Length; y++)
                {
                    if (terrain[y] == null || terrain[y].Length != width) return "地形层的宽度需要与地图一致。";
                    for (int x = 0; x < width; x++)
                        if (" ISD".IndexOf(terrain[y][x]) < 0 || rows[y][x] == '#' && terrain[y][x] != ' ')
                            return "特殊地形不能与墙壁重叠。";
                }
            }
            return null;
        }

        public string Validate()
        {
            string layoutError = ValidateLayout();
            if (layoutError != null) return layoutError;
            int players = 0, boxes = 0, goals = 0;
            foreach (string row in rows)
                foreach (char c in row)
                {
                    if (c == '@' || c == '+') players++;
                    if (c == '$' || c == '*') boxes++;
                    if (c == '.' || c == '*' || c == '+') goals++;
                }
            if (players != 1) return "请放置且只放置一个玩家。";
            if (boxes == 0 || boxes != goals) return "至少放置一个箱子，且箱子与目标点数量必须相等。";
            bool plate = false, door = false;
            if (terrain != null) foreach (string row in terrain) { plate |= row.Contains("S"); door |= row.Contains("D"); }
            if (plate != door) return "压力板与门需要一起放置。";
            return null;
        }

        public static LevelData Empty(int width, int height)
        {
            width = Mathf.Clamp(width, 3, 16);
            height = Mathf.Clamp(height, 3, 14);
            var result = new LevelData { title = "未命名关卡", hint = "", rows = new string[height] };
            for (int y = 0; y < height; y++)
                result.rows[y] = y == 0 || y == height - 1 ? new string('#', width) : "#" + new string(' ', width - 2) + "#";
            return result;
        }

        // Keep the overlapping rectangle intact, including goals under entities.
        public LevelData Resized(int width, int height)
        {
            var result = Empty(width, height);
            result.title = title;
            result.hint = hint;
            for (int y = 0; y < Mathf.Min(rows.Length, result.rows.Length); y++)
            {
                var chars = result.rows[y].ToCharArray();
                for (int x = 0; x < Mathf.Min(rows[y].Length, chars.Length); x++) chars[x] = rows[y][x];
                result.rows[y] = new string(chars);
                for (int x = 0; x < Mathf.Min(rows[y].Length, chars.Length); x++)
                    if (TerrainAt(x, y) != ' ') result.SetTerrain(x, y, TerrainAt(x, y));
            }
            return result;
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
        public HashSet<Vector2Int> Ice = new HashSet<Vector2Int>();
        public HashSet<Vector2Int> Plates = new HashSet<Vector2Int>();
        public HashSet<Vector2Int> Doors = new HashSet<Vector2Int>();
        public bool DoorsOpen => Plates.Contains(Player) || Boxes.Exists(p => Plates.Contains(p));
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
                    char terrain = data.TerrainAt(x, y);
                    if (terrain == 'I') Ice.Add(p);
                    if (terrain == 'S') Plates.Add(p);
                    if (terrain == 'D') Doors.Add(p);
                }
        }

        public bool IsWall(Vector2Int p) => p.x < 0 || p.y < 0 || p.x >= Width || p.y >= Height || Walls.Contains(p);
        // A released door blocks entry; an occupant can always leave it.
        public bool IsBlocked(Vector2Int p) => IsWall(p) || Doors.Contains(p) && !DoorsOpen && p != Player && !Boxes.Contains(p);
        public int Docked { get { int n = 0; foreach (var p in Boxes) if (Goals.Contains(p)) n++; return n; } }
        public bool Won => Boxes.Count > 0 && Docked == Boxes.Count;
        public bool HasCorneredBox => Boxes.Exists(p => !Goals.Contains(p) &&
            (IsWall(p + Vector2Int.left) || IsWall(p + Vector2Int.right)) &&
            (IsWall(p + Vector2Int.up) || IsWall(p + Vector2Int.down)));

        public bool TryMove(Vector2Int direction)
        {
            if (Math.Abs(direction.x) + Math.Abs(direction.y) != 1) return false;
            var next = Player + direction;
            if (IsBlocked(next)) return false;
            int box = Boxes.IndexOf(next);
            if (box >= 0)
            {
                var beyond = next + direction;
                if (IsBlocked(beyond) || Boxes.Contains(beyond)) return false;
                Boxes[box] = beyond;
                Player = next;
                while (Ice.Contains(Boxes[box]))
                {
                    var slide = Boxes[box] + direction;
                    if (IsBlocked(slide) || Boxes.Contains(slide)) break;
                    Boxes[box] = slide;
                }
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
        static LevelData[] loaded;
        static string[] lessons;
        public static LevelData[] All { get { Load(); return loaded; } }
        public static string[] Lessons { get { Load(); return lessons; } }
        public static void Reload() { loaded = null; lessons = null; }
        static void Load()
        {
            if (loaded != null) return;
            var assets = Resources.LoadAll<SokobanLevel>("Levels");
            Array.Sort(assets, (a, b) => a.order.CompareTo(b.order));
            if (assets.Length == 0) { loaded = Seeds; lessons = SeedLessons; return; }
            loaded = new LevelData[assets.Length]; lessons = new string[assets.Length];
            for (int i = 0; i < assets.Length; i++) { loaded[i] = assets[i].data.Copy(); lessons[i] = assets[i].lesson; }
        }
        public static readonly string[] SeedLessons = { "基础移动", "多目标", "推动顺序", "冰面滑行", "压力板开门", "综合  借箱停靠", "综合  分路协作" };
        public static readonly LevelData[] Seeds =
        {
            new LevelData {
                title = "第一步", hint = "把箱子推到绿色目标点。只能推，不能拉。",
                rows = new[] { "########", "#      #", "# @$ . #", "#      #", "#      #", "########" }
            },
            new LevelData {
                title = "各就各位", hint = "两个箱子都要到位。观察目标的位置，绕到箱子的另一侧再推动。",
                rows = new[] { "########", "# .    #", "#    $.#", "# $ #  #", "# @    #", "#      #", "########" }
            },
            new LevelData {
                title = "先里后外", hint = "窄道里有两个目标。先想好哪一个先填满，别让箱子挡住后面的路。",
                rows = new[] { "########", "########", "#    ..#", "# $$####", "# @ ####", "########" }
            },
            new LevelData {
                title = "滑到哪里？", hint = "箱子在冰面上会一直滑，离开冰面或遇到障碍才停下。玩家正常行走。",
                rows = new[] { "#########", "#     . #", "# $    ##", "#  ## . #", "# $     #", "#@      #", "#########" },
                terrain = new[] { "         ", "         ", "   IIII  ", "         ", "   III   ", "         ", "         " }
            },
            new LevelData {
                title = "借力开门", hint = "玩家或箱子压住紫色压力板时，门会打开。松开后关闭，门上的物体仍可离开。",
                rows = new[] { "#########", "# . #####", "# $ #####", "#  $  ..#", "# $ #####", "#@  #####", "#########" },
                terrain = new[] { "         ", "  S      ", "         ", "    D    ", "         ", "         ", "         " }
            },
            new LevelData {
                title = "借箱停靠", hint = "先压住压力板打开门，再把箱子送入冰道。已经到位的箱子，也能成为下一个箱子的挡板。",
                rows = new[] { "#########", "# . #####", "# $ #####", "#  $  ..#", "# $ #####", "#@  #####", "#########" },
                terrain = new[] { "         ", "  S      ", "         ", "    DII  ", "         ", "         ", "         " }
            },
            new LevelData {
                title = "分路协作", hint = "一块压力板控制两扇门。为两个目标安排各自的冰面路线，开门的箱子要留在原位。",
                rows = new[] { "##########", "# .  #   #", "# $     .#", "#  $ #   #", "#      . #", "# $  #   #", "# @  #####", "##########" },
                terrain = new[] { "          ", "  S       ", "     DII  ", "          ", "     DI   ", "          ", "          ", "          " }
            }
        };
    }
}
