using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Kuluo.Sokoban
{
    public sealed class SokobanSliderFeedback : MonoBehaviour, IPointerUpHandler, IMoveHandler
    {
        public event Action released;
        public void OnPointerUp(PointerEventData e) => released?.Invoke();
        public void OnMove(AxisEventData e) => released?.Invoke();
    }
}
