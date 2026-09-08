using System;
using System.IO;
using System.Linq;
using Kuluo.Sokoban;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class SokobanMigration
{
    const string Root = "Assets/Sokoban";
    static TMP_FontAsset font;
    static GameObject buttonPrefab;
    static Color Bg => ColorOf("101C29");
    static Color Panel => ColorOf("192A3B");
    static Color Ink => ColorOf("EAF2F5");
    static Color Muted => ColorOf("96ABBD");
    static Color Mint => ColorOf("69DFC0");
    static Color ColorOf(string s) { ColorUtility.TryParseHtmlString("#" + s, out var c); return c; }

    [MenuItem("Tools/Sokoban/Migrate to Editable Scene")]
    public static void Migrate()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("请先停止运行，再迁移场景。"); return; }
        if (File.Exists(Root + "/UI/MainMenu.prefab")) { Debug.Log("场景已迁移；请直接编辑现有对象和预制体。"); return; }
        Directory.CreateDirectory(Root + "/UI"); Directory.CreateDirectory(Root + "/Tiles"); Directory.CreateDirectory(Root + "/Pieces"); Directory.CreateDirectory(Root + "/Sprites"); Directory.CreateDirectory("Assets/Resources/Levels");
        if (Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
        {
            var package = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).First();
            AssetDatabase.importPackageCompleted += OnPackageImported;
            AssetDatabase.ImportPackage(package, false);
        }
        else Build();
    }
    static void OnPackageImported(string name) { AssetDatabase.importPackageCompleted -= OnPackageImported; EditorApplication.delayCall += Build; }
    public static void Build()
    {
        AssetDatabase.Refresh();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/UI/Chinese.asset");
        if (font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansCJKsc-Regular.otf");
            font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = "Chinese";
            AssetDatabase.CreateAsset(font, Root + "/UI/Chinese.asset");
            font.atlasTextures[0].name = "Chinese Atlas"; font.material.name = "Chinese Material";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font); AssetDatabase.AddObjectToAsset(font.material, font);
            string chars = string.Concat(Directory.GetFiles("Assets/Scripts", "*.cs").Select(File.ReadAllText));
            font.TryAddCharacters(new string(chars.Distinct().ToArray()), out string missing);
            EditorUtility.SetDirty(font);
        }
        for (int i = 0; i < DemoLevels.Seeds.Length; i++)
        {
            string path = "Assets/Resources/Levels/Level" + (i+1).ToString("00") + ".asset";
            if (AssetDatabase.LoadAssetAtPath<SokobanLevel>(path) != null) continue;
            var level = ScriptableObject.CreateInstance<SokobanLevel>(); level.order = i; level.lesson = DemoLevels.SeedLessons[i]; level.data = DemoLevels.Seeds[i].Copy();
            AssetDatabase.CreateAsset(level, path);
        }
        DemoLevels.Reload();
        MakeArt();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("MainCamera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.orthographic = true; camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Bg; camera.transform.position = new Vector3(0,0,-10); camera.gameObject.AddComponent<AudioListener>();
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var app = new GameObject("Game").AddComponent<SokobanDemo>();
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280,800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var root = Node(canvas.transform, "UIRoot", 0,0,1280,800); root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f,.5f); root.anchoredPosition = Vector2.zero;
        app.uiRoot = root;
        // The authored 1280 x 800 layout stays centered at every window aspect ratio.
        var content = Node(root,"Content",0,0,1280,800); content.gameObject.AddComponent<CanvasGroup>();
        Image(content,"Accent",32,35,5,42,Mint); Text(content,"GameTitle",52,30,580,44,"推箱子",30,Ink,true);
        Button(content,"HeaderBack",750,38,180,38,"← 主菜单"); Button(content,"HeaderSettings",1032,38,100,38,"设置"); Button(content,"HeaderPause",1144,38,100,38,"暂停 Esc");
        buttonPrefab = PrefabUtility.SaveAsPrefabAsset(MakeButton(null,"Button",0,0,240,44,"按钮",false).gameObject, Root + "/UI/Button.prefab");
        UnityEngine.Object.DestroyImmediate(GameObject.Find("Button"));
        var entry = MakeButton(null,"ListEntry",0,0,874,36,"文件",false); var layout = entry.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 36; layout.minHeight = 36;
        app.listEntryPrefab = PrefabUtility.SaveAsPrefabAsset(entry.gameObject, Root + "/UI/ListEntry.prefab").GetComponent<Button>(); UnityEngine.Object.DestroyImmediate(entry.gameObject);
        BuildMenu(content); BuildHome(content); BuildLibrary(content);
        var boardPage = Node(content,"BoardPage",0,0,1280,800);
        Text(boardPage,"BoardHeading",38,119,758,36,"第 1 / 7 关",21,Mint,true);
        Image(boardPage,"BoardBackground",36,174,770,556,Panel);
        var viewport = Node(boardPage,"BoardViewport",58,195,726,510); var raw = viewport.gameObject.AddComponent<RawImage>(); raw.raycastTarget = true; viewport.gameObject.AddComponent<SokobanBoardInput>();
        app.boardView = BuildBoard(); raw.texture = app.boardView.boardCamera.targetTexture;
        Text(boardPage,"BoardHelp",40,749,1150,28,"WASD / 方向键 移动  Z 撤销  R 重开  Esc 暂停",15,Muted);
        BuildPlay(boardPage); BuildEditor(boardPage); BuildModals(root);
        var notice = Image(root,"Notice",40,684,758,44,ColorOf("284B51")); Text(notice,"NoticeText",13,6,730,36,"",14,Ink); notice.gameObject.SetActive(false);
        foreach (var id in new[] {"HomePage","LibraryPage","BoardPage","SettingsModal","PauseModal","FilesModal","ConfirmModal","DiscardModal","HeaderBack","HeaderSettings","HeaderPause"}) Find(root,id).gameObject.SetActive(false);
        app.boardView.Preview();
        PrefabUtility.SaveAsPrefabAssetAndConnect(Find(root,"MenuPage").gameObject, Root + "/UI/MainMenu.prefab", InteractionMode.AutomatedAction);
        foreach (string id in new[] {"HomePage","LibraryPage","PlayPanel","EditorPanel","SettingsModal","PauseModal","FilesModal","ConfirmModal","DiscardModal"})
            PrefabUtility.SaveAsPrefabAssetAndConnect(Find(root,id).gameObject, Root + "/UI/" + id + ".prefab", InteractionMode.AutomatedAction);
        // Keep the main menu connected too; its layout is visible without entering Play mode.

        EditorSceneManager.SaveScene(scene,"Assets/Scenes/SokobanDemo.unity");
        EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene("Assets/Scenes/SokobanDemo.unity",true)};
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = canvas;
        Debug.Log("SOKOBAN_UGUI_MIGRATION_OK");
    }
    static Transform Find(Transform parent,string name) => parent.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
    static RectTransform Node(Transform parent,string name,float x,float y,float w,float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0,1); r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h); return r;
    }
    static RectTransform Image(Transform p,string name,float x,float y,float w,float h,Color color,bool hit=false)
    {
        var r=Node(p,name,x,y,w,h); var image=r.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=hit; return r;
    }
    static TextMeshProUGUI Text(Transform p,string name,float x,float y,float w,float h,string value,int size,Color color,bool bold=false)
    {
        var r=Node(p,name,x,y,w,h); var text=r.gameObject.AddComponent<TextMeshProUGUI>(); text.font=font; text.text=value; text.fontSize=size; text.color=color; text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;
        text.raycastTarget=false; text.enableWordWrapping=true; text.overflowMode=TextOverflowModes.Ellipsis; if(h < size*1.55f) r.sizeDelta=new Vector2(w,Mathf.Ceil(size*1.55f)); text.margin=Vector4.zero; return text;
    }
    static Button MakeButton(Transform p,string name,float x,float y,float w,float h,string value,bool accent)
    {
        var r=Image(p,name,x,y,w,h,Color.white,true); var button=r.gameObject.AddComponent<Button>(); button.targetGraphic=r.GetComponent<Image>();
        var colors=button.colors; colors.normalColor=accent?Mint:ColorOf("2B4255"); colors.highlightedColor=Color.Lerp(colors.normalColor,Color.white,.2f); colors.pressedColor=Color.Lerp(colors.normalColor,Color.black,.15f); colors.selectedColor=colors.normalColor; colors.disabledColor=ColorOf("223445"); colors.fadeDuration=.1f; button.colors=colors;
        // Arrow keys remain gameplay input; Tab traversal is handled by TMP fields.
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        var label=Text(r,name+"Label",8,4,w-16,h-8,value,17,accent?Bg:Ink,accent); label.alignment=TextAlignmentOptions.Center; label.enableAutoSizing=true; label.fontSizeMin=14; label.fontSizeMax=17; label.enableWordWrapping=false;
        var lr=label.rectTransform; lr.anchorMin=Vector2.zero; lr.anchorMax=Vector2.one; lr.offsetMin=new Vector2(8,4); lr.offsetMax=new Vector2(-8,-4);
        r.gameObject.AddComponent<SokobanButtonStyle>(); return button;
    }
    static Button Button(Transform p,string name,float x,float y,float w,float h,string value,bool accent=false)
    {
        if (buttonPrefab==null) return MakeButton(p,name,x,y,w,h,value,accent);
        var g=(GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab,p); g.name=name; var r=(RectTransform)g.transform; r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h);
        g.GetComponentInChildren<TMP_Text>().text=value; g.GetComponent<SokobanButtonStyle>().SetSelected(accent); return g.GetComponent<Button>();
    }
    static void Input(Transform p,string name,float x,float y,float w,float h,int limit)
    {
        var r=Image(p,name,x,y,w,h,ColorOf("263E50"),true); var f=r.gameObject.AddComponent<TMP_InputField>();
        var viewport=Node(r,name+"Viewport",10,4,w-20,h-8); viewport.gameObject.AddComponent<RectMask2D>();
        var text=Text(viewport,name+"Text",0,0,w-20,h-8,"",18,Ink); text.alignment=TextAlignmentOptions.MidlineLeft;
        f.textViewport=viewport; f.textComponent=text; f.targetGraphic=r.GetComponent<Image>(); f.characterLimit=limit; f.caretColor=Mint; f.customCaretColor=true; f.selectionColor=new Color(.41f,.87f,.75f,.3f);
        var colors=f.colors; colors.highlightedColor=colors.normalColor; f.colors=colors;
    }
    static void Slider(Transform p,string name,float x,float y,float w)
    {
        var r=Node(p,name,x,y,w,32); var slider=r.gameObject.AddComponent<Slider>();
        Image(r,name+"Track",0,12,w,8,ColorOf("33485A"),true);
        var fillArea=Node(r,name+"FillArea",10,12,w-20,8); var fill=Image(fillArea,name+"Fill",0,0,w-20,8,Mint); fill.sizeDelta=Vector2.zero; slider.fillRect=fill;
        var handleArea=Node(r,name+"HandleArea",10,0,w-20,32); var handle=Image(handleArea,name+"Handle",0,0,20,28,Mint,true);
        handle.pivot=new Vector2(.5f,.5f); handle.sizeDelta=new Vector2(20,-4); handle.anchoredPosition=Vector2.zero; slider.handleRect=handle; slider.targetGraphic=handle.GetComponent<Image>(); slider.minValue=0; slider.maxValue=1; slider.value=.5f;
        slider.navigation=new Navigation{mode=Navigation.Mode.Automatic}; r.gameObject.AddComponent<SokobanSliderFeedback>();
    }
    static RectTransform Page(Transform parent,string name) => Node(parent,name,0,0,1280,800);
    static RectTransform Modal(Transform parent,string name,float x,float y,float w,float h)
    {
        var r=Image(parent,name,0,0,1280,800,new Color(.025f,.06f,.1f,.94f),true); r.gameObject.AddComponent<CanvasGroup>(); Image(r,name+"Panel",x,y,w,h,Panel,true); return r;
    }
    static void BuildMenu(Transform content)
    {
        var p=Page(content,"MenuPage"); Text(p,"MenuTitle",128,170,580,90,"推箱子",52,Ink,true); Text(p,"MenuTagline",128,251,580,40,"每一次推动，都多想一步。",25,Muted);
        Preview(p,"MenuPreview",128,326,440,330,DemoLevels.All[0]);
        string[] ids={"StartGame","SelectLevels","EditMaps","MenuSettings","Quit"}; string[] labels={"开始游戏","选择关卡","编辑地图","设置","退出游戏"};
        for(int i=0;i<5;i++) Button(p,ids[i],712,326+i*70,440,50,labels[i],i==0);
    }
    static void BuildHome(Transform content)
    {
        var p=Page(content,"HomePage"); Text(p,"HomeTitle",38,128,760,52,"从第一步，到多想一步。",32,Ink,true); Text(p,"CampaignProgress",40,190,770,32,"",18,Muted); Button(p,"ResumeCampaign",960,135,284,60,"开始游戏",true);
        for(int i=0;i<DemoLevels.All.Length;i++)
        {
            float x=36+(i%4)*304,y=250+(i/4)*213; var card=Image(p,"LevelCard"+i,x,y,290,197,Panel);
            Text(card,"LevelLesson"+i,16,14,262,28,(i+1).ToString("00")+"  "+DemoLevels.Lessons[i],17,Mint,true);
            Preview(card,"LevelPreview"+i,12,56,110,110,DemoLevels.All[i]); Text(card,"LevelTitle"+i,136,54,142,36,DemoLevels.All[i].title,21,Ink,true);
            Text(card,"LevelStatus"+i,136,96,142,40,"",14,Muted); Button(card,"Level"+i,136,144,142,36,"进入",true);
        }
        Text(p,"HomeFooter",40,709,600,32,"也可以自己设计一个谜题。",18,Muted); Button(p,"HomeLibrary",848,696,190,48,"我的关卡"); Button(p,"HomeCreate",1054,696,190,48,"新建关卡");
    }
    static void BuildLibrary(Transform content)
    {
        var p=Page(content,"LibraryPage"); Text(p,"LibraryHeading",38,120,420,36,"我的关卡",25,Ink,true); Button(p,"LibraryImport",832,115,192,44,"导入关卡文件"); Button(p,"LibraryCreate",1040,115,204,44,"新建关卡",true);
        Image(p,"LibraryListBackground",36,181,770,529,Panel); Text(p,"LibraryEmpty",72,232,680,110,"还没有自己的关卡。\n点击「新建关卡」，或者导入一个关卡文件。",23,Muted);
        for(int n=0;n<6;n++) Button(p,"LibraryRow"+n,55,198+n*77,732,64,"关卡");
        Button(p,"LibraryPrev",36,728,120,36,"上一页"); Text(p,"LibraryPageNumber",170,736,120,25,"1 / 1",16,Muted); Button(p,"LibraryNext",310,728,120,36,"下一页");
        Image(p,"LibraryDetails",830,181,414,529,Panel); Text(p,"SelectedTitle",858,214,352,95,"选择一个关卡",26,Ink,true); Text(p,"SelectedHint",858,320,352,86,"",17,Muted);
        Button(p,"LibraryEdit",858,425,352,48,"编辑 / 重命名",true); Button(p,"LibraryTest",858,490,352,42,"试玩"); Button(p,"LibraryExport",858,548,352,42,"导出 JSON"); Button(p,"LibraryDelete",858,629,352,42,"删除关卡");
    }
    static void BuildPlay(Transform parent)
    {
        var p=Page(parent,"PlayPanel"); Image(p,"PlayBackground",830,110,414,620,Panel);
        Text(p,"PlayLesson",858,139,350,24,"基础移动",15,Mint,true); Text(p,"PlayTitle",856,181,357,62,"第一步",29,Ink,true); Text(p,"PlayHint",858,253,350,102,"",18,Ink);
        Image(p,"PlayDivider",858,374,352,1,ColorOf("33485A")); Text(p,"MovesLabel",858,397,165,26,"步数",16,Muted); Text(p,"DockedLabel",1040,397,170,26,"箱子到位",16,Muted);
        Text(p,"MoveCount",858,430,165,60,"00",34,Ink,true); Text(p,"DockCount",1040,430,170,60,"0 / 1",34,Mint,true);
        Button(p,"UndoMove",858,496,170,45,"撤销 Z"); Button(p,"Restart",1040,496,170,45,"重开 R"); Text(p,"PlayTip",858,563,350,64,"",20,Muted);
        Button(p,"NextLevel",858,644,352,51,"下一关 →",true); Button(p,"ReturnToEdit",858,644,352,44,"继续编辑",true);
    }
    static void BuildEditor(Transform parent)
    {
        var p=Page(parent,"EditorPanel"); Image(p,"EditorBackground",830,110,414,620,Panel); Text(p,"NameLabel",858,130,352,28,"关卡名称",16,Muted); Input(p,"LevelName",858,166,352,40,40);
        Text(p,"SizeLabel",858,222,352,23,"宽度 3–16 / 高度 3–14",15,Muted);
        Button(p,"WidthMinus",858,256,39,33,"−"); Text(p,"MapWidth",905,258,70,30,"8",20,Ink,true).alignment=TextAlignmentOptions.Center; Button(p,"WidthPlus",988,256,40,33,"+");
        Button(p,"HeightMinus",1040,256,39,33,"−"); Text(p,"MapHeight",1085,258,70,30,"6",20,Ink,true).alignment=TextAlignmentOptions.Center; Button(p,"HeightPlus",1170,256,40,33,"+"); Button(p,"ResizeMap",858,300,352,34,"应用尺寸");
        string[] brushes={"擦除","墙壁","目标点","箱子","玩家","冰面","压力板","门"}; for(int i=0;i<8;i++) Button(p,"Brush"+i,858+i%2*182,347+i/2*39,170,33,brushes[i]);
        Button(p,"TestMap",858,512,170,36,"试玩 →",true); Button(p,"UndoEdit",1040,512,170,36,"撤销编辑"); Button(p,"SaveMap",858,557,170,39,"保存关卡"); Button(p,"ExportMap",1040,557,170,39,"导出文件");
        Text(p,"EditorHint",858,610,352,45,"先画地形，再放箱子或目标点。压力板控制地图上的所有门。",14,Muted); Button(p,"EditorBack",858,675,352,32,"返回我的关卡");
    }
    static void BuildModals(Transform root)
    {
        var pause=Modal(root,"PauseModal",410,180,460,440); Text(pause,"PauseTitle",450,221,380,48,"已暂停",34,Ink,true); Button(pause,"PauseContinue",450,322,380,52,"继续 / Esc",true); Button(pause,"PauseSelect",450,396,380,47,"选择关卡"); Button(pause,"PauseCopy",450,464,380,47,"用这张地图创作"); Button(pause,"PauseHome",450,540,380,43,"主菜单");
        var settings=Modal(root,"SettingsModal",330,218,620,374); Text(settings,"SettingsTitle",372,250,536,45,"设置",30,Ink,true);
        Text(settings,"MusicLabel",372,319,180,28,"音乐音量",18,Ink); Slider(settings,"MusicSlider",372,354,438); Text(settings,"MusicPercent",830,355,78,32,"40%",20,Mint);
        Text(settings,"EffectsLabel",372,411,180,28,"音效音量",18,Ink); Slider(settings,"EffectsSlider",372,446,438); Text(settings,"EffectsPercent",830,447,78,32,"65%",20,Mint); Button(settings,"SettingsDone",372,519,536,43,"完成",true);
        var files=Modal(root,"FilesModal",170,102,940,588); Text(files,"FilesTitle",200,124,780,40,"导入关卡文件",26,Ink,true); Input(files,"Directory",200,183,648,38,1024); Button(files,"OpenDirectory",862,183,99,38,"打开路径"); Button(files,"ParentDirectory",975,183,99,38,"上一级");
        var entries=Node(files,"FileEntries",200,238,874,246); var layout=entries.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=5; layout.childControlHeight=true; layout.childControlWidth=true; layout.childForceExpandHeight=false;
        Button(files,"FilePrev",200,491,100,32,"上一页"); Text(files,"FilesPageNumber",320,494,120,28,"1 / 1",16,Muted); Button(files,"FileNext",456,491,100,32,"下一页");
        Text(files,"FilesHint",200,530,875,28,"",15,Muted); Input(files,"Filename",200,568,620,41,180); Button(files,"FileExport",835,568,239,41,"导出",true); Button(files,"FileCancel",834,628,240,38,"取消");
        var confirm=Modal(root,"ConfirmModal",330,260,620,300); Text(confirm,"ConfirmMessage",366,296,548,100,"确定退出游戏吗？",23,Ink,true); Button(confirm,"ConfirmYes",366,454,261,47,"确认",true); Button(confirm,"ConfirmNo",646,454,264,47,"取消");
        var discard=Modal(root,"DiscardModal",330,260,620,300); Text(discard,"DiscardMessage",366,296,548,100,"当前关卡有未保存的修改。",23,Ink,true); Button(discard,"DiscardSave",366,454,170,47,"保存并离开",true); Button(discard,"DiscardYes",553,454,170,47,"放弃修改"); Button(discard,"DiscardNo",740,454,170,47,"取消");
    }
    static Sprite SpriteAsset(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Sprites/"+name+".png");
    static void MakeArt()
    {
        foreach(string kind in new[]{"Floor","Wall","Ice","Player","Box","Goal","Plate","Door","Bars"})
        {
            string path=Root+"/Sprites/"+kind+".png";
            if(File.Exists(path)) continue;
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                Color color=Color.clear;
                if(kind=="Floor" && x>=2 && y>=2 && x<62 && y<62) color=ColorOf("263E50");
                if(kind=="Wall" && x>=3 && x<61 && y>=3 && y<61) color=y<9?ColorOf("102130"):ColorOf("496174");
                if(kind=="Wall" && x>=7 && x<57 && y>=55 && y<58) color=ColorOf("658092");
                if(kind=="Ice" && x>=3 && x<61 && y>=3 && y<61) color=ColorOf("376D89");
                if(kind=="Ice" && ((x>10&&x<44&&y>46&&y<50)||(x>22&&x<54&&y>18&&y<21))) color=ColorOf("9ADCF4");
                if(kind=="Player" && Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))<21) color=ColorOf("83CBF1");
                if(kind=="Player" && ((x>=27&&x<=30&&y>=22&&y<=41)||(x>=30&&x<=37&&(y>=38&&y<=41||y>=30&&y<=33))||(x>=35&&x<=38&&y>=32&&y<=39))) color=ColorOf("101C29");
                if(kind=="Box" && x>=10&&x<54&&y>=10&&y<54) color=new Color(.6f,.6f,.6f);
                if(kind=="Box" && x>=14&&x<50&&y>=14&&y<50) color=Color.white;
                if(kind=="Box" && ((x>=28&&x<36&&y>=14&&y<50)||(y>=28&&y<34&&x>=14&&x<50))) color=new Color(.6f,.6f,.6f);
                if(kind=="Box" && x>=28&&x<36&&y>=28&&y<36) color=Color.white;
                if(kind=="Goal" && x>=14&&x<50&&y>=14&&y<50) color=ColorOf("365F5D");
                if(kind=="Goal" && x>=19&&x<45&&y>=19&&y<45) color=ColorOf("69DFC0");
                if(kind=="Goal" && x>=23&&x<41&&y>=23&&y<41) color=ColorOf("233A4B");
                if(kind=="Goal" && x>=29&&x<35&&y>=29&&y<35) color=ColorOf("69DFC0");
                if(kind=="Plate" && x>=6&&x<58&&y>=6&&y<58) color=Color.white;
                if(kind=="Plate" && x>=13&&x<51&&y>=13&&y<51) color=new Color(.4f,.4f,.4f);
                if(kind=="Door" && ((x>=5&&x<11)||(x>=53&&x<59))&&y>=5&&y<59) color=ColorOf("C7A0E8");
                if(kind=="Bars" && x>=12&&x<52&&((y>=12&&y<18)||(y>=28&&y<34)||(y>=44&&y<50))) color=ColorOf("C7A0E8");
                texture.SetPixel(x,y,color);
            }
            texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path); var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=64; importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.mipmapEnabled=false; importer.SaveAndReimport();
        }
        foreach(string kind in new[]{"Floor","Wall","Ice"})
        {
            string path=Root+"/Tiles/"+kind+".asset"; if(AssetDatabase.LoadAssetAtPath<Tile>(path)!=null) continue;
            var tile=ScriptableObject.CreateInstance<Tile>(); tile.sprite=SpriteAsset(kind); tile.colliderType=Tile.ColliderType.None; AssetDatabase.CreateAsset(tile,path);
        }
        foreach(string kind in new[]{"Player","Box","Goal","Plate","Door"})
        {
            string path=Root+"/Pieces/"+kind+".prefab"; if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null) continue;
            var go=new GameObject(kind); go.layer=30; var renderer=go.AddComponent<SpriteRenderer>(); renderer.sprite=SpriteAsset(kind); renderer.sortingOrder=kind=="Player"?6:kind=="Box"?5:kind=="Goal"?3:2;
            if(kind=="Box") renderer.color=ColorOf("F2BD69"); if(kind=="Plate") renderer.color=ColorOf("A680C4");
            var piece=go.AddComponent<SokobanPieceView>(); piece.body=renderer; if(kind=="Plate") piece.activeColor=ColorOf("D4ACF2");
            if(kind=="Door") { var bars=new GameObject("ClosedBars"); bars.layer=30; bars.transform.SetParent(go.transform,false); var sr=bars.AddComponent<SpriteRenderer>(); sr.sprite=SpriteAsset("Bars"); sr.sortingOrder=2; piece.closedBars=bars; }
            PrefabUtility.SaveAsPrefabAsset(go,path); UnityEngine.Object.DestroyImmediate(go);
        }
    }
    static SokobanBoardView BuildBoard()
    {
        var go=new GameObject("Board"); go.layer=30; var view=go.AddComponent<SokobanBoardView>();
        var grid=new GameObject("Grid",typeof(Grid)); grid.layer=30; grid.transform.SetParent(go.transform,false);
        Tilemap MakeMap(string name,int order)
        {
            var child=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer)); child.layer=30; child.transform.SetParent(grid.transform,false); child.GetComponent<TilemapRenderer>().sortingOrder=order; return child.GetComponent<Tilemap>();
        }
        view.floorMap=MakeMap("Floor",0); view.wallMap=MakeMap("Walls",1); view.iceMap=MakeMap("Ice",1);
        view.floorTile=AssetDatabase.LoadAssetAtPath<Tile>(Root+"/Tiles/Floor.asset"); view.wallTile=AssetDatabase.LoadAssetAtPath<Tile>(Root+"/Tiles/Wall.asset"); view.iceTile=AssetDatabase.LoadAssetAtPath<Tile>(Root+"/Tiles/Ice.asset");
        view.playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Pieces/Player.prefab"); view.boxPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Pieces/Box.prefab"); view.goalPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Pieces/Goal.prefab"); view.platePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Pieces/Plate.prefab"); view.doorPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Pieces/Door.prefab");
        view.pieces=new GameObject("Pieces").transform; view.pieces.SetParent(go.transform,false);
        var camera=new GameObject("BoardCamera").AddComponent<Camera>(); camera.transform.SetParent(go.transform,false); camera.orthographic=true; camera.cullingMask=1<<30; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Panel; camera.allowHDR=false; camera.allowMSAA=false;
        var rt=new RenderTexture(1452,1020,16,RenderTextureFormat.ARGB32); rt.name="BoardView"; AssetDatabase.CreateAsset(rt,Root+"/UI/BoardView.renderTexture"); camera.targetTexture=rt; view.boardCamera=camera;
        view.previewLevel=AssetDatabase.LoadAssetAtPath<SokobanLevel>("Assets/Resources/Levels/Level01.asset"); return view;
    }
    static void Preview(Transform parent,string name,float x,float y,float w,float h,LevelData data)
    {
        var root=Node(parent,name,x,y,w,h); float cell=Mathf.Min(w/data.rows[0].Length,h/data.rows.Length); float left=(w-cell*data.rows[0].Length)/2, top=(h-cell*data.rows.Length)/2;
        void Part(string kind,int cx,int cy,Color? tint=null)
        {
            var r=Image(root,kind+"_"+cx+"_"+cy,left+cx*cell,top+cy*cell,cell,cell,tint??Color.white); r.GetComponent<Image>().sprite=SpriteAsset(kind);
        }
        for(int yy=0;yy<data.rows.Length;yy++) for(int xx=0;xx<data.rows[yy].Length;xx++)
        {
            char c=data.rows[yy][xx]; Part(c=='#'?"Wall":"Floor",xx,yy); if(c=='#') continue;
            char terrain=data.TerrainAt(xx,yy); if(terrain=='I') Part("Ice",xx,yy); if(terrain=='S') Part("Plate",xx,yy,ColorOf("A680C4")); if(terrain=='D') { Part("Door",xx,yy); Part("Bars",xx,yy); }
            if(c=='.'||c=='*'||c=='+') Part("Goal",xx,yy); if(c=='$'||c=='*') Part("Box",xx,yy,c=='*'?Mint:ColorOf("F2BD69")); if(c=='@'||c=='+') Part("Player",xx,yy);
        }
        var preview=root.gameObject.AddComponent<SokobanMapPreview>();
        int index=Array.IndexOf(DemoLevels.All,data);
        preview.level=AssetDatabase.LoadAssetAtPath<SokobanLevel>("Assets/Resources/Levels/Level"+(index+1).ToString("00")+".asset");
        preview.sprites=new[]{"Floor","Wall","Ice","Player","Box","Goal","Plate","Door","Bars"}.Select(SpriteAsset).ToArray(); preview.RecordLayout();
    }
}

