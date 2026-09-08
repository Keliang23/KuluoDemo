using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Kuluo.Sokoban;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class SokobanViewValidation
{
    const string Pending="Sokoban.UIValidation";
    static IEnumerator routine;
    static SokobanDemo app;
    static float musicBefore,effectsBefore;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("Tools/Sokoban/Build Windows Demo")]
    public static void BuildWindows()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before building.");
        Directory.CreateDirectory("Builds/Windows");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/SokobanDemo.unity"},
            locationPathName="Builds/Windows/Kuluo.exe",
            target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.None
        });
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Windows build failed: "+report.summary.result);
        Debug.Log("SOKOBAN_WINDOWS_BUILD_OK");
    }
    static SokobanViewValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
            {SessionState.SetBool(Pending,false); routine=Run(); EditorApplication.update+=Tick;}
            if(state==PlayModeStateChange.ExitingPlayMode) {EditorApplication.update-=Tick; routine=null;}
        };
    }
    [MenuItem("Tools/Sokoban/Validate Editable UI")]
    public static void Validate()
    {
        if(EditorApplication.isPlaying) {Debug.LogError("请先停止运行。"); return;}
        DemoLevels.Reload(); SokobanSetup.ValidateDemo();
        var controller=UnityEngine.Object.FindObjectOfType<SokobanDemo>();
        Require(controller!=null && controller.uiRoot!=null && controller.boardView!=null,"Serialized scene references");
        foreach (var t in controller.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
        {
            Require(t.name.All(c=>c<128), "English object name: "+t.name);
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0, "No missing scripts: "+t.name);
        }
        foreach (var text in controller.uiRoot.GetComponentsInChildren<TMP_Text>(true)) Require(!text.text.Contains("·"),"No decorative dot separators");
        Require(controller.uiRoot.GetComponentsInChildren<Button>(true).Length>40,"Authored uGUI buttons");
        Require(controller.uiRoot.GetComponentsInChildren<Slider>(true).Length==2,"Authored volume sliders");
        Require(Resources.LoadAll<SokobanLevel>("Levels").Length==7,"Seven editable level assets");
        Require(controller.boardView.floorMap!=null && controller.boardView.iceMap!=null,"Tilemaps assigned");
        foreach(var text in controller.uiRoot.GetComponentsInChildren<TMP_Text>(true)) Require(!text.raycastTarget,"Passive text does not capture pointer: "+text.name);
        foreach(var prefab in new[]{controller.boardView.playerPrefab,controller.boardView.boxPrefab,controller.boardView.platePrefab,controller.boardView.doorPrefab}) Require(PrefabUtility.IsPartOfPrefabAsset(prefab),"Piece is a saved prefab");
        var savedLayout = SokobanBoardInspector.ReadLayout(controller.boardView);
        Require(savedLayout.rows.SequenceEqual(controller.boardView.previewLevel.data.rows), "Scene-to-level roundtrip");
        SessionState.SetBool(Pending,true); EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        try { if(routine==null || !routine.MoveNext()) Finish(); }
        catch(Exception e) {Debug.LogException(e); Finish();}
    }
    static void Finish()
    {
        if(app!=null) {Set("musicVolume",musicBefore); Set("effectsVolume",effectsBefore);}
        routine=null; EditorApplication.update-=Tick; EditorApplication.isPlaying=false;
    }
    static object Get(string name)=>typeof(SokobanDemo).GetField(name,Private).GetValue(app);
    static void Set(string name,object value)=>typeof(SokobanDemo).GetField(name,Private).SetValue(app,value);
    static object Call(string name,params object[] args)=>typeof(SokobanDemo).GetMethod(name,Private).Invoke(app,args);
    static Transform Widget(string id)=>app.uiRoot.GetComponentsInChildren<Transform>(true).First(t=>t.name==id);
    static void Click(string id)
    {
        var button=Widget(id).GetComponent<Button>(); Require(button.gameObject.activeInHierarchy && button.IsInteractable(),"Button available: "+id); button.onClick.Invoke();
    }
    static BoardState Board=>(BoardState)Get("board");
    static void Require(bool ok,string message) {if(!ok) throw new Exception("UI validation: "+message);}
    static IEnumerator Run()
    {
        yield return null; yield return null;
        app=UnityEngine.Object.FindObjectOfType<SokobanDemo>(); Require(app!=null && app.enabled,"Controller initialized");
        musicBefore=(float)Get("musicVolume"); effectsBefore=(float)Get("effectsVolume");
        // Exercise persistence in a disposable directory, never the player's save files.
        string path=Path.Combine(Path.GetTempPath(),"KuluoUIValidation-"+Guid.NewGuid().ToString("N"));
        var storage=new SokobanStorage(path); Set("storage",storage); var progress=new CampaignProgress(); progress.Normalize(7); Set("progress",progress); Set("library",new LevelLibrary());
        Require(Widget("MenuPage").gameObject.activeInHierarchy,"Initial main menu");
        Click("MenuSettings"); yield return null;
        var music=Widget("MusicSlider").GetComponent<Slider>(); var fx=Widget("EffectsSlider").GetComponent<Slider>(); music.value=.23f; fx.value=.42f; yield return null;
        Require(Mathf.Approximately((float)Get("musicVolume"),.23f) && Mathf.Approximately((float)Get("effectsVolume"),.42f),"Slider binding");
        Require(Mathf.Approximately(((AudioSource)Get("music")).volume,.069f),"Live music volume");
        Canvas.ForceUpdateCanvases();
        Require(music.fillRect.rect.width <= ((RectTransform)music.transform).rect.width && music.handleRect.rect.height <= 32,"Slider visuals remain within track");
        Require(!Widget("Content").GetComponent<CanvasGroup>().interactable,"Modal blocks page");
        Click("SettingsDone"); Require(Mathf.Approximately(PlayerPrefs.GetFloat("Sokoban.MusicVolume"),.23f),"Volume persisted");
        Set("musicVolume",musicBefore); Set("effectsVolume",effectsBefore); Call("SaveAudioSettings");
        Click("Quit"); Require(Widget("ConfirmModal").gameObject.activeInHierarchy,"Quit confirmation"); Click("ConfirmNo");
        Click("StartGame"); yield return null;
        Require(Board.Moves==0 && (int)Get("levelIndex")==0,"Start always opens first level");
        Require(app.boardView.pieces.GetComponentsInChildren<SpriteRenderer>().Length>=3,"Visible prefab instances");
        Call("Move",Vector2Int.right); Call("RefreshUI"); yield return null;
        Require(Board.Pushes==1,"Push through controller"); Click("UndoMove"); Require(Board.Moves==0,"Undo through button");
        for(int i=0;i<DemoLevels.All.Length;i++)
        {
            if(i>0) Click("NextLevel"); yield return null;
            var solve=typeof(SokobanSetup).GetMethod("Solve",BindingFlags.Static|BindingFlags.NonPublic);
            string solution=(string)solve.Invoke(null,new object[]{new BoardState(DemoLevels.All[i])}); Require(solution!=null,"Level solvable");
            foreach(char c in solution)
            {
                var d=c=='L'?Vector2Int.left:c=='R'?Vector2Int.right:c=='U'?Vector2Int.down:Vector2Int.up;
                Call("Move",d); Call("RefreshUI");
            }
            yield return null; Require(Board.Won,"Level completed through UI controller: "+i);
            Require(app.boardView.pieces.GetComponentsInChildren<SokobanPieceView>().Count(p=>p.name.StartsWith("Box"))==Board.Boxes.Count,"Crate views match model");
        }
        Click("NextLevel"); Require(Widget("HomePage").gameObject.activeInHierarchy,"Final returns to level select");
        Click("HomeCreate"); yield return null;
        for(int i=0;i<8;i++) {Click("Brush"+i); Require((int)Get("tool")==i,"Editor brush "+i);}
        // Copy a combined level to exercise editing, rename, save, export, import and test return.
        Call("BeginDraft",DemoLevels.All[6],null); Call("RefreshUI"); yield return null;
        Widget("LevelName").GetComponent<TMP_InputField>().text="验证关卡"; Click("SaveMap");
        Require(((LevelLibrary)Get("library")).levels.Count==1 && ((LevelData)Get("draft")).title=="验证关卡","Named map saved");
        Click("Brush5"); Call("Paint",1,1,false); Call("RefreshUI"); Require(((LevelData)Get("draft")).TerrainAt(1,1)=='I',"Ice painting");
        Click("UndoEdit"); Require(((LevelData)Get("draft")).TerrainAt(1,1)==' ',"Editor undo");
        Click("TestMap"); yield return null; Require((bool)Get("testing"),"Test mode"); Click("ReturnToEdit"); Require(Widget("EditorPanel").gameObject.activeInHierarchy,"Return to original draft");
        Click("ExportMap"); yield return null; Click("FileExport"); Require(File.Exists(Path.Combine(storage.ExportDirectory,"level.json")),"Export JSON");
        Click("EditorBack"); Click("LibraryImport"); yield return null;
        var importButton=Widget("FileEntries").GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<TMP_Text>().text=="level.json"); importButton.onClick.Invoke(); yield return null;
        Require(((LevelData)Get("draft")).Validate()==null && (bool)Get("dirty"),"Import as new draft");
        Click("EditorBack"); Require(Widget("DiscardModal").gameObject.activeInHierarchy,"Unsaved guard"); Click("DiscardNo");
        Require(Widget("EditorPanel").gameObject.activeInHierarchy,"Cancel preserves draft");
        // Validate viewport mapping at a known cell center, including the vertical coordinate flip.
        var world=app.boardView.transform.TransformPoint(new Vector3(1.5f,-1.5f,0)); var uv=app.boardView.boardCamera.WorldToViewportPoint(world);
        Require(app.boardView.TryCell(uv,out var cell) && cell==new Vector2Int(1,1),"Pointer maps to correct tile");
        Call("BeginDraft", LevelData.Empty(8,6), null); Call("RefreshUI"); yield return null;
        Click("Brush5");
        var input = Widget("BoardViewport").GetComponent<SokobanBoardInput>(); var viewport=(RectTransform)input.transform;
        Vector2 ScreenCell(int x,int y)
        {
            var uvPoint=app.boardView.boardCamera.WorldToViewportPoint(app.boardView.transform.TransformPoint(new Vector3(x+.5f,-y-.5f,0)));
            var local=new Vector3(viewport.rect.xMin+uvPoint.x*viewport.rect.width,viewport.rect.yMin+uvPoint.y*viewport.rect.height,0);
            return RectTransformUtility.WorldToScreenPoint(null,viewport.TransformPoint(local));
        }
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=ScreenCell(1,1),pressPosition=ScreenCell(1,1)};
        input.OnPointerDown(pointer); input.OnBeginDrag(pointer); pointer.position=ScreenCell(5,1); input.OnDrag(pointer);
        for(int x=1;x<=5;x++) Require(((LevelData)Get("draft")).TerrainAt(x,1)=='I',"Fast drag paints every crossed cell");
        Debug.Log("SOKOBAN_UGUI_VALIDATION_OK · seven levels, sliders, modals, editor, save/import/export, viewport");
    }
}
