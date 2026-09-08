using System;
using Kuluo.Sokoban;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(SokobanBoardView))]
public sealed class SokobanBoardInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var board=(SokobanBoardView)target;
        EditorGUILayout.HelpBox("Edit Tilemaps and move or duplicate prefabs under Pieces in Edit mode. Save back to the preview level when finished. Set map dimensions in the level asset. Edit prefabs or Tiles to change appearance.",MessageType.Info);
        using(new EditorGUI.DisabledScope(Application.isPlaying || board.previewLevel==null))
        {
            if(GUILayout.Button("Reload From Level")) {board.Preview(); EditorSceneManager.MarkSceneDirty(board.gameObject.scene);}
            if(GUILayout.Button("Save Scene Layout To Level"))
            {
                try
                {
                    var data=ReadLayout(board);
                    Undo.RecordObject(board.previewLevel,"Save Scene Layout"); board.previewLevel.data=data; EditorUtility.SetDirty(board.previewLevel); AssetDatabase.SaveAssets(); DemoLevels.Reload();
                    Debug.Log("Level saved: "+data.title);
                }
                catch(Exception e) {Debug.LogError(e.Message);}
            }
        }
    }
    public static LevelData ReadLayout(SokobanBoardView board)
    {
        var data=board.previewLevel.data.Copy(); int w=data.rows[0].Length,h=data.rows.Length;
        var chars=new char[h][]; data.terrain=null;
        for(int y=0;y<h;y++)
        {
            chars[y]=new string(' ',w).ToCharArray();
            for(int x=0;x<w;x++)
            {
                var at=new Vector3Int(x,-y-1,0);
                if(board.wallMap.HasTile(at)) chars[y][x]='#';
                else if(board.iceMap.HasTile(at)) data.SetTerrain(x,y,'I');
            }
        }
        foreach(Transform piece in board.pieces)
        {
            if(!piece.gameObject.activeSelf) continue;
            var source=PrefabUtility.GetCorrespondingObjectFromSource(piece.gameObject);
            var pos=piece.localPosition; int x=Mathf.RoundToInt(pos.x-.5f),y=Mathf.RoundToInt(-pos.y-.5f);
            if(x<0||y<0||x>=w||y>=h) throw new Exception(piece.name+" is outside the map. Resize the level asset first.");
            if(Mathf.Abs(pos.x-(x+.5f))>.05f || Mathf.Abs(pos.y-(-y-.5f))>.05f) throw new Exception(piece.name+" is not aligned to a cell center. Use a 1-unit move snap.");
            char c=chars[y][x]; if(c=='#') throw new Exception(piece.name+" overlaps a wall.");
            if(source==board.platePrefab || source==board.doorPrefab)
            {
                if(data.TerrainAt(x,y)!=' ') throw new Exception("Ice, plates and doors cannot share a cell.");
                data.SetTerrain(x,y,source==board.platePrefab?'S':'D');
            }
            else if(source==board.goalPrefab)
            {
                if(c=='.'||c=='*'||c=='+') throw new Exception("Duplicate goals occupy the same cell."); chars[y][x]=c=='$'?'*':c=='@'?'+':'.';
            }
            else if(source==board.boxPrefab || source==board.playerPrefab)
            {
                if(c=='$'||c=='*'||c=='@'||c=='+') throw new Exception("Players or boxes overlap."); chars[y][x]=source==board.boxPrefab?(c=='.'?'*':'$'):(c=='.'?'+':'@');
            }
            else throw new Exception(piece.name+" is not one of the assigned piece prefabs.");
        }
        for(int y=0;y<h;y++) data.rows[y]=new string(chars[y]);
        string error=data.Validate(); if(error!=null) throw new Exception(error); return data;
    }
}
