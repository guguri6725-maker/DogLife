using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace DogLife
{
    public sealed class EconomyItemAudio:MonoBehaviour,IPointerEnterHandler,ISelectHandler,IPointerDownHandler,IPointerClickHandler
    {
        Button button;
        public bool shopPreview=true;
        bool disabledOnPress,hasPreview;
        Vector2 lastPointer;
        float previewAt;
        void Awake(){button=GetComponent<Button>();}
        void Preview(){if(shopPreview&&button&&button.IsInteractable())GameAudio.Play("Shop_Item_Select",Vector3.zero,.4f,ui:true,minimumInterval:.08f);}
        public void OnPointerEnter(PointerEventData e)
        {
            // Layout/raycast churn at a stationary pointer is not another user selection.
            if(hasPreview&&(e.position-lastPointer).sqrMagnitude<16)return;
            if(Time.unscaledTime<previewAt)return;
            hasPreview=true;lastPointer=e.position;previewAt=Time.unscaledTime+.2f;Preview();
        }
        public void OnSelect(BaseEventData e){if(e is PointerEventData||Time.unscaledTime<previewAt)return;previewAt=Time.unscaledTime+.2f;Preview();}
        void OnDisable(){hasPreview=false;disabledOnPress=false;}
        public void OnPointerDown(PointerEventData e){disabledOnPress=button&&!button.IsInteractable();}
        public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&disabledOnPress)GameAudio.Play("UI_Action_Denied",Vector3.zero,.55f,ui:true);disabledOnPress=false;}
    }
}
