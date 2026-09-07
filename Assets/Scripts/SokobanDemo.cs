using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kuluo.Sokoban
{
    public partial class SokobanDemo : MonoBehaviour
    {
        const float CanvasWidth = 1280, CanvasHeight = 800;
        enum Page { Menu, Home, Play, Library, Editor }
        Page page = Page.Menu;
        readonly Color background = Hex("101C29"), panel = Hex("192A3B"), muted = Hex("96ABBD");
        readonly Color ink = Hex("EAF2F5"), mint = Hex("69DFC0"), gold = Hex("F2BD69");
        readonly Stack<BoardState.Snapshot> history = new Stack<BoardState.Snapshot>();
        readonly List<LevelData> editorUndo = new List<LevelData>();
        BoardState board;
        LevelData current, draft;
        CampaignProgress progress;
        LevelLibrary library = new LevelLibrary();
        SokobanStorage storage;
        int levelIndex, tool, desiredWidth = 8, desiredHeight = 6, libraryPage;
        bool editing, testing, menu, dirty, libraryWritable = true, progressWritable = true, resetFocus;
        bool hasCampaignSession;
        string draftId, selectedId, notice = "", savedDraftJson = "";
        float noticeUntil, moveTime, nextRepeat, moveDuration = .105f;
        Vector2Int heldDirection;
        Vector2 fromPlayer;
        Vector2Int[] fromBoxes;
        Font font;
        Texture2D playerIcon;
        GUIStyle textStyle, fieldStyle;
        AudioSource speaker;
        AudioSource music;
        AudioClip buttonClip;
        bool settingsOpen, effectsPreviewPending;
        float musicVolume, effectsVolume;
        AudioClip stepClip, pushClip, winClip;
        Action confirmAction, discardAction;
        string confirmText;
        bool filePicker, exporting;
        string directoryText, filename = "level.json", fileError = "";
        string[] folders = new string[0], files = new string[0];
        Vector2 fileScroll;
        bool OverlayOpen => settingsOpen || menu || confirmAction != null || discardAction != null || filePicker;

        void Awake()
        {
            Application.targetFrameRate = 60;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 24);
            playerIcon = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    playerIcon.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(31 - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)))));
            playerIcon.Apply();
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            stepClip = Tone("step", 330, .055f);
            pushClip = Tone("push", 180, .09f);
            winClip = Tone("complete", 660, .3f);
            buttonClip = Resources.Load<AudioClip>("Audio/ButtonClick");
            effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Sokoban.EffectsVolume", PlayerPrefs.GetInt("Sokoban.MuteEffects", 0) == 1 ? 0 : .65f));
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Sokoban.MusicVolume", PlayerPrefs.GetInt("Sokoban.MuteMusic", 0) == 1 ? 0 : .4f));
            music = gameObject.AddComponent<AudioSource>();
            music.clip = Resources.Load<AudioClip>("Audio/Puzzling");
            music.loop = true;
            music.volume = musicVolume * .3f;
            if (music.clip != null) music.Play();
            storage = new SokobanStorage(Application.persistentDataPath);
            try
            {
                progress = storage.LoadProgress(DemoLevels.All.Length);
            }
            catch (Exception e)
            {
                progressWritable = false;
                progress = new CampaignProgress();
                progress.Normalize(DemoLevels.All.Length);
                Notify(e.Message);
            }
            try { library = storage.LoadLibrary(); }
            catch (Exception e) { libraryWritable = false; Notify(e.Message + " 保存已暂停，以保护原文件。"); }
            if (!string.IsNullOrEmpty(storage.RecoveryNotice)) Notify(storage.RecoveryNotice);
            current = DemoLevels.All[0].Copy();
            ResetBoard();
            page = Page.Menu;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                heldDirection = Vector2Int.zero;
                if (settingsOpen) { CloseSettings(); return; }
                if (confirmAction != null) { confirmAction = null; return; }
                if (discardAction != null) { discardAction = null; return; }
                if (filePicker) { filePicker = false; return; }
                if (page == Page.Play) menu = !menu;
                else if (page == Page.Editor) LeaveEditor(() => Go(Page.Library));
                else if (page == Page.Menu) ConfirmQuit();
                else Go(Page.Menu);
                return;
            }
            if (OverlayOpen) return;
            if (page == Page.Editor)
            {
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Z)) UndoEdit();
                return;
            }
            if (page != Page.Play) return;
            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Backspace)) Undo();
            if (Input.GetKeyDown(KeyCode.R)) ResetBoard();
            if (board.Won || Time.unscaledTime < moveTime + moveDuration) return;
            Vector2Int direction = Vector2Int.zero;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) direction = Vector2Int.left;
            else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) direction = Vector2Int.right;
            else if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) direction = Vector2Int.down;
            else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) direction = Vector2Int.up;
            if (direction == Vector2Int.zero) { heldDirection = direction; return; }
            if (direction != heldDirection || Time.unscaledTime >= nextRepeat)
            {
                bool first = direction != heldDirection;
                Move(direction);
                nextRepeat = Time.unscaledTime + (first ? .24f : .13f);
                heldDirection = direction;
            }
        }

        void Go(Page target)
        {
            page = target;
            editing = target == Page.Editor;
            menu = false;
            heldDirection = Vector2Int.zero;
            resetFocus = true;
        }

        void LoadLevel(int index)
        {
            if (!progress.CanPlay(index)) { Notify("请先完成前一关。"); return; }
            if (hasCampaignSession && levelIndex == index && !board.Won) { Go(Page.Play); return; }
            hasCampaignSession = true;
            levelIndex = index;
            current = DemoLevels.All[index].Copy();
            testing = false;
            progress.lastPlayed = index;
            SaveProgress();
            Go(Page.Play);
            ResetBoard();
        }

        void StartGame()
        {
            hasCampaignSession = false;
            LoadLevel(0);
        }

        void ConfirmQuit() { Ask("确定退出游戏吗？", QuitGame); }
        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void CloseSettings()
        {
            settingsOpen = false;
            effectsPreviewPending = false;
            SaveAudioSettings();
        }

        void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat("Sokoban.MusicVolume", musicVolume);
            PlayerPrefs.SetFloat("Sokoban.EffectsVolume", effectsVolume);
            PlayerPrefs.Save();
        }

        void OnApplicationQuit() { SaveAudioSettings(); }

        void SaveProgress()
        {
            if (!progressWritable) { Notify("进度文件读取失败，本次进度暂不写入。"); return; }
            try { storage.SaveProgress(progress); }
            catch (Exception e) { Notify("进度保存失败：" + e.Message); }
        }

        void ResetBoard()
        {
            board = new BoardState(current);
            history.Clear();
            SnapAnimation();
            heldDirection = Vector2Int.zero;
        }

        void SnapAnimation()
        {
            fromPlayer = board.Player;
            fromBoxes = board.Boxes.ToArray();
            moveTime = -100;
        }

        void Move(Vector2Int direction)
        {
            if (board.Won || page != Page.Play || OverlayOpen) return;
            var previous = board.Capture();
            if (!board.TryMove(direction)) return;
            history.Push(previous);
            fromPlayer = previous.player;
            fromBoxes = previous.boxes;
            moveDuration = .105f;
            for (int i = 0; i < board.Boxes.Count; i++)
                moveDuration = Mathf.Max(moveDuration, .075f * Vector2Int.Distance(previous.boxes[i], board.Boxes[i]));
            moveTime = Time.unscaledTime;
            Play(board.Won ? winClip : board.Pushes > previous.pushes ? pushClip : stepClip);
            if (board.Won && !testing)
            {
                progress.Complete(levelIndex, board.Moves);
                SaveProgress();
            }
        }

        void Undo()
        {
            if (history.Count == 0) return;
            board.Restore(history.Pop());
            SnapAnimation();
        }
        void Notify(string message) { notice = message; noticeUntil = Time.unscaledTime + 7; }

        void BeginDraft(LevelData data, string id = null)
        {
            draft = data.Copy();
            draftId = id;
            savedDraftJson = id == null ? "" : JsonUtility.ToJson(draft);
            dirty = id == null;
            desiredWidth = draft.rows[0].Length;
            desiredHeight = draft.rows.Length;
            editorUndo.Clear();
            tool = 1;
            testing = false;
            Go(Page.Editor);
        }

        void LeaveEditor(Action next)
        {
            if (dirty) discardAction = next;
            else next();
        }

        void TestDraft()
        {
            string error = draft.Validate();
            if (error != null) { Notify(error); return; }
            hasCampaignSession = false;
            current = draft.Copy();
            testing = true;
            Go(Page.Play);
            ResetBoard();
        }

        bool SaveDraft()
        {
            if (!libraryWritable) { Notify("关卡库读取失败，请先备份并修复原文件。可以导出当前设计。"); return false; }
            try
            {
                string id = draftId ?? Guid.NewGuid().ToString("N");
                var copy = library.Copy();
                copy.Put(id, draft);
                storage.SaveLibrary(copy);
                library = copy;
                draftId = selectedId = id;
                draft.title = draft.title.Trim();
                savedDraftJson = JsonUtility.ToJson(draft);
                dirty = false;
                Notify("关卡已保存。");
                return true;
            }
            catch (Exception e) { Notify("保存失败：" + e.Message); return false; }
        }

        void DeleteLevel(SavedLevel selected)
        {
            Ask("删除「" + selected.data.title + "」？其他关卡不受影响。", () =>
            {
                try
                {
                    var copy = library.Copy();
                    copy.levels.RemoveAll(l => l.id == selected.id);
                    storage.SaveLibrary(copy);
                    library = copy;
                    selectedId = null;
                    Notify("关卡已删除。");
                }
                catch (Exception e) { Notify("删除失败：" + e.Message); }
            });
        }

        void Ask(string message, Action action) { confirmText = message; confirmAction = action; }

        void RememberEdit()
        {
            if (editorUndo.Count >= 100) editorUndo.RemoveAt(0);
            editorUndo.Add(draft.Copy());
        }

        void UndoEdit()
        {
            if (editorUndo.Count == 0) return;
            draft = editorUndo[editorUndo.Count - 1];
            editorUndo.RemoveAt(editorUndo.Count - 1);
            desiredWidth = draft.rows[0].Length;
            desiredHeight = draft.rows.Length;
            MarkDirty();
        }
        void MarkDirty() { dirty = JsonUtility.ToJson(draft) != savedDraftJson; }

        void ResizeDraft()
        {
            if (desiredWidth == draft.rows[0].Length && desiredHeight == draft.rows.Length) return;
            Action resize = () =>
            {
                RememberEdit();
                draft = draft.Resized(desiredWidth, desiredHeight);
                MarkDirty();
                Notify("地图尺寸已调整；重叠区域保留，可撤销。");
            };
            if (desiredWidth < draft.rows[0].Length || desiredHeight < draft.rows.Length)
                Ask("缩小地图会裁掉右侧或下方超出范围的内容。是否继续？", resize);
            else resize();
        }

        void Paint(int x, int y, bool erase)
        {
            char c = draft.rows[y][x];
            bool goal = c == '.' || c == '*' || c == '+';
            char terrain = draft.TerrainAt(x, y);
            char replacement;
            if (erase || tool == 0) { replacement = ' '; terrain = ' '; }
            else if (tool == 1) { replacement = '#'; terrain = ' '; }
            else if (tool >= 5) { replacement = c == '#' ? ' ' : c; terrain = tool == 5 ? 'I' : tool == 6 ? 'S' : 'D'; }
            else replacement = tool == 2 ? c == '$' || c == '*' ? '*' : c == '@' || c == '+' ? '+' : '.' :
                tool == 3 ? goal ? '*' : '$' : goal ? '+' : '@';
            if (replacement == c && terrain == draft.TerrainAt(x, y)) return;
            RememberEdit();
            if (!erase && tool == 4)
                for (int row = 0; row < draft.rows.Length; row++)
                    draft.rows[row] = draft.rows[row].Replace('@', ' ').Replace('+', '.');
            var chars = draft.rows[y].ToCharArray();
            chars[x] = replacement;
            draft.rows[y] = new string(chars);
            draft.SetTerrain(x, y, terrain);
            MarkDirty();
        }

        void OpenFiles(bool export)
        {
            exporting = export;
            filePicker = true;
            filename = "level.json";
            try
            {
                Directory.CreateDirectory(storage.ExportDirectory);
                directoryText = storage.ExportDirectory;
                RefreshFiles();
            }
            catch (Exception e) { fileError = e.Message; }
        }

        void RefreshFiles()
        {
            try
            {
                string path = Path.GetFullPath(directoryText);
                var foundFolders = Directory.GetDirectories(path);
                var foundFiles = Directory.GetFiles(path, "*.json");
                Array.Sort(foundFolders, StringComparer.OrdinalIgnoreCase);
                Array.Sort(foundFiles, StringComparer.OrdinalIgnoreCase);
                directoryText = path;
                folders = foundFolders;
                files = foundFiles;
                fileScroll = Vector2.zero;
                fileError = "";
            }
            catch (Exception e) { folders = files = new string[0]; fileError = "无法打开文件夹：" + e.Message; }
        }

        void ImportFile(string path)
        {
            try
            {
                var imported = storage.Import(path);
                BeginDraft(imported);
                filePicker = false;
                Notify("已导入为新草稿；点击保存后加入我的关卡。");
            }
            catch (Exception e) { fileError = "导入失败：" + e.Message; }
        }

        void ExportFile()
        {
            if (string.IsNullOrWhiteSpace(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            { fileError = "请输入有效的文件名，不要包含文件夹路径。"; return; }
            try
            {
                string name = filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? filename : filename + ".json";
                string path = Path.Combine(Path.GetFullPath(directoryText), name);
                // Only allow exporting through the currently listed, existing directory.
                if (!Directory.Exists(Path.GetDirectoryName(path))) { fileError = "请先打开一个存在的文件夹。"; return; }
                Action write = () =>
                {
                    try { storage.Export(path, draft); filePicker = false; Notify("已导出：" + path); }
                    catch (Exception e) { fileError = "导出失败：" + e.Message; }
                };
                if (File.Exists(path)) Ask("文件已存在，是否覆盖 " + name + "？", write);
                else write();
            }
            catch (Exception e) { fileError = e.Message; }
        }

        void DrawBoard(Rect area)
        {
            LevelData data = editing ? draft : current;
            int width = data.rows[0].Length, height = data.rows.Length;
            float cell = Mathf.Min(area.width / width, area.height / height, 74);
            float left = area.x + (area.width - width * cell) / 2;
            float top = area.y + (area.height - height * cell) / 2;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    char c = data.rows[y][x];
                    var tile = new Rect(left + x * cell, top + y * cell, cell, cell);
                    Fill(Inset(tile, 2), (x + y) % 2 == 0 ? Hex("233A4B") : Hex("263E50"));
                    if (c == '#')
                    {
                        Fill(new Rect(tile.x + 3, tile.y + 8, cell - 6, cell - 10), Hex("102130"));
                        Fill(new Rect(tile.x + 3, tile.y + 3, cell - 6, cell - 12), Hex("496174"));
                        Fill(new Rect(tile.x + 7, tile.y + 6, cell - 14, 3), Hex("658092"));
                    }
                    else DrawTerrain(tile, data.TerrainAt(x, y), !editing && board.DoorsOpen, !editing && (board.Player == new Vector2Int(x, y) || board.Boxes.Contains(new Vector2Int(x, y))));
                    if (c == '.' || c == '*' || c == '+')
                    {
                        Fill(Inset(tile, cell * .19f), Hex("365F5D"));
                        Fill(Inset(tile, cell * .27f), mint);
                        Fill(Inset(tile, cell * .32f), Hex("233A4B"));
                        Fill(Inset(tile, cell * .43f), mint);
                    }
                    if (editing)
                    {
                        if (c == '$' || c == '*') DrawBox(tile, c == '*');
                        if (c == '@' || c == '+') DrawPlayer(tile);
                        if (!OverlayOpen && tile.Contains(Event.current.mousePosition))
                        {
                            if ((Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag) && Event.current.button <= 1)
                            {
                                Paint(x, y, Event.current.button == 1);
                                Event.current.Use();
                            }
                        }
                    }
                }
            if (!editing)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - moveTime) / moveDuration));
                for (int i = 0; i < board.Boxes.Count; i++)
                {
                    Vector2 p = Vector2.Lerp(fromBoxes[i], board.Boxes[i], t);
                    DrawBox(new Rect(left + p.x * cell, top + p.y * cell, cell, cell), board.Goals.Contains(board.Boxes[i]));
                }
                Vector2 robot = Vector2.Lerp(fromPlayer, board.Player, t);
                DrawPlayer(new Rect(left + robot.x * cell, top + robot.y * cell, cell, cell));
            }
        }

        void DrawTerrain(Rect tile, char kind, bool open, bool occupied = false)
        {
            float c = tile.width;
            if (kind == 'I')
            {
                Fill(Inset(tile, 3), Hex("376D89"));
                Fill(new Rect(tile.x + c * .16f, tile.y + c * .22f, c * .52f, c * .055f), Hex("9ADCF4"));
                Fill(new Rect(tile.x + c * .35f, tile.y + c * .67f, c * .47f, c * .045f), Hex("71B8D5"));
            }
            if (kind == 'S')
            {
                Fill(Inset(tile, c * .09f), Hex("62507C"));
                Fill(Inset(tile, c * .15f), open ? Hex("D4ACF2") : Hex("A680C4"));
                Fill(Inset(tile, c * .22f), Hex("413C59"));
            }
            if (kind == 'D')
            {
                Color color = Hex("C7A0E8");
                Fill(new Rect(tile.x + c * .08f, tile.y + c * .08f, c * .1f, c * .84f), color);
                Fill(new Rect(tile.x + c * .82f, tile.y + c * .08f, c * .1f, c * .84f), color);
                if (!open && !occupied)
                    for (int i = 0; i < 3; i++) Fill(new Rect(tile.x + c * .2f, tile.y + c * (.2f + i * .25f), c * .6f, c * .10f), color);
            }
        }

        void DrawBox(Rect tile, bool docked)
        {
            float c = tile.width;
            Fill(new Rect(tile.x + c * .14f, tile.y + c * .23f, c * .72f, c * .66f), Hex("142633"));
            var box = new Rect(tile.x + c * .14f, tile.y + c * .12f, c * .72f, c * .67f);
            Fill(box, docked ? Hex("438F7C") : Hex("B87938"));
            Fill(Inset(box, c * .07f), docked ? mint : gold);
            Fill(new Rect(tile.x + c * .44f, tile.y + c * .18f, c * .12f, c * .55f), docked ? Hex("438F7C") : Hex("B87938"));
            Fill(new Rect(tile.x + c * .22f, tile.y + c * .42f, c * .56f, c * .09f), docked ? Hex("438F7C") : Hex("B87938"));
            Fill(new Rect(tile.x + c * .42f, tile.y + c * .38f, c * .16f, c * .17f), Hex("F7E5B9"));
        }

        void DrawPlayer(Rect tile)
        {
            float c = tile.width;
            Color previous = GUI.color;
            GUI.color = Hex("83CBF1");
            GUI.DrawTexture(Inset(tile, c * .19f), playerIcon);
            GUI.color = previous;
            Text(tile, "P", Mathf.RoundToInt(c * .32f), background, true, TextAnchor.MiddleCenter);
        }

        void Text(Rect rect, string value, int size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            textStyle.fontSize = size;
            textStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            // Unity's default label hover color must not change passive text.
            foreach (var state in new[] { textStyle.normal, textStyle.hover, textStyle.active, textStyle.focused,
                textStyle.onNormal, textStyle.onHover, textStyle.onActive, textStyle.onFocused })
            { state.textColor = color; state.background = null; }
            textStyle.alignment = anchor;
            GUI.Label(rect, value, textStyle);
        }

        static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
        static Rect Inset(Rect r, float amount) => new Rect(r.x + amount, r.y + amount, r.width - amount * 2, r.height - amount * 2);
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color c); return c; }
        void Play(AudioClip clip) { if (clip != null && effectsVolume > 0) speaker.PlayOneShot(clip, effectsVolume * .25f); }
        static AudioClip Tone(string name, float frequency, float duration)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * Mathf.Sin(Mathf.PI * i / samples.Length) * .4f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void OnDestroy()
        {
            if (font != null) Destroy(font);
            if (playerIcon != null) Destroy(playerIcon);
            if (stepClip != null) Destroy(stepClip);
            if (pushClip != null) Destroy(pushClip);
            if (winClip != null) Destroy(winClip);
        }
    }
}

