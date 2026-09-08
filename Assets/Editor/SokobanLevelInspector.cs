using System;
using Kuluo.Sokoban;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(SokobanLevel))]
public sealed class SokobanLevelInspector : Editor
{
    int brush, width, height;
    bool raw;
    static readonly string[] Brushes = {"Floor","Wall","Goal","Box","Player","Ice","Plate","Door"};
    void OnEnable() { var l=(SokobanLevel)target; if(l.data?.rows!=null) {width=l.data.rows[0].Length; height=l.data.rows.Length;} }
    public override void OnInspectorGUI()
    {
        var level=(SokobanLevel)target;
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("order"),new GUIContent("Order")); EditorGUILayout.PropertyField(serializedObject.FindProperty("lesson"),new GUIContent("Lesson"));
        var data=serializedObject.FindProperty("data"); EditorGUILayout.PropertyField(data.FindPropertyRelative("title"),new GUIContent("Title")); EditorGUILayout.PropertyField(data.FindPropertyRelative("hint"),new GUIContent("Hint")); serializedObject.ApplyModifiedProperties();
        if(level.data==null || level.data.rows==null) { if(GUILayout.Button("Create Empty Map")) {Undo.RecordObject(level,"Create Level"); level.data=LevelData.Empty(8,6); EditorUtility.SetDirty(level); OnEnable();} return; }
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Map Painter",EditorStyles.boldLabel); brush=GUILayout.SelectionGrid(brush,Brushes,4);
        EditorGUILayout.HelpBox("Click cells to paint terrain, then add goals and pieces. Floor clears a cell. Changes are saved in this level asset.",MessageType.Info);
        float size=Mathf.Min(30,(EditorGUIUtility.currentViewWidth-40)/level.data.rows[0].Length);
        for(int y=0;y<level.data.rows.Length;y++)
        {
            EditorGUILayout.BeginHorizontal();
            for(int x=0;x<level.data.rows[y].Length;x++)
            {
                char c=level.data.rows[y][x], t=level.data.TerrainAt(x,y);
                string label=c=='#'?"■":c=='@'||c=='+'?"P":c=='$'||c=='*'?"B":c=='.'?"◎":t=='I'?"I":t=='S'?"S":t=='D'?"D":"";
                var previous=GUI.backgroundColor; GUI.backgroundColor=c=='#'?new Color(.5f,.6f,.7f):t=='I'?Color.cyan:t=='S'||t=='D'?new Color(.8f,.6f,1):c=='.'||c=='*'||c=='+'?Color.green:Color.white;
                if(GUILayout.Button(label,GUILayout.Width(size),GUILayout.Height(size))) Paint(level,x,y);
                GUI.backgroundColor=previous;
            }
            EditorGUILayout.EndHorizontal();
        }
        width=EditorGUILayout.IntSlider("Width",width,3,16); height=EditorGUILayout.IntSlider("Height",height,3,14);
        if(GUILayout.Button("Apply Size"))
        {
            Undo.RecordObject(level,"Resize Level"); level.data=level.data.Resized(width,height); EditorUtility.SetDirty(level);
        }
        string error=level.data.Validate(); EditorGUILayout.HelpBox(error??"Layout valid. Playtest to check solvability.",error==null?MessageType.Info:MessageType.Warning);
        if(GUILayout.Button("Preview in Scene"))
        {
            var board=UnityEngine.Object.FindObjectOfType<SokobanBoardView>(true);
            if(board!=null && !Application.isPlaying) {Undo.RecordObject(board,"Preview Level"); board.previewLevel=level; board.gameObject.SetActive(true); board.Preview(); EditorSceneManager.MarkSceneDirty(board.gameObject.scene); Selection.activeGameObject=board.gameObject; SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(level.data.rows[0].Length/2f,-level.data.rows.Length/2f,0),new Vector3(level.data.rows[0].Length+2,level.data.rows.Length+2,1)),false);}
        }
        raw=EditorGUILayout.Foldout(raw,"Raw Map Data"); if(raw) {serializedObject.Update(); EditorGUILayout.PropertyField(data.FindPropertyRelative("rows")); EditorGUILayout.PropertyField(data.FindPropertyRelative("terrain")); serializedObject.ApplyModifiedProperties();}
    }
    void Paint(SokobanLevel level,int x,int y)
    {
        Undo.RecordObject(level,"Paint Level"); var d=level.data; char c=d.rows[y][x]; bool goal=c=='.'||c=='*'||c=='+'; char t=d.TerrainAt(x,y); char next;
        if(brush<=1) {next=brush==0?' ':'#'; t=' ';}
        else if(brush>=5) {next=c=='#'?' ':c; t=brush==5?'I':brush==6?'S':'D';}
        else next=brush==2?(c=='$'||c=='*'?'*':c=='@'||c=='+'?'+':'.'):brush==3?(goal?'*':'$'):(goal?'+':'@');
        if(brush==4) for(int row=0;row<d.rows.Length;row++) d.rows[row]=d.rows[row].Replace('@',' ').Replace('+','.');
        var chars=d.rows[y].ToCharArray(); chars[x]=next; d.rows[y]=new string(chars); d.SetTerrain(x,y,t); EditorUtility.SetDirty(level); DemoLevels.Reload();
    }
}
