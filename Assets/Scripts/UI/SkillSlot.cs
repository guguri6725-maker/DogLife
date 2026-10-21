using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DogLife
{
    // 장착 슬롯은 드롭으로 채우고, 슬롯 간 드래그로 교환하며, 우클릭으로 해제합니다.
    public sealed class SkillSlot : MonoBehaviour, IDropHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public SkillWindow window;
        public Image icon;
        public Text skillLabel;
        public Text keyLabel;
        public GameObject emptyMark, hoverMark;
        public SkillCard Equipped { get; private set; }
        public void SetSkill(SkillCard card) { Equipped = card; Refresh(); }
        public void Refresh()
        {
            int index=System.Array.IndexOf(window.slots,this);
            if(keyLabel)keyLabel.text="["+(index+1)+"]"+(window.inventory&&window.inventory.ActiveSlot==index?" 사용":"");
            hoverMark.SetActive(window.SelectedSlot==index);
            icon.enabled = Equipped != null; emptyMark.SetActive(!Equipped);
            skillLabel.text = Equipped ? Equipped.skillName : "빈 슬롯";
            if (Equipped) { icon.sprite = Equipped.icon.sprite; icon.color = Equipped.icon.color;icon.preserveAspect=true; }
        }
        public void SetHover(bool value) { hoverMark.SetActive(value||window.SelectedSlot==System.Array.IndexOf(window.slots,this)); }
        public void OnDrop(PointerEventData e) { window.DropOn(this); }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) window.Unequip(this);
            else if (e.button == PointerEventData.InputButton.Left && !e.dragging) window.SelectSlot(this);
        }
        public void OnBeginDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) window.BeginDrag(Equipped, this, e); }
        public void OnDrag(PointerEventData e) { window.MoveDrag(e); }
        public void OnEndDrag(PointerEventData e) { window.EndDrag(); }
        public void OnPointerEnter(PointerEventData e) { SetHover(window.Dragged != null); }
        public void OnPointerExit(PointerEventData e) { SetHover(false); }
    }
}
