using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kuluo.Sokoban
{
    [Serializable]
    public class CampaignProgress
    {
        public int version = 1;
        public int lastPlayed;
        public int[] bestMoves = new int[0];

        public void Normalize(int count)
        {
            var old = bestMoves ?? new int[0];
            bestMoves = new int[count];
            for (int i = 0; i < Math.Min(count, old.Length); i++) bestMoves[i] = Math.Max(0, old[i]);
            lastPlayed = Mathf.Clamp(lastPlayed, 0, UnlockedCount - 1);
        }
        public int UnlockedCount
        {
            get
            {
                int count = 1;
                while (count < bestMoves.Length && bestMoves[count - 1] > 0) count++;
                return count;
            }
        }
        public int CompletedCount { get { int n = 0; foreach (int v in bestMoves) if (v > 0) n++; return n; } }
        public bool CanPlay(int index) => index >= 0 && index < bestMoves.Length && index < UnlockedCount;
        public void Complete(int index, int moves)
        {
            if (!CanPlay(index) || moves <= 0) throw new ArgumentException("无效的通关记录。");
            bestMoves[index] = bestMoves[index] == 0 ? moves : Math.Min(bestMoves[index], moves);
            lastPlayed = Math.Min(index + 1, bestMoves.Length - 1);
        }
    }

    [Serializable]
    public class SavedLevel
    {
        public string id;
        public LevelData data;
        public SavedLevel Copy() => new SavedLevel { id = id, data = data.Copy() };
    }

    [Serializable]
    public class LevelLibrary
    {
        public int version = 1;
        public List<SavedLevel> levels = new List<SavedLevel>();
        public LevelLibrary Copy()
        {
            var result = new LevelLibrary();
            foreach (var level in levels) result.levels.Add(level.Copy());
            return result;
        }
        public void Put(string id, LevelData data)
        {
            if (data == null || data.ValidateLayout() != null) throw new ArgumentException("地图格式无效。");
            if (string.IsNullOrWhiteSpace(data.title) || data.title.Trim().Length > 40) throw new ArgumentException("关卡名需为 1–40 个字符。");
            var saved = levels.Find(l => l.id == id);
            if (saved == null)
            {
                if (levels.Count >= 200) throw new InvalidOperationException("最多保存 200 个关卡。");
                saved = new SavedLevel { id = id };
                levels.Add(saved);
            }
            saved.data = data.Copy();
            saved.data.title = saved.data.title.Trim();
        }
    }

    public class SokobanStorage
    {
        readonly string root;
        public string LibraryPath => Path.Combine(root, "sokoban-library.json");
        public string ProgressPath => Path.Combine(root, "sokoban-progress-learning-v3.json");
        public string ExportDirectory => Path.Combine(root, "Exports");
        public string RecoveryNotice { get; private set; }
        public SokobanStorage(string root) { this.root = root; }

        public CampaignProgress LoadProgress(int count)
        {
            var result = Read(ProgressPath, json =>
            {
                var value = JsonUtility.FromJson<CampaignProgress>(json);
                if (value == null || value.version != 1 || value.bestMoves == null || value.bestMoves.Length > 1000)
                    throw new InvalidDataException("进度文件格式无效。");
                return value;
            }) ?? new CampaignProgress();
            result.Normalize(count);
            return result;
        }

        public LevelLibrary LoadLibrary()
        {
            var library = Read(LibraryPath, ParseLibrary);
            if (library != null) return library;
            library = new LevelLibrary();
            string legacy = Path.Combine(root, "sokoban-custom-level.json");
            if (File.Exists(legacy))
            {
                var data = Import(legacy);
                library.Put(Guid.NewGuid().ToString("N"), data);
                SaveLibrary(library);
            }
            return library;
        }

        static LevelLibrary ParseLibrary(string json)
        {
            var value = JsonUtility.FromJson<LevelLibrary>(json);
            if (value == null || value.version != 1 || value.levels == null || value.levels.Count > 200)
                throw new InvalidDataException("关卡库格式无效。");
            var ids = new HashSet<string>();
            foreach (var entry in value.levels)
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id) || entry.data == null ||
                    entry.data.ValidateLayout() != null || string.IsNullOrWhiteSpace(entry.data.title) || entry.data.title.Length > 40)
                    throw new InvalidDataException("关卡库含有无效关卡。");
            return value;
        }

        T Read<T>(string path, Func<string, T> parse) where T : class
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
            try { return parse(ReadBounded(path)); }
            catch (Exception first)
            {
                if (!File.Exists(path + ".bak")) throw new IOException("无法读取 " + Path.GetFileName(path) + "；原文件已保留。", first);
                var result = parse(ReadBounded(path + ".bak"));
                RecoveryNotice = "存档读取异常，已加载上一份备份。";
                return result;
            }
        }

        static string ReadBounded(string path)
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("文件过大（上限 4 MB）。");
            return File.ReadAllText(path);
        }

        public LevelData Import(string path)
        {
            var data = JsonUtility.FromJson<LevelData>(ReadBounded(path));
            if (data == null || data.ValidateLayout() != null) throw new InvalidDataException("请选择有效的关卡 JSON 文件。");
            data.title = string.IsNullOrWhiteSpace(data.title) ? "导入的关卡" : data.title.Trim();
            if (data.title.Length > 40) data.title = data.title.Substring(0, 40);
            data.hint = data.hint ?? "";
            return data;
        }

        public void SaveProgress(CampaignProgress data) => AtomicWrite(ProgressPath, JsonUtility.ToJson(data, true));
        public void SaveLibrary(LevelLibrary data)
        {
            string json = JsonUtility.ToJson(data, true);
            ParseLibrary(json);
            AtomicWrite(LibraryPath, json);
        }
        public void Export(string path, LevelData data)
        {
            if (data == null || data.ValidateLayout() != null) throw new InvalidDataException("地图格式无效。");
            AtomicWrite(path, JsonUtility.ToJson(data, true));
        }
        static void AtomicWrite(string path, string contents)
        {
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, contents, new System.Text.UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
