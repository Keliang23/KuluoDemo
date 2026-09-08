using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kuluo.Sokoban
{
    public partial class SokobanDemo
    {
        [Header("Scene References")]
        public RectTransform uiRoot;
        public SokobanBoardView boardView;
        public Button listEntryPrefab;
        readonly Dictionary<string, Transform> widgets = new Dictionary<string, Transform>();
        string fileListKey;
        int filePage;
        readonly List<GameObject> fileEntries = new List<GameObject>();
        Transform W(string id) => widgets[id];
        T C<T>(string id) where T : Component => W(id).GetComponent<T>();
        void Visible(string id, bool value) { if (W(id).gameObject.activeSelf != value) W(id).gameObject.SetActive(value); }
        void Label(string id, string value) { var t = C<TMP_Text>(id); if (t.text != value) t.text = value; }
        void Caption(string id, string value) { var t = W(id).GetComponentInChildren<TMP_Text>(true); if (t.text != value) t.text = value; }
        void Enabled(string id, bool value) { C<Selectable>(id).interactable = value; }
        void Click(string id, Action action)
        {
            C<Button>(id).onClick.AddListener(() => { Play(buttonClip); action(); RefreshUI(); });
        }
        bool IsTyping() => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;

        void BindUI()
        {
            if (uiRoot == null || boardView == null) { Debug.LogError("请打开 SokobanDemo 场景，界面引用缺失。", this); enabled = false; return; }
            foreach (var t in uiRoot.GetComponentsInChildren<Transform>(true))
                if (!widgets.ContainsKey(t.name)) widgets.Add(t.name, t);
            Click("StartGame", StartGame);
            Click("SelectLevels", () => Go(Page.Home));
            Click("EditMaps", () => Go(Page.Library));
            Click("MenuSettings", () => settingsOpen = true);
            Click("Quit", ConfirmQuit);
            Click("HeaderSettings", () => settingsOpen = true);
            Click("HeaderPause", () => menu = true);
            Click("HeaderBack", () => { if (editing) LeaveEditor(() => Go(Page.Library)); else if (testing && page == Page.Play) Go(Page.Editor); else Go(Page.Menu); });
            Click("ResumeCampaign", () => LoadLevel(hasCampaignSession && !board.Won ? levelIndex : progress.lastPlayed));
            for (int i = 0; i < DemoLevels.All.Length; i++) { int index = i; Click("Level" + i, () => LoadLevel(index)); }
            Click("HomeLibrary", () => Go(Page.Library));
            Click("HomeCreate", () => BeginDraft(LevelData.Empty(8, 6)));
            Click("UndoMove", Undo); Click("Restart", ResetBoard);
            Click("NextLevel", () => { if (testing) Go(Page.Editor); else if (levelIndex + 1 < DemoLevels.All.Length) LoadLevel(levelIndex + 1); else Go(Page.Home); });
            Click("ReturnToEdit", () => Go(Page.Editor));
            Click("LibraryImport", () => OpenFiles(false));
            Click("LibraryCreate", () => BeginDraft(LevelData.Empty(8, 6)));
            Click("LibraryPrev", () => libraryPage--); Click("LibraryNext", () => libraryPage++);
            for (int i = 0; i < 6; i++) { int slot = i; Click("LibraryRow" + i, () => selectedId = library.levels[libraryPage * 6 + slot].id); }
            Click("LibraryEdit", () => { var s = Selected(); if (s != null) BeginDraft(s.data, s.id); });
            Click("LibraryTest", () => { var s = Selected(); if (s != null) { BeginDraft(s.data, s.id); TestDraft(); } });
            Click("LibraryExport", () => { var s = Selected(); if (s != null) { draft = s.data.Copy(); OpenFiles(true); } });
            Click("LibraryDelete", () => { var s = Selected(); if (s != null) DeleteLevel(s); });
            C<TMP_InputField>("LevelName").onValueChanged.AddListener(v => { if (draft != null) { draft.title = v; MarkDirty(); } });
            Click("WidthMinus", () => desiredWidth = Mathf.Max(3, desiredWidth - 1));
            Click("WidthPlus", () => desiredWidth = Mathf.Min(16, desiredWidth + 1));
            Click("HeightMinus", () => desiredHeight = Mathf.Max(3, desiredHeight - 1));
            Click("HeightPlus", () => desiredHeight = Mathf.Min(14, desiredHeight + 1));
            Click("ResizeMap", ResizeDraft);
            for (int i = 0; i < 8; i++) { int brush = i; Click("Brush" + i, () => tool = brush); }
            Click("UndoEdit", UndoEdit); Click("TestMap", TestDraft);
            Click("SaveMap", () => SaveDraft()); Click("ExportMap", () => OpenFiles(true));
            Click("EditorBack", () => LeaveEditor(() => Go(Page.Library)));
            Click("SettingsDone", CloseSettings);
            C<Slider>("MusicSlider").SetValueWithoutNotify(musicVolume);
            C<Slider>("EffectsSlider").SetValueWithoutNotify(effectsVolume);
            C<Slider>("MusicSlider").onValueChanged.AddListener(v => { musicVolume = v; music.volume = v * .3f; });
            C<Slider>("EffectsSlider").onValueChanged.AddListener(v => effectsVolume = v);
            C<SokobanSliderFeedback>("EffectsSlider").released += () => Play(buttonClip);
            Click("PauseContinue", () => menu = false);
            Click("PauseSelect", () => Go(testing ? Page.Editor : Page.Home));
            Click("PauseCopy", () => BeginDraft(current));
            Click("PauseHome", () => { if (testing) { menu = false; LeaveEditor(() => Go(Page.Menu)); } else Go(Page.Menu); });
            Click("ConfirmYes", () => { var action = confirmAction; confirmAction = null; action?.Invoke(); });
            Click("ConfirmNo", () => confirmAction = null);
            Click("DiscardSave", () => { if (SaveDraft()) { var action = discardAction; discardAction = null; action?.Invoke(); } });
            Click("DiscardYes", () => { var action = discardAction; discardAction = null; action?.Invoke(); });
            Click("DiscardNo", () => discardAction = null);
            C<TMP_InputField>("Directory").onValueChanged.AddListener(v => directoryText = v);
            C<TMP_InputField>("Filename").onValueChanged.AddListener(v => filename = v);
            Click("OpenDirectory", RefreshFiles);
            Click("ParentDirectory", () => { try { var p = Directory.GetParent(directoryText); if (p != null) { directoryText = p.FullName; RefreshFiles(); } } catch (Exception e) { fileError = e.Message; } });
            Click("FileExport", ExportFile); Click("FileCancel", () => filePicker = false);
            Click("FilePrev", () => { filePage--; fileListKey = null; });
            Click("FileNext", () => { filePage++; fileListKey = null; });
            C<SokobanBoardInput>("BoardViewport").paint += (uv, erase) =>
            {
                if (!editing || OverlayOpen) return;
                if (boardView.TryCell(uv, out var cell)) Paint(cell.x, cell.y, erase);
            };
            RefreshUI();
        }
        SavedLevel Selected() => library.levels.Find(l => l.id == selectedId);
        void LateUpdate() { if (uiRoot != null && widgets.Count > 0) RefreshUI(); }
        void SyncInput(string id, string value)
        {
            var input = C<TMP_InputField>(id);
            if (!input.isFocused && input.text != (value ?? "")) input.SetTextWithoutNotify(value ?? "");
        }
        void RefreshUI()
        {
            if (board == null) return;
            if (resetFocus) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); resetFocus = false; }
            Visible("MenuPage", page == Page.Menu); Visible("HomePage", page == Page.Home);
            Visible("LibraryPage", page == Page.Library); Visible("BoardPage", page == Page.Play || editing);
            Visible("PlayPanel", page == Page.Play); Visible("EditorPanel", editing);
            Visible("HeaderBack", page != Page.Menu); Visible("HeaderSettings", page != Page.Menu); Visible("HeaderPause", page == Page.Play);
            Caption("HeaderBack", editing ? "← 我的关卡" : testing && page == Page.Play ? "← 返回编辑" : "← 主菜单");
            var group = C<CanvasGroup>("Content"); group.interactable = !OverlayOpen; group.blocksRaycasts = !OverlayOpen;
            Visible("SettingsModal", settingsOpen); Visible("PauseModal", menu && !settingsOpen);
            Visible("FilesModal", filePicker); Visible("ConfirmModal", confirmAction != null); Visible("DiscardModal", discardAction != null);
            C<CanvasGroup>("FilesModal").interactable = confirmAction == null && discardAction == null;
            Label("MusicPercent", Mathf.RoundToInt(musicVolume * 100) + "%");
            Label("EffectsPercent", Mathf.RoundToInt(effectsVolume * 100) + "%");
            Label("ConfirmMessage", confirmText ?? ""); Enabled("DiscardSave", libraryWritable);
            Visible("Notice", !string.IsNullOrEmpty(notice) && Time.unscaledTime < noticeUntil); Label("NoticeText", notice);
            Caption("PauseSelect", testing ? "返回编辑" : "选择关卡"); Visible("PauseCopy", !testing);
            if (page == Page.Home)
            {
                Label("CampaignProgress", "把箱子送到绿色目标点。   " + progress.CompletedCount + " / " + DemoLevels.All.Length + " 已完成");
                Caption("ResumeCampaign", hasCampaignSession && !board.Won ? "继续游戏" : progress.CompletedCount == DemoLevels.All.Length ? "再玩一次" : progress.CompletedCount == 0 ? "开始游戏" : "继续第 " + (progress.lastPlayed + 1) + " 关");
                for (int i = 0; i < DemoLevels.All.Length; i++)
                {
                    bool unlocked = progress.CanPlay(i); Enabled("Level" + i, unlocked);
                    Label("LevelTitle" + i, DemoLevels.All[i].title);
                    Label("LevelLesson" + i, (i + 1).ToString("00") + "  " + DemoLevels.Lessons[i]);
                    Label("LevelStatus" + i, progress.bestMoves[i] > 0 ? "最佳 " + progress.bestMoves[i] + " 步" : unlocked ? "准备好就开始吧" : "完成上一关后解锁");
                    Caption("Level" + i, unlocked ? progress.bestMoves[i] > 0 ? "重玩" : "进入" : "尚未解锁");
                }
            }
            if (page == Page.Play || editing)
            {
                Label("BoardHeading", editing ? "地图编辑器" + (dirty ? "  未保存" : "  已保存") : testing ? "自定义关卡  试玩" : "第 " + (levelIndex + 1) + " / " + DemoLevels.All.Length + " 关");
                Label("BoardHelp", editing ? "左键绘制 / 拖动连续绘制  右键擦除  Ctrl + Z 撤销编辑" : "WASD / 方向键 移动  Z 撤销  R 重开  Esc 暂停");
                boardView.gameObject.SetActive(true);
                boardView.Show(editing ? draft : current, editing ? null : board);
                if (!editing)
                {
                    float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - moveTime) / moveDuration));
                    boardView.Animate(board, fromPlayer, fromBoxes, t);
                    Label("PlayLesson", testing ? "试玩" : DemoLevels.Lessons[levelIndex]); Label("PlayTitle", current.title); Label("PlayHint", current.hint);
                    Label("MoveCount", board.Moves.ToString("00")); Label("DockCount", board.Docked + " / " + board.Boxes.Count);
                    Enabled("UndoMove", history.Count > 0);
                    Label("PlayTip", board.Won ? testing ? "设计通过！" : levelIndex == DemoLevels.All.Length - 1 ? "全部关卡完成！" : "完成！" :
                        board.HasCorneredBox ? "有箱子卡在墙角了，试试撤销。" : board.Doors.Count > 0 ? board.DoorsOpen ? "压力板已压下  门已打开" : "门关闭  找到紫色压力板" : board.Ice.Count > 0 ? "箱子在冰面上滑行，利用障碍停下。" : "走错也没关系，随时可以撤销。");
                    Visible("NextLevel", board.Won); Visible("ReturnToEdit", testing && !board.Won);
                    Caption("NextLevel", testing ? "继续编辑" : levelIndex + 1 < DemoLevels.All.Length ? "下一关 →" : "选择关卡");
                }
                else
                {
                    SyncInput("LevelName", draft.title); Label("MapWidth", desiredWidth.ToString()); Label("MapHeight", desiredHeight.ToString());
                    Enabled("WidthMinus", desiredWidth > 3); Enabled("WidthPlus", desiredWidth < 16); Enabled("HeightMinus", desiredHeight > 3); Enabled("HeightPlus", desiredHeight < 14);
                    Enabled("UndoEdit", editorUndo.Count > 0); Enabled("SaveMap", libraryWritable);
                    for (int i = 0; i < 8; i++) W("Brush" + i).GetComponent<SokobanButtonStyle>().SetSelected(tool == i);
                }
            }
            else boardView.gameObject.SetActive(false);
            if (page == Page.Library)
            {
                int pages = Math.Max(1, (library.levels.Count + 5) / 6); libraryPage = Mathf.Clamp(libraryPage, 0, pages - 1);
                Label("LibraryHeading", "我的关卡  " + library.levels.Count); Label("LibraryPageNumber", (libraryPage + 1) + " / " + pages);
                Visible("LibraryEmpty", library.levels.Count == 0); Enabled("LibraryPrev", libraryPage > 0); Enabled("LibraryNext", libraryPage + 1 < pages);
                for (int n = 0; n < 6; n++)
                {
                    int i = libraryPage * 6 + n; Visible("LibraryRow" + n, i < library.levels.Count);
                    if (i >= library.levels.Count) continue;
                    var l = library.levels[i]; Caption("LibraryRow" + n, l.data.title + "  " + l.data.rows[0].Length + " × " + l.data.rows.Length + (l.data.Validate() == null ? "  可试玩" : "  草稿"));
                    C<SokobanButtonStyle>("LibraryRow" + n).SetSelected(l.id == selectedId);
                }
                var s = Selected(); Label("SelectedTitle", s == null ? "选择一个关卡" : s.data.title);
                Label("SelectedHint", s == null ? "关卡可以随时改名、编辑或导出。" : s.data.Validate() ?? "关卡结构有效，可以开始试玩。");
                Enabled("LibraryEdit", s != null); Enabled("LibraryTest", s != null && s.data.Validate() == null); Enabled("LibraryExport", s != null); Enabled("LibraryDelete", s != null && libraryWritable);
            }
            if (filePicker) UpdateFilesUI();
        }
        void UpdateFilesUI()
        {
            Label("FilesTitle", exporting ? "导出关卡文件" : "导入关卡文件"); SyncInput("Directory", directoryText); SyncInput("Filename", filename);
            Visible("Filename", exporting); Visible("FileExport", exporting);
            Label("FilesHint", !string.IsNullOrEmpty(fileError) ? fileError : exporting ? "选择文件夹并输入文件名。" : "点击 JSON 文件导入，或在上方输入文件夹路径。");
            string directoryKey = directoryText + "|" + string.Join("|", folders) + "|" + string.Join("|", files);
            if (lastDirectoryKey != directoryKey) { filePage = 0; fileListKey = null; lastDirectoryKey = directoryKey; }
            int pages = Math.Max(1, (folders.Length + files.Length + 5) / 6); filePage = Mathf.Clamp(filePage, 0, pages - 1);
            Enabled("FilePrev", filePage > 0); Enabled("FileNext", filePage + 1 < pages); Label("FilesPageNumber", (filePage + 1) + " / " + pages);
            string key = directoryKey + "|" + filePage + "|" + exporting;
            if (fileListKey == key) return; fileListKey = key;
            foreach (var entry in fileEntries) { entry.SetActive(false); Destroy(entry); } fileEntries.Clear();
            int count = folders.Length + files.Length;
            for (int i = filePage * 6; i < Math.Min(count, filePage * 6 + 6); i++)
            {
                bool folder = i < folders.Length; string path = folder ? folders[i] : files[i - folders.Length];
                var entry = Instantiate(listEntryPrefab, W("FileEntries")); entry.name = Path.GetFileName(path);
                entry.GetComponentInChildren<TMP_Text>().text = (folder ? "文件夹 / " : "") + Path.GetFileName(path);
                entry.onClick.AddListener(() => { Play(buttonClip); if (folder) { directoryText = path; RefreshFiles(); } else if (exporting) filename = Path.GetFileName(path); else ImportFile(path); RefreshUI(); });
                fileEntries.Add(entry.gameObject);
            }
        }
        string lastDirectoryKey;
    }
}
