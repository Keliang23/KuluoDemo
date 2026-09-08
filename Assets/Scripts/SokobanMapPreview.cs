using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kuluo.Sokoban
{
    public sealed class SokobanMapPreview : MonoBehaviour
    {
        public SokobanLevel level;
        public Sprite[] sprites;
        [SerializeField, HideInInspector] string bakedLayout;
        [SerializeField, HideInInspector] GameObject[] cells;
        static readonly string[] Kinds={"Floor","Wall","Ice","Player","Box","Goal","Plate","Door","Bars"};
        string Key()=>level==null?"":JsonUtility.ToJson(level.data);
        public void RecordLayout()
        {
            bakedLayout=Key(); var list=new List<GameObject>(); foreach(Transform child in transform) list.Add(child.gameObject); cells=list.ToArray();
        }
        void Start() {if(Key()!=bakedLayout) Rebuild();}
        [ContextMenu("Refresh Level Thumbnail")]
        public void Rebuild()
        {
            if(level==null || sprites==null || sprites.Length!=Kinds.Length || level.data.ValidateLayout()!=null) return;
            if(cells!=null) foreach(var cell in cells) if(cell!=null) {cell.SetActive(false); if(Application.isPlaying) Destroy(cell); else DestroyImmediate(cell);}
            var created=new List<GameObject>(); var data=level.data; var area=((RectTransform)transform).rect; int width=data.rows[0].Length,height=data.rows.Length;
            float size=Mathf.Min(area.width/width,area.height/height),left=(area.width-width*size)/2,top=(area.height-height*size)/2;
            void Part(int kind,int x,int y,Color color)
            {
                var r=new GameObject(Kinds[kind]+"_"+x+"_"+y,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>(); r.SetParent(transform,false); r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(left+x*size,-top-y*size); r.sizeDelta=new Vector2(size,size);
                var image=r.GetComponent<Image>(); image.sprite=sprites[kind]; image.color=color; image.raycastTarget=false; created.Add(r.gameObject);
            }
            var mint=new Color(.412f,.875f,.753f); var gold=new Color(.949f,.741f,.412f); var violet=new Color(.651f,.502f,.769f);
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            {
                char c=data.rows[y][x]; Part(c=='#'?1:0,x,y,Color.white); if(c=='#') continue;
                char t=data.TerrainAt(x,y); if(t=='I') Part(2,x,y,Color.white); if(t=='S') Part(6,x,y,violet); if(t=='D') {Part(7,x,y,Color.white); Part(8,x,y,Color.white);}
                if(c=='.'||c=='*'||c=='+') Part(5,x,y,Color.white); if(c=='$'||c=='*') Part(4,x,y,c=='*'?mint:gold); if(c=='@'||c=='+') Part(3,x,y,Color.white);
            }
            cells=created.ToArray(); bakedLayout=Key();
        }
    }
}
