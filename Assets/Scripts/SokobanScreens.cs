using System;
using System.IO;
using UnityEngine;

namespace Kuluo.Sokoban
{
    public partial class SokobanDemo
    {
        bool drawingOverlay;

        void OnGUI()
        {
            if (board == null) return;
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { font = font, wordWrap = true, richText = false, padding = new RectOffset() };
                fieldStyle = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 17, padding = new RectOffset(10, 10, 7, 5) };
                fieldStyle.normal.textColor = fieldStyle.focused.textColor = ink;
                fieldStyle.hover = fieldStyle.normal;
            }
            if (resetFocus) { GUI.FocusControl(null); resetFocus = false; }
            var oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / CanvasWidth, Screen.height / CanvasHeight);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - CanvasWidth * scale) / 2, (Screen.height - CanvasHeight * scale) / 2), Quaternion.identity, new Vector3(scale, scale, 1));
            drawingOverlay = false;
            Fill(new Rect(0, 0, 1280, 800), background);
            Fill(new Rect(32, 35, 5, 42), mint);
            Text(new Rect(52, 30, 580, 38), "推箱子", 30, ink, true);

            if (page != Page.Menu && Button(new Rect(1032, 38, 100, 38), "设置")) settingsOpen = true;
            if (page == Page.Play && Button(new Rect(1144, 38, 100, 38), "暂停  Esc")) menu = true;
            if (page != Page.Menu)
            {
                string back = testing && page == Page.Play ? "← 返回编辑" : "← 主菜单";
                if (page == Page.Editor) back = "← 我的关卡";
                if (Button(new Rect(750, 38, 180, 38), back))
                {
                    if (page == Page.Editor) LeaveEditor(() => Go(Page.Library));
                    else if (page == Page.Play && testing) Go(Page.Editor);
                    else Go(Page.Menu);
                }
            }

            switch (page)
            {
                case Page.Menu: DrawMainMenu(); break;
                case Page.Home: DrawHome(); break;
                case Page.Library: DrawLibrary(); break;
                case Page.Editor:
                case Page.Play:
                    Text(new Rect(38, 119, 758, 36), editing ? "地图编辑器" + (dirty ? "  · 未保存" : "  · 已保存") : testing ? "自定义关卡 · 试玩" : "第 " + (levelIndex + 1) + " / " + DemoLevels.All.Length + " 关", 21, mint, true);
                    Fill(new Rect(36, 174, 770, 556), panel);
                    DrawBoard(new Rect(58, 195, 726, 510));
                    Fill(new Rect(830, 110, 414, 620), panel);
                    if (editing) DrawEditor(); else DrawPlayPanel();
                    Text(new Rect(40, 749, 1150, 28), editing ? "左键绘制 / 拖动连续绘制    ·    右键擦除    ·    Ctrl + Z 撤销编辑" : "WASD / 方向键 移动    ·    Z 撤销    ·    R 重开    ·    Esc 暂停", 15, muted);
                    break;
            }
            if (settingsOpen)
            {
                drawingOverlay = true;
                DrawSettings();
                drawingOverlay = false;
            }
            if (menu)
            {
                drawingOverlay = !settingsOpen;
                DrawPause();
                drawingOverlay = false;
            }
            if (filePicker)
            {
                drawingOverlay = confirmAction == null && discardAction == null;
                DrawFilePicker();
                drawingOverlay = false;
            }
            if (discardAction != null || confirmAction != null)
            {
                drawingOverlay = true;
                DrawConfirmation();
                drawingOverlay = false;
            }
            if (!string.IsNullOrEmpty(notice) && Time.unscaledTime < noticeUntil)
            {
                Fill(new Rect(40, 684, 758, 44), Hex("284B51"));
                Text(new Rect(53, 690, 730, 36), notice, 14, ink);
            }
            GUI.matrix = oldMatrix;
        }

        void DrawMainMenu()
        {
            Text(new Rect(128, 170, 580, 72), "推箱子", 52, ink, true);
            Text(new Rect(128, 251, 580, 40), "每一次推动，都多想一步。", 25, muted);
            // Both columns share y=326..656 and have equal 440-unit widths.
            DrawPreview(DemoLevels.All[0], new Rect(128, 326, 440, 330));
            if (Button(new Rect(712, 326, 440, 50), "开始游戏", true)) StartGame();
            if (Button(new Rect(712, 396, 440, 50), "选择关卡")) Go(Page.Home);
            if (Button(new Rect(712, 466, 440, 50), "编辑地图")) Go(Page.Library);
            if (Button(new Rect(712, 536, 440, 50), "设置")) settingsOpen = true;
            if (Button(new Rect(712, 606, 440, 50), "退出游戏")) ConfirmQuit();
        }

        void DrawSettings()
        {
            Shade();
            Fill(new Rect(330, 218, 620, 374), panel);
            Text(new Rect(372, 250, 536, 45), "设置", 30, ink, true);
            Text(new Rect(372, 319, 180, 28), "音乐音量", 18, ink);
            musicVolume = VolumeSlider(new Rect(372, 360, 438, 28), musicVolume);
            music.volume = musicVolume * .3f;
            Text(new Rect(830, 355, 78, 32), Mathf.RoundToInt(musicVolume * 100) + "%", 20, mint);
            Text(new Rect(372, 411, 180, 28), "音效音量", 18, ink);
            float volume = VolumeSlider(new Rect(372, 452, 438, 28), effectsVolume);
            if (!Mathf.Approximately(volume, effectsVolume)) { effectsVolume = volume; effectsPreviewPending = true; }
            Text(new Rect(830, 447, 78, 32), Mathf.RoundToInt(effectsVolume * 100) + "%", 20, mint);
            if (effectsPreviewPending && (Event.current.rawType == EventType.MouseUp || Event.current.rawType == EventType.KeyUp))
            { effectsPreviewPending = false; Play(buttonClip); }
            if (Button(new Rect(372, 519, 536, 43), "完成", true)) CloseSettings();
        }

        GUIStyle volumeTrack, volumeThumb;
        float VolumeSlider(Rect rect, float value)
        {
            if (volumeTrack == null)
            {
                volumeTrack = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 8, margin = new RectOffset(0, 0, 10, 0) };
                volumeThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 20, fixedHeight = 28 };
                volumeThumb.normal.background = volumeThumb.hover.background = volumeThumb.active.background = Texture2D.whiteTexture;
            }
            Color previous = GUI.color;
            GUI.color = mint;
            float result = GUI.HorizontalSlider(rect, value, 0, 1, volumeTrack, volumeThumb);
            GUI.color = previous;
            return result;
        }

        void DrawHome()
        {
            Text(new Rect(38, 128, 760, 52), "从第一步，到多想一步。", 32, ink, true);
            Text(new Rect(40, 190, 770, 32), "把箱子送到绿色目标点。   " + progress.CompletedCount + " / " + DemoLevels.All.Length + " 已完成", 18, muted);
            int next = hasCampaignSession && !board.Won ? levelIndex : progress.lastPlayed;
            string action = hasCampaignSession && !board.Won ? "继续游戏" : progress.CompletedCount == DemoLevels.All.Length ? "再玩一次" : progress.CompletedCount == 0 ? "开始游戏" : "继续第 " + (next + 1) + " 关";
            if (Button(new Rect(960, 135, 284, 60), action, true)) LoadLevel(next);

            for (int i = 0; i < DemoLevels.All.Length; i++)
            {
                float x = 36 + (i % 4) * 304, y = 250 + (i / 4) * 213;
                bool unlocked = progress.CanPlay(i);
                Fill(new Rect(x, y, 290, 197), panel);
                Text(new Rect(x + 16, y + 14, 262, 26), (i + 1).ToString("00") + "  ·  " + DemoLevels.Lessons[i], 17, i == 3 ? Hex("9ADCF4") : i == 4 ? Hex("C7A0E8") : mint, true);
                DrawPreview(DemoLevels.All[i], new Rect(x + 12, y + 56, 110, 110));
                Text(new Rect(x + 136, y + 54, 142, 33), DemoLevels.All[i].title, 21, ink, true);
                Text(new Rect(x + 136, y + 96, 142, 40), progress.bestMoves[i] > 0 ? "最佳 " + progress.bestMoves[i] + " 步" : unlocked ? "准备好就开始吧" : "完成上一关后解锁", 14, muted);
                if (Button(new Rect(x + 136, y + 144, 142, 36), unlocked ? progress.bestMoves[i] > 0 ? "重玩" : "进入" : "尚未解锁", unlocked, unlocked)) LoadLevel(i);
            }
            Text(new Rect(40, 709, 600, 32), "也可以自己设计一个谜题。", 18, muted);
            if (Button(new Rect(848, 696, 190, 48), "我的关卡")) Go(Page.Library);
            if (Button(new Rect(1054, 696, 190, 48), "新建关卡")) BeginDraft(LevelData.Empty(8, 6));
        }

        void DrawPreview(LevelData data, Rect area)
        {
            float cell = Mathf.Min(area.width / data.rows[0].Length, area.height / data.rows.Length);
            float left = area.center.x - cell * data.rows[0].Length / 2;
            for (int y = 0; y < data.rows.Length; y++)
                for (int x = 0; x < data.rows[y].Length; x++)
                {
                    var rect = new Rect(left + x * cell, area.y + y * cell, cell, cell);
                    char c = data.rows[y][x];
                    Fill(Inset(rect, 1), c == '#' ? Hex("496174") : Hex("263E50"));
                    if (c != '#') DrawTerrain(rect, data.TerrainAt(x, y), false);
                    if (c == '.' || c == '*' || c == '+') Fill(Inset(rect, cell * .32f), mint);
                    if (c == '$' || c == '*') DrawBox(rect, c == '*');
                    if (c == '@' || c == '+') DrawPlayer(rect);
                }
        }

        void DrawPlayPanel()
        {
            Text(new Rect(858, 139, 350, 24), testing ? "试玩" : DemoLevels.Lessons[levelIndex], 15, mint, true);
            Text(new Rect(856, 181, 357, 62), current.title, 29, ink, true);
            Text(new Rect(858, 253, 350, 102), current.hint, 18, ink);
            Fill(new Rect(858, 374, 352, 1), Hex("33485A"));
            Text(new Rect(858, 397, 165, 26), "步数", 16, muted);
            Text(new Rect(1040, 397, 170, 26), "箱子到位", 16, muted);
            Text(new Rect(858, 430, 165, 45), board.Moves.ToString("00"), 34, ink, true);
            Text(new Rect(1040, 430, 170, 45), board.Docked + " / " + board.Boxes.Count, 34, mint, true);
            if (Button(new Rect(858, 496, 170, 45), "撤销  Z", false, history.Count > 0)) Undo();
            if (Button(new Rect(1040, 496, 170, 45), "重开  R")) ResetBoard();
            if (board.Won)
            {
                Text(new Rect(858, 563, 350, 38), testing ? "设计通过！" : levelIndex == DemoLevels.All.Length - 1 ? "全部关卡完成！" : "完成！", 25, mint, true);
                if (Button(new Rect(858, 622, 352, 51), testing ? "继续编辑" : levelIndex + 1 < DemoLevels.All.Length ? "下一关 →" : "选择关卡", true))
                {
                    if (testing) Go(Page.Editor);
                    else if (levelIndex + 1 < DemoLevels.All.Length) LoadLevel(levelIndex + 1);
                    else Go(Page.Home);
                }
            }
            else
            {
                string tip = board.HasCorneredBox ? "有箱子卡在墙角了，试试撤销。" :
                    board.Doors.Count > 0 ? board.DoorsOpen ? "压力板已压下 · 门已打开" : "门关闭 · 找到紫色压力板" :
                    board.Ice.Count > 0 ? "蓝色条纹是冰面，墙壁可以帮助刹车。" : "走错也没关系，随时可以撤销。";
                Text(new Rect(858, 565, 350, 64), tip, 17, board.HasCorneredBox ? gold : muted);
                if (testing && Button(new Rect(858, 650, 352, 44), "继续编辑", true)) Go(Page.Editor);
            }
        }

        void DrawLibrary()
        {
            Text(new Rect(38, 120, 420, 36), "我的关卡  ·  " + library.levels.Count, 25, ink, true);
            if (Button(new Rect(832, 115, 192, 44), "导入关卡文件")) OpenFiles(false);
            if (Button(new Rect(1040, 115, 204, 44), "新建关卡", true)) BeginDraft(LevelData.Empty(8, 6));
            Fill(new Rect(36, 181, 770, 529), panel);
            int pageCount = Math.Max(1, (library.levels.Count + 5) / 6);
            libraryPage = Mathf.Clamp(libraryPage, 0, pageCount - 1);
            if (library.levels.Count == 0) Text(new Rect(72, 232, 680, 110), "还没有自己的关卡。\n点击「新建关卡」，或者导入一个关卡文件。", 23, muted);
            for (int n = 0; n < 6; n++)
            {
                int i = libraryPage * 6 + n;
                if (i >= library.levels.Count) break;
                var level = library.levels[i];
                bool valid = level.data.Validate() == null;
                string name = level.data.title.Length > 24 ? level.data.title.Substring(0, 24) + "…" : level.data.title;
                if (Button(new Rect(55, 198 + n * 77, 732, 64), name + "   ·   " + level.data.rows[0].Length + " × " + level.data.rows.Length + (valid ? "   可试玩" : "   草稿"), selectedId == level.id)) selectedId = level.id;
            }
            if (Button(new Rect(36, 728, 120, 36), "上一页", false, libraryPage > 0)) libraryPage--;
            Text(new Rect(170, 736, 200, 25), (libraryPage + 1) + " / " + pageCount, 16, muted);
            if (Button(new Rect(310, 728, 120, 36), "下一页", false, libraryPage + 1 < pageCount)) libraryPage++;
            Fill(new Rect(830, 181, 414, 529), panel);
            var selected = library.levels.Find(l => l.id == selectedId);
            Text(new Rect(858, 214, 352, 95), selected == null ? "选择一个关卡" : selected.data.title, 26, ink, true);
            Text(new Rect(858, 320, 352, 86), selected == null ? "关卡可以随时改名、编辑或导出。" : selected.data.Validate() ?? "关卡结构有效，可以开始试玩。", 17, muted);
            if (Button(new Rect(858, 425, 352, 48), "编辑 / 重命名", true, selected != null)) BeginDraft(selected.data, selected.id);
            if (Button(new Rect(858, 490, 352, 42), "试玩", false, selected != null && selected.data.Validate() == null))
            { BeginDraft(selected.data, selected.id); TestDraft(); }
            if (Button(new Rect(858, 548, 352, 42), "导出 JSON", false, selected != null))
            { draft = selected.data.Copy(); OpenFiles(true); }
            if (Button(new Rect(858, 629, 352, 42), "删除关卡", false, selected != null && libraryWritable)) DeleteLevel(selected);
        }

        void DrawEditor()
        {
            Text(new Rect(858, 130, 352, 28), "关卡名称", 16, muted);
            string name = Field(new Rect(858, 166, 352, 40), draft.title ?? "", 40);
            if (name != draft.title) { draft.title = name; MarkDirty(); }
            Text(new Rect(858, 222, 352, 23), "宽度 3–16  /  高度 3–14", 15, muted);
            desiredWidth = Stepper(858, 256, desiredWidth, 3, 16);
            desiredHeight = Stepper(1040, 256, desiredHeight, 3, 14);
            if (Button(new Rect(858, 300, 352, 34), "应用尺寸")) ResizeDraft();
            string[] labels = { "擦除", "墙壁", "目标点", "箱子", "玩家", "冰面", "压力板", "门" };
            for (int i = 0; i < labels.Length; i++)
                if (Button(new Rect(858 + (i % 2) * 182, 347 + (i / 2) * 39, 170, 33), labels[i], tool == i)) tool = i;
            if (Button(new Rect(1040, 512, 170, 36), "撤销编辑", false, editorUndo.Count > 0)) UndoEdit();
            if (Button(new Rect(858, 512, 170, 36), "试玩 →", true)) TestDraft();
            if (Button(new Rect(858, 557, 170, 39), "保存关卡", false, libraryWritable)) SaveDraft();
            if (Button(new Rect(1040, 557, 170, 39), "导出文件")) OpenFiles(true);
            Text(new Rect(858, 610, 352, 45), "先画地形，再放箱子或目标点。压力板控制地图上的所有门。", 14, muted);
            if (Button(new Rect(858, 675, 352, 32), "返回我的关卡")) LeaveEditor(() => Go(Page.Library));
        }

        int Stepper(float x, float y, int value, int min, int max)
        {
            if (Button(new Rect(x, y, 39, 33), "−", false, value > min)) value--;
            Text(new Rect(x + 45, y + 4, 80, 28), value.ToString(), 20, ink, true, TextAnchor.MiddleCenter);
            if (Button(new Rect(x + 130, y, 40, 33), "+", false, value < max)) value++;
            return value;
        }

        void DrawPause()
        {
            Shade();
            Fill(new Rect(410, 180, 460, 440), panel);
            Text(new Rect(450, 221, 380, 48), "已暂停", 34, ink, true);
            if (Button(new Rect(450, 322, 380, 52), "继续  /  Esc", true)) menu = false;
            if (Button(new Rect(450, 396, 380, 47), testing ? "返回编辑" : "选择关卡"))
            { if (testing) Go(Page.Editor); else Go(Page.Home); }
            if (!testing && Button(new Rect(450, 464, 380, 47), "用这张地图创作")) BeginDraft(current);
            if (Button(new Rect(450, 540, 380, 43), "主菜单")) { if (testing) { menu = false; LeaveEditor(() => Go(Page.Menu)); } else Go(Page.Menu); }
        }

        void DrawConfirmation()
        {
            Shade();
            Fill(new Rect(330, 260, 620, 300), panel);
            Text(new Rect(366, 296, 548, 100), discardAction != null ? "当前关卡有未保存的修改。" : confirmText, 23, ink, true);
            if (discardAction != null)
            {
                if (Button(new Rect(366, 454, 170, 47), "保存并离开", true, libraryWritable))
                {
                    if (SaveDraft()) { var action = discardAction; discardAction = null; action(); }
                }
                if (Button(new Rect(553, 454, 170, 47), "放弃修改")) { var action = discardAction; discardAction = null; action(); }
                if (Button(new Rect(740, 454, 170, 47), "取消")) discardAction = null;
            }
            else
            {
                if (Button(new Rect(366, 454, 261, 47), "确认", true)) { var action = confirmAction; confirmAction = null; action(); }
                if (Button(new Rect(646, 454, 264, 47), "取消")) confirmAction = null;
            }
        }

        void DrawFilePicker()
        {
            Shade();
            Fill(new Rect(170, 102, 940, 588), panel);
            Text(new Rect(200, 124, 780, 40), exporting ? "导出关卡文件" : "导入关卡文件", 26, ink, true);
            directoryText = Field(new Rect(200, 183, 648, 38), directoryText ?? "", 1024);
            if (Button(new Rect(862, 183, 99, 38), "打开路径")) RefreshFiles();
            if (Button(new Rect(975, 183, 99, 38), "上一级"))
            {
                try { var parent = Directory.GetParent(directoryText); if (parent != null) { directoryText = parent.FullName; RefreshFiles(); } }
                catch (Exception e) { fileError = e.Message; }
            }
            Rect viewport = new Rect(200, 238, 874, 260);
            fileScroll = GUI.BeginScrollView(viewport, fileScroll, new Rect(0, 0, 849, Math.Max(260, (folders.Length + files.Length) * 36)));
            for (int i = 0; i < folders.Length; i++)
                if (Button(new Rect(0, i * 36, 843, 31), "文件夹  /  " + Path.GetFileName(folders[i])))
                { directoryText = folders[i]; RefreshFiles(); break; }
            for (int i = 0; i < files.Length; i++)
                if (Button(new Rect(0, (folders.Length + i) * 36, 843, 31), Path.GetFileName(files[i])))
                { if (exporting) filename = Path.GetFileName(files[i]); else ImportFile(files[i]); break; }
            GUI.EndScrollView();
            Text(new Rect(200, 509, 875, 41), fileError.Length > 0 ? fileError : exporting ? "选择文件夹并输入文件名。可以把文件分享给其他人。" : "点击 JSON 文件导入；也可以在上方输入文件夹路径。", 15, fileError.Length > 0 ? gold : muted);
            if (exporting)
            {
                filename = Field(new Rect(200, 565, 620, 41), filename, 180);
                if (Button(new Rect(835, 565, 239, 41), "导出", true)) ExportFile();
            }
            if (Button(new Rect(834, 628, 240, 38), "取消")) filePicker = false;
        }

        void Shade() { Fill(new Rect(0, 0, 1280, 800), new Color(.025f, .06f, .10f, .93f)); }
        string Field(Rect rect, string value, int maxLength)
        {
            bool old = GUI.enabled;
            GUI.enabled = !OverlayOpen || drawingOverlay;
            string result = GUI.TextField(rect, value, maxLength, fieldStyle);
            GUI.enabled = old;
            return result;
        }
        bool Button(Rect rect, string label, bool accent = false, bool enabled = true)
        {
            enabled = enabled && (!OverlayOpen || drawingOverlay);
            bool hover = rect.Contains(Event.current.mousePosition) && enabled;
            Color color = accent ? (hover ? Hex("94F2D9") : mint) : hover ? Hex("3C586B") : Hex("2B4255");
            if (!enabled) color = Hex("223445");
            if (hover) { Fill(rect, mint); Fill(Inset(rect, 2), color); }
            else Fill(rect, color);
            Text(Inset(rect, 6), label, 16, enabled ? accent ? background : ink : Hex("627788"), accent, TextAnchor.MiddleCenter);
            bool old = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = old;
            if (clicked && buttonClip != null) Play(buttonClip);
            return clicked;
        }
    }
}
