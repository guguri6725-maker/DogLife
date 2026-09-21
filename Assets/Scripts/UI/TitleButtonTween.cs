using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DogLife
{
    [RequireComponent(typeof(Button))]
    public sealed class TitleButtonTween : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] RectTransform visual;
        [SerializeField, Range(1f, 1.3f)] float hoverScale = 1.10f;
        [SerializeField, Range(.8f, 1f)] float pressedScale = .94f;
        [SerializeField, Min(.01f)] float hoverDuration = .18f;
        [SerializeField, Min(.01f)] float pressDuration = .08f;
        Button button;
        Tween tween;
        Vector3 originalScale;
        bool hovered, selected, pressed;

        void Awake()
        {
            button = GetComponent<Button>();
            if (!visual && button.targetGraphic) visual = button.targetGraphic.rectTransform;
            if (!visual) visual = (RectTransform)transform;
            originalScale = visual.localScale;
        }
        void Animate()
        {
            if (!isActiveAndEnabled || !visual) return;
            tween?.Kill();
            float scale = !button.IsInteractable() ? 1 : pressed ? pressedScale : hovered || selected ? hoverScale : 1;
            tween = visual.DOScale(originalScale * scale, pressed ? pressDuration : hoverDuration)
                .SetEase(pressed ? Ease.OutQuad : Ease.OutCubic).SetUpdate(true);
        }
        public void OnPointerEnter(PointerEventData e) { hovered = true; Animate(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; Animate(); }
        public void OnPointerDown(PointerEventData e) { if(e.button != PointerEventData.InputButton.Left)return; pressed = true; Animate(); }
        public void OnPointerUp(PointerEventData e) { if(e.button != PointerEventData.InputButton.Left)return; pressed = false; Animate(); }
        public void OnSelect(BaseEventData e) { selected = !(e is PointerEventData); Animate(); }
        public void OnDeselect(BaseEventData e) { selected = false; pressed = false; Animate(); }
        void OnDisable()
        {
            tween?.Kill(); tween = null;
            if (visual) visual.localScale = originalScale;
            hovered = selected = pressed = false;
        }
    }
}
