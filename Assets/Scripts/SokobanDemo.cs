using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kuluo.Sokoban
{
    // A self-contained first playable. The board rules are independent of its temporary presentation.
    public class SokobanDemo : MonoBehaviour
    {
        const float CanvasWidth = 1280, CanvasHeight = 800;
        readonly Color background = Hex("101C29"), panel = Hex("192A3B"), muted = Hex("96ABBD");
        readonly Color ink = Hex("EAF2F5"), mint = Hex("69DFC0"), gold = Hex("F2BD69");
        readonly Stack<BoardState.Snapshot> history = new Stack<BoardState.Snapshot>();
        BoardState board;
        LevelData current, draft;
        int levelIndex, tool;
        bool editing, testing, menu, mutedAudio;
        string notice = "";
        float noticeUntil, moveTime, nextRepeat;
        Vector2Int heldDirection;
        Vector2 fromPlayer;
        Vector2Int[] fromBoxes;
        Font font;
        Texture2D playerIcon;
        GUIStyle textStyle;
        AudioSource speaker;
        AudioClip stepClip, pushClip, winClip;
        string SavePath => Path.Combine(Application.persistentDataPath, "sokoban-custom-level.json");

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
            LoadLevel(0);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { menu = !menu; heldDirection = Vector2Int.zero; }
            if (menu || editing) return;
            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Backspace)) Undo();
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (board.Won) return;
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

        void LoadLevel(int index)
        {
            levelIndex = index;
            current = DemoLevels.All[index].Copy();
            editing = testing = menu = false;
            ResetBoard();
        }

        void ResetBoard()
        {
            board = new BoardState(current);
            history.Clear();
            SnapAnimation();
            heldDirection = Vector2Int.zero;
            notice = "";
        }

        void SnapAnimation()
        {
            fromPlayer = board.Player;
            fromBoxes = board.Boxes.ToArray();
            moveTime = -100;
        }

        void Move(Vector2Int direction)
        {
            if (board.Won || editing || menu) return;
            var previous = board.Capture();
            if (!board.TryMove(direction)) return;
            history.Push(previous);
            fromPlayer = previous.player;
            fromBoxes = previous.boxes;
            moveTime = Time.unscaledTime;
            Play(board.Won ? winClip : board.Pushes > previous.pushes ? pushClip : stepClip);
            if (board.Won && !testing)
            {
                int old = PlayerPrefs.GetInt("Sokoban.Best." + levelIndex, int.MaxValue);
                PlayerPrefs.SetInt("Sokoban.Best." + levelIndex, Math.Min(old, board.Moves));
                PlayerPrefs.Save();
            }
        }

        void Undo()
        {
            if (history.Count == 0) return;
            board.Restore(history.Pop());
            SnapAnimation();
        }

        void Restart() { ResetBoard(); }
        void Notify(string message) { notice = message; noticeUntil = Time.unscaledTime + 5; }

        void OpenEditor()
        {
            draft = (testing && draft != null ? draft : current).Copy();
            editing = true;
            testing = menu = false;
            tool = 1;
            notice = "";
        }

        void TestDraft()
        {
            string error = draft.Validate();
            if (error != null) { Notify(error); return; }
            current = draft.Copy();
            current.title = "自定义关卡";
            current.hint = "试玩你的设计。随时返回编辑，继续调整。";
            editing = false;
            testing = true;
            ResetBoard();
        }

        void SaveDraft()
        {
            string error = draft.Validate();
            if (error != null) { Notify(error); return; }
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(SavePath, JsonUtility.ToJson(draft, true));
                Notify("已保存到本机。关闭游戏后也可以读取。");
            }
            catch (Exception e) { Notify("保存失败：" + e.Message); }
        }

        void LoadDraft()
        {
            try
            {
                if (!File.Exists(SavePath)) { Notify("还没有保存的关卡。先画一关吧。"); return; }
                var loaded = JsonUtility.FromJson<LevelData>(File.ReadAllText(SavePath));
                string error = loaded == null ? "关卡文件无效。" : loaded.Validate();
                if (error != null) { Notify(error); return; }
                draft = loaded;
                Notify("已读取本机保存的关卡。");
            }
            catch (Exception e) { Notify("读取失败：" + e.Message); }
        }

        void Paint(int x, int y, bool erase)
        {
            char c = draft.rows[y][x];
            bool goal = c == '.' || c == '*' || c == '+';
            char replacement;
            if (erase || tool == 0) replacement = ' ';
            else if (tool == 1) replacement = '#';
            else if (tool == 2) replacement = c == '$' || c == '*' ? '*' : c == '@' || c == '+' ? '+' : '.';
            else if (tool == 3) replacement = goal ? '*' : '$';
            else
            {
                for (int row = 0; row < draft.rows.Length; row++)
                    draft.rows[row] = draft.rows[row].Replace('@', ' ').Replace('+', '.');
                replacement = goal ? '+' : '@';
            }
            var chars = draft.rows[y].ToCharArray();
            chars[x] = replacement;
            draft.rows[y] = new string(chars);
        }

        void OnGUI()
        {
            if (board == null) return;
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label) { font = font, wordWrap = true, richText = false, padding = new RectOffset() };
            var oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / CanvasWidth, Screen.height / CanvasHeight);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - CanvasWidth * scale) / 2, (Screen.height - CanvasHeight * scale) / 2), Quaternion.identity, new Vector3(scale, scale, 1));
            Fill(new Rect(0, 0, 1280, 800), background);
            Fill(new Rect(32, 35, 5, 42), mint);
            Text(new Rect(52, 30, 460, 35), "推箱子原型", 29, ink, true);
            Text(new Rect(53, 68, 470, 24), "SOKOBAN  /  BASIC PROTOTYPE", 12, muted);
            if (Button(new Rect(1040, 38, 92, 38), mutedAudio ? "声音：关" : "声音：开", false, !menu)) mutedAudio = !mutedAudio;
            if (Button(new Rect(1144, 38, 100, 38), "菜单  Esc", false, !menu)) menu = true;

            if (!editing && !testing)
            {
                for (int i = 0; i < DemoLevels.All.Length; i++)
                {
                    int best = PlayerPrefs.GetInt("Sokoban.Best." + i, -1);
                    if (Button(new Rect(36 + i * 253, 110, 240, 44), (i + 1).ToString("00") + "  " + DemoLevels.All[i].title + (best >= 0 ? "  · 已完成" : ""), i == levelIndex, !menu)) LoadLevel(i);
                }
            }
            else Text(new Rect(38, 116, 760, 36), editing ? "地图编辑器  /  画一关自己的谜题" : "地图编辑器  /  试玩中", 22, mint, true);

            Fill(new Rect(36, 174, 770, 556), panel);
            DrawBoard(new Rect(58, 195, 726, 510));
            Fill(new Rect(830, 110, 414, 620), panel);
            if (editing) DrawEditor(); else DrawPlayPanel();
            Text(new Rect(40, 749, 1150, 28), editing ? "左键绘制 / 拖动连续绘制    ·    右键擦除    ·    地图边界自动阻挡移动" : "WASD / 方向键 移动    ·    Z 撤销    ·    R 重开    ·    Esc 菜单", 15, muted);
            if (!string.IsNullOrEmpty(notice) && Time.unscaledTime < noticeUntil)
            {
                Fill(new Rect(40, 678, 758, 45), Hex("284B51"));
                Text(new Rect(55, 687, 725, 32), notice, 16, ink);
            }
            if (menu) DrawMenu();
            GUI.matrix = oldMatrix;
        }

        void DrawPlayPanel()
        {
            Text(new Rect(858, 139, 350, 24), testing ? "CUSTOM LEVEL" : "BASIC LEVEL", 13, mint, true);
            Text(new Rect(856, 179, 357, 47), current.title, 30, ink, true);
            Text(new Rect(858, 243, 344, 78), current.hint, 18, muted);
            Fill(new Rect(858, 331, 352, 1), Hex("33485A"));
            Text(new Rect(858, 353, 110, 25), "步数", 15, muted);
            Text(new Rect(981, 353, 100, 25), "推动", 15, muted);
            Text(new Rect(1098, 353, 120, 25), "已送达", 15, muted);
            Text(new Rect(858, 383, 100, 44), board.Moves.ToString("00"), 32, ink, true);
            Text(new Rect(981, 383, 100, 44), board.Pushes.ToString("00"), 32, ink, true);
            Text(new Rect(1098, 383, 120, 44), board.Docked + " / " + board.Boxes.Count, 30, mint, true);
            if (Button(new Rect(858, 447, 170, 43), "撤销  Z", false, history.Count > 0 && !menu)) Undo();
            if (Button(new Rect(1040, 447, 170, 43), "重开  R", false, !menu)) Restart();

            if (board.Won)
            {
                Text(new Rect(858, 513, 350, 35), "通关！", 25, mint, true);
                Text(new Rect(858, 553, 352, 35), "所有箱子都已到达目标点。", 16, muted);
                if (Button(new Rect(858, 606, 352, 49), testing ? "返回编辑" : "重新开始", true, !menu))
                {
                    if (testing) OpenEditor(); else LoadLevel(0);
                }
            }
            else
            {
                Text(new Rect(858, 520, 350, 42), "把每个箱子推到目标点上。", 17, ink);
                Text(new Rect(858, 561, 350, 36), "只能推，不能拉；一次推动一个箱子。", 15, muted);
                if (Button(new Rect(858, 613, 352, 46), testing ? "返回编辑" : "打开地图编辑器", false, !menu)) OpenEditor();
            }
            if (testing && Button(new Rect(858, 674, 352, 32), "退出试玩 · 返回第 1 关", false, !menu)) LoadLevel(0);
        }

        void DrawEditor()
        {
            Text(new Rect(858, 139, 350, 24), "LEVEL WORKSHOP", 13, mint, true);
            Text(new Rect(856, 179, 360, 45), "地图编辑器", 27, ink, true);
            Text(new Rect(858, 236, 346, 58), "选择画笔，然后在左侧地图上绘制。目标点可以放在箱子或玩家下面。", 16, muted);
            string[] labels = { "地板 / 擦除", "墙壁", "目标点", "箱子", "玩家" };
            for (int i = 0; i < labels.Length; i++)
                if (Button(new Rect(858 + (i % 2) * 182, 310 + (i / 2) * 48, 170, 39), labels[i], tool == i, !menu)) tool = i;
            if (Button(new Rect(858, 474, 352, 48), "试玩这一关  →", true, !menu)) TestDraft();
            if (Button(new Rect(858, 535, 170, 40), "保存到本机", false, !menu)) SaveDraft();
            if (Button(new Rect(1040, 535, 170, 40), "读取保存", false, !menu)) LoadDraft();
            Text(new Rect(858, 594, 349, 60), "试玩前检查出生点、箱子和目标点数量。是否能解开，需要你来验证。", 15, muted);
            if (Button(new Rect(858, 673, 352, 32), "返回第 1 关（放弃未保存编辑）", false, !menu)) LoadLevel(0);
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
                    else if (c == '.' || c == '*' || c == '+')
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
                        if (!menu && tile.Contains(Event.current.mousePosition))
                        {
                            Fill(Inset(tile, 2), new Color(1, 1, 1, .12f));
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
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - moveTime) / .105f));
                for (int i = 0; i < board.Boxes.Count; i++)
                {
                    Vector2 p = Vector2.Lerp(fromBoxes[i], board.Boxes[i], t);
                    DrawBox(new Rect(left + p.x * cell, top + p.y * cell, cell, cell), board.Goals.Contains(board.Boxes[i]));
                }
                Vector2 robot = Vector2.Lerp(fromPlayer, board.Player, t);
                DrawPlayer(new Rect(left + robot.x * cell, top + robot.y * cell, cell, cell));
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

        void DrawMenu()
        {
            Fill(new Rect(0, 0, 1280, 800), new Color(.025f, .06f, .10f, .91f));
            Fill(new Rect(410, 192, 460, 420), panel);
            Text(new Rect(450, 225, 380, 48), "休息一下", 34, ink, true);
            Text(new Rect(450, 286, 380, 38), "随时继续测试。", 18, muted);
            if (Button(new Rect(450, 350, 380, 51), "继续  /  Esc", true)) menu = false;
            if (Button(new Rect(450, 418, 380, 47), "地图编辑器")) OpenEditor();
            if (Button(new Rect(450, 481, 380, 47), "返回第 1 关")) LoadLevel(0);
            Text(new Rect(450, 552, 380, 28), "推箱子原型 · Unity Demo", 14, muted);
        }

        bool Button(Rect rect, string label, bool accent = false, bool enabled = true)
        {
            bool hover = rect.Contains(Event.current.mousePosition) && enabled;
            Color color = accent ? mint : hover ? Hex("3C586B") : Hex("2B4255");
            if (!enabled) color = Hex("223445");
            Fill(rect, color);
            Text(Inset(rect, 6), label, 16, enabled ? accent ? background : ink : Hex("627788"), accent, TextAnchor.MiddleCenter);
            bool old = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = old;
            return clicked;
        }

        void Text(Rect rect, string value, int size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            textStyle.fontSize = size;
            textStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            textStyle.normal.textColor = color;
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
        void Play(AudioClip clip) { if (!mutedAudio) speaker.PlayOneShot(clip, .16f); }
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

