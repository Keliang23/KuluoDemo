using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Kuluo.Sokoban
{
    [RequireComponent(typeof(Button))]
    public sealed class SokobanButtonStyle : MonoBehaviour
    {
        public Color normal = new Color(.169f,.259f,.333f);
        public Color selected = new Color(.412f,.875f,.753f);
        public Color textColor = new Color(.918f,.949f,.961f);
        public Color selectedText = new Color(.063f,.11f,.16f);
        public Color disabledText = new Color(.44f,.52f,.59f);
        bool? last;
        Button button;
        TMP_Text label;
        Color enabledText;
        void Awake() {button=GetComponent<Button>(); label=GetComponentInChildren<TMP_Text>(); enabledText=label.color;}
        void LateUpdate()
        {
            if (button==null || label==null) return;
            var desired=button.IsInteractable()?enabledText:disabledText;
            if (label.color!=desired) label.color=desired;
        }
        public void SetSelected(bool value)
        {
            if (last == value) return; last = value;
            var button = GetComponent<Button>(); var colors = button.colors;
            colors.normalColor = value ? selected : normal;
            colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, .2f);
            colors.selectedColor = colors.normalColor; colors.pressedColor = Color.Lerp(colors.normalColor, Color.black, .15f);
            button.colors = colors; enabledText = value ? selectedText : textColor; GetComponentInChildren<TMP_Text>().color = enabledText;
        }
    }
}
