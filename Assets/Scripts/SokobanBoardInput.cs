using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Kuluo.Sokoban
{
    public sealed class SokobanBoardInput : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IPointerUpHandler
    {
        public event Action<Vector2, bool> paint;
        Vector2 previous;
        public void OnInitializePotentialDrag(PointerEventData e) {e.useDragThreshold=false;}
        public void OnPointerDown(PointerEventData e) {previous=e.position; Apply(e,e.position);}
        public void OnBeginDrag(PointerEventData e) {previous=e.pressPosition;}
        public void OnDrag(PointerEventData e) => Stroke(e);
        public void OnPointerUp(PointerEventData e) {if(e.dragging) Stroke(e);}
        void Stroke(PointerEventData e)
        {
            // Fill gaps between sampled pointer positions so fast strokes do not skip cells.
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(previous,e.position)/4f));
            for(int i=0;i<=steps;i++) Apply(e,Vector2.Lerp(previous,e.position,i/(float)steps));
            previous=e.position;
        }
        void Apply(PointerEventData e,Vector2 position)
        {
            if(e.button!=PointerEventData.InputButton.Left && e.button!=PointerEventData.InputButton.Right) return;
            var r=(RectTransform)transform;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(r,position,e.pressEventCamera,out var p) && r.rect.Contains(p))
                paint?.Invoke(new Vector2((p.x-r.rect.xMin)/r.rect.width,(p.y-r.rect.yMin)/r.rect.height),e.button==PointerEventData.InputButton.Right);
        }
    }
}
