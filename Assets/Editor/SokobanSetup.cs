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
        Require(DemoLevels.All.Length == DemoLevels.Lessons.Length, "Every level has a lesson label");
        foreach (string resource in new[] { "Audio/Puzzling", "Audio/ButtonClick" })
        {
            var clip = Resources.Load<AudioClip>(resource);
            Require(clip != null && clip.length > 0 && clip.samples > 0, "Audio imported: " + resource);
        }
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
        ValidatePersistence();
        ValidateMechanics();
        Debug.Log("SOKOBAN_VALIDATION_OK");
    }

    static void ValidateMechanics()
    {
        var ice = new BoardState(new LevelData {
            rows = new[] { "#########", "#     . #", "#  ##   #", "#@$    ##", "#       #", "#########" },
            terrain = new[] { "         ", "         ", "         ", "   IIII  ", "         ", "         " }
        });
        var start = ice.Capture();
        Require(ice.TryMove(Vector2Int.right) && ice.Boxes[0] == new Vector2Int(6, 3), "Ice slides to wall");
        Require(ice.Player == new Vector2Int(2, 3) && ice.Moves == 1 && ice.Pushes == 1, "Slide is one action; player does not slide");
        ice.Restore(start);
        Require(ice.Boxes.SequenceEqual(start.boxes), "Undo entire slide");
        var floor = new BoardState(new LevelData { rows = new[] { "########", "#@$   .#", "########" }, terrain = new[] { "        ", "   II   ", "        " } });
        Require(floor.TryMove(Vector2Int.right) && floor.Boxes[0].x == 5, "Ice stops on first non-ice cell");
        var obstacle = new BoardState(new LevelData { rows = new[] { "########", "#@$ $..#", "########" }, terrain = new[] { "        ", "   II   ", "        " } });
        Require(obstacle.TryMove(Vector2Int.right) && obstacle.Boxes[0].x == 3 && obstacle.Boxes[1].x == 4, "Slide does not push another box");
        var gate = new BoardState(new LevelData {
            rows = new[] { "#########", "# . #   #", "# $ #   #", "#  $  . #", "# @ #   #", "#########" },
            terrain = new[] { "         ", "  S      ", "         ", "    D    ", "         ", "         " }
        });
        Require(!gate.DoorsOpen && gate.IsBlocked(new Vector2Int(4, 3)), "Door initially closed");
        gate.TryMove(Vector2Int.down);
        var beforePlate = gate.Capture();
        gate.TryMove(Vector2Int.down);
        Require(gate.DoorsOpen && !gate.IsBlocked(new Vector2Int(4, 3)), "Crate holds plate open");
        gate.Restore(beforePlate);
        Require(!gate.DoorsOpen, "Undo closes door again");
        var playerPlate = new BoardState(new LevelData { rows = new[] { "######", "#@   #", "# $ .#", "######" }, terrain = new[] { "      ", " SD   ", "      ", "      " } });
        Require(playerPlate.DoorsOpen && playerPlate.TryMove(Vector2Int.right) && !playerPlate.DoorsOpen, "Player activates and releases plate");
        Require(playerPlate.TryMove(Vector2Int.right), "Player can exit released door");
        Require(!playerPlate.TryMove(Vector2Int.left), "Cannot enter released door");
        var noPlate = new BoardState(DemoLevels.All[4]);
        noPlate.Plates.Clear();
        Require(Solve(noPlate) == null, "Door mechanic required to solve final lesson");
        foreach (var level in DemoLevels.All.Where(l => l.terrain != null))
        {
            var copy = JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(level.Copy()));
            Require(copy.terrain.SequenceEqual(level.terrain), "Terrain copy/JSON roundtrip");
            var resized = copy.Resized(12, 10);
            for (int y = 0; y < copy.rows.Length; y++)
                for (int x = 0; x < copy.rows[y].Length; x++)
                    Require(resized.TerrainAt(x, y) == copy.TerrainAt(x, y), "Resize preserves terrain");
        }
        var invalid = DemoLevels.All[3].Copy(); invalid.terrain[0] = "I";
        Require(invalid.ValidateLayout() != null, "Reject malformed terrain dimensions");
        var wrongOrder = new BoardState(DemoLevels.All[2]);
        wrongOrder.Player = new Vector2Int(3, 2);
        wrongOrder.Boxes = new List<Vector2Int> { new Vector2Int(4, 2), new Vector2Int(5, 2) };
        Require(Solve(wrongOrder) == null, "Filling the near goal then blocking the corridor is a real dead end");
        Debug.Log("SOKOBAN_MECHANICS_OK");
    }

    static void ValidatePersistence()
    {
        var progress = new CampaignProgress();
        progress.Normalize(DemoLevels.All.Length);
        Require(progress.UnlockedCount == 1 && !progress.CanPlay(1), "Initial lock");
        progress.Complete(0, 4);
        Require(progress.CanPlay(1) && !progress.CanPlay(2), "Sequential unlock");
        progress.Complete(0, 6);
        Require(progress.bestMoves[0] == 4, "Do not replace a better score");
        progress.Complete(0, 2);
        Require(progress.bestMoves[0] == 2, "Improve best score");
        for (int i = 1; i < DemoLevels.All.Length; i++) progress.Complete(i, 20);
        Require(progress.CompletedCount == DemoLevels.All.Length && progress.lastPlayed == DemoLevels.All.Length - 1, "Final level progress");

        var original = DemoLevels.All[0].Copy();
        var grown = original.Resized(12, 10);
        Require(grown.Validate() == null && grown.rows[2].Substring(0, 8) == original.rows[2], "Resize preserves contents");
        var cropped = grown.Resized(3, 3);
        Require(cropped.rows.Length == 3 && cropped.rows[0].Length == 3 && cropped.Validate() != null, "Shrink and playable validation");
        Require(LevelData.Empty(16, 14).ValidateLayout() == null && LevelData.Empty(16, 14).Validate() != null, "Incomplete draft can be stored");

        string root = System.IO.Path.Combine(Application.dataPath, "../Temp/SokobanValidation", Guid.NewGuid().ToString("N"));
        var store = new SokobanStorage(root);
        string previousProgress = System.IO.Path.Combine(root, "sokoban-progress.json");
        System.IO.Directory.CreateDirectory(root);
        System.IO.File.WriteAllText(previousProgress, JsonUtility.ToJson(progress));
        Require(store.LoadProgress(DemoLevels.All.Length).CompletedCount == 0 && System.IO.File.Exists(previousProgress), "New campaign does not inherit or delete old scores");
        var library = new LevelLibrary();
        library.Put("one", original);
        library.Put("two", LevelData.Empty(16, 14));
        store.SaveLibrary(library);
        var loaded = store.LoadLibrary();
        Require(loaded.levels.Count == 2 && loaded.levels[1].data.rows.Length == 14, "Multiple-level persistence");
        original.title = "重命名测试";
        loaded.Put("one", original);
        store.SaveLibrary(loaded);
        loaded = store.LoadLibrary();
        Require(loaded.levels.Count == 2 && loaded.levels[0].data.title == "重命名测试", "Rename preserves identity");
        loaded.levels.RemoveAll(l => l.id == "one");
        store.SaveLibrary(loaded);
        Require(store.LoadLibrary().levels.Single().id == "two", "Delete only selected level");
        string exported = System.IO.Path.Combine(root, "Exports/roundtrip.json");
        store.Export(exported, original);
        Require(store.Import(exported).rows.SequenceEqual(original.rows), "File import/export roundtrip");
        foreach (var level in DemoLevels.All.Where(l => l.terrain != null))
        {
            store.Export(exported, level);
            Require(store.Import(exported).terrain.SequenceEqual(level.terrain), "Mechanics file import/export");
        }
        string invalid = System.IO.Path.Combine(root, "invalid.json");
        System.IO.File.WriteAllText(invalid, "{\"rows\":[\"bad\"]}");
        bool rejected = false;
        try { store.Import(invalid); } catch (System.IO.InvalidDataException) { rejected = true; }
        Require(rejected && store.LoadLibrary().levels.Count == 1, "Reject invalid import without changing library");
        store.SaveProgress(progress);
        var restored = store.LoadProgress(DemoLevels.All.Length);
        Require(restored.bestMoves.SequenceEqual(progress.bestMoves) && restored.lastPlayed == progress.lastPlayed, "Progress restart persistence");
        System.IO.File.WriteAllText(store.LibraryPath, "corrupt");
        Require(store.LoadLibrary().levels.Count == 2 && !string.IsNullOrEmpty(store.RecoveryNotice), "Recover backup without deleting original");
        string legacyRoot = System.IO.Path.Combine(root, "Legacy");
        System.IO.Directory.CreateDirectory(legacyRoot);
        string legacyPath = System.IO.Path.Combine(legacyRoot, "sokoban-custom-level.json");
        System.IO.File.WriteAllText(legacyPath, JsonUtility.ToJson(original));
        var legacyStore = new SokobanStorage(legacyRoot);
        Require(legacyStore.LoadLibrary().levels.Count == 1 && legacyStore.LoadLibrary().levels.Count == 1 && System.IO.File.Exists(legacyPath), "Migrate legacy once and preserve original");
        Debug.Log("SOKOBAN_PERSISTENCE_OK");
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
