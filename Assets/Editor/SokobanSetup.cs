using System;
using System.Collections.Generic;
using System.Linq;
using Kuluo.Sokoban;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SokobanSetup
{
    [MenuItem("Tools/Sokoban/Create Demo Scene")]
    public static void CreateScene()
    {
        const string path = "Assets/Scenes/SokobanDemo.unity";
        if (!System.IO.File.Exists(path))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.063f, .11f, .16f);
            camera.gameObject.AddComponent<AudioListener>();
            new GameObject("Sokoban Demo").AddComponent<SokobanDemo>();
            EditorSceneManager.SaveScene(scene, path);
        }
        else EditorSceneManager.OpenScene(path);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        PlayerSettings.productName = "Sokoban Prototype";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        AssetDatabase.SaveAssets();
        ValidateDemo();
        Debug.Log("SOKOBAN_SETUP_OK");
    }

    [MenuItem("Tools/Sokoban/Validate Demo")]
    public static void ValidateDemo()
    {
        foreach (var level in DemoLevels.All)
        {
            Require(level.Validate() == null, "Invalid level: " + level.title);
            var restored = JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(level));
            Require(restored.Validate() == null && restored.rows.SequenceEqual(level.rows), "JSON roundtrip");
            var board = new BoardState(level);
            var start = board.Capture();
            var solution = Solve(board);
            Require(solution != null, "Unsolvable level: " + level.title);
            board.Restore(start);
            foreach (char c in solution) Require(board.TryMove(Direction(c)), "Solution replay");
            Require(board.Won, "Victory not detected");
            board.Restore(start);
            Require(board.Moves == 0 && board.Pushes == 0 && !board.Won, "Undo/reset state");
            Debug.Log("SOKOBAN_SOLVED " + level.title + " " + solution);
        }
        var blocked = new BoardState(new LevelData { rows = new[] { "######", "#@$$.#", "######" } });
        Require(!blocked.TryMove(Vector2Int.right) && blocked.Moves == 0, "Must not push two boxes");
        Require(!blocked.TryMove(Vector2Int.up) && blocked.Moves == 0, "Must not walk into wall");
        Require(!blocked.TryMove(new Vector2Int(1, 1)), "Must reject diagonal move");
        var push = new BoardState(DemoLevels.All[0]);
        var before = push.Capture();
        Require(push.TryMove(Vector2Int.right) && push.Pushes == 1, "Push counter");
        push.Restore(before);
        Require(push.Boxes.SequenceEqual(before.boxes) && push.Player == before.player && push.Pushes == 0, "Push undo");
        var edge = new BoardState(new LevelData { rows = new[] { "@  ", " $.", "   " } });
        Require(!edge.TryMove(Vector2Int.left) && !edge.TryMove(Vector2Int.down), "Map boundary");
        Require(new LevelData { rows = new[] { "###", "#@#", "###" } }.Validate() != null, "No-box validation");
        Require(new LevelData { rows = new[] { "###", "@@$", " . " } }.Validate() != null, "Spawn validation");
        Debug.Log("SOKOBAN_VALIDATION_OK");
    }

    static Vector2Int Direction(char c) => c == 'R' ? Vector2Int.right : c == 'L' ? Vector2Int.left : c == 'U' ? Vector2Int.down : Vector2Int.up;
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static string Key(BoardState board) => board.Player.x + "," + board.Player.y + ":" + string.Join(";", board.Boxes.Select(p => p.y * board.Width + p.x).OrderBy(v => v));

    static string Solve(BoardState board)
    {
        var queue = new Queue<Tuple<BoardState.Snapshot, string>>();
        var visited = new HashSet<string>();
        queue.Enqueue(Tuple.Create(board.Capture(), ""));
        visited.Add(Key(board));
        while (queue.Count > 0 && visited.Count < 200000)
        {
            var item = queue.Dequeue();
            foreach (char direction in "RULD")
            {
                board.Restore(item.Item1);
                if (!board.TryMove(Direction(direction))) continue;
                string moves = item.Item2 + direction;
                if (board.Won) return moves;
                if (visited.Add(Key(board))) queue.Enqueue(Tuple.Create(board.Capture(), moves));
            }
        }
        return null;
    }
}
