using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DogLife
{
    // 스킬 정보는 각 목록 항목의 Inspector에서 수정합니다. Icon의 Source Image가 모든 표시의 원본입니다.
    public sealed class SkillCard : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public SkillWindow window;
        public InventoryWeapon Item { get; private set; }
        public string InstanceId { get; private set; }
        public int OwnedQuantity { get; private set; }
        public string skillName, category;
        [TextArea(3, 8)] public string description;
        [TextArea(3, 6)] public string obtainPath;
        public Image icon;
        public Text nameLabel, categoryLabel;
        public GameObject selectionMark;
        public void Bind(InventoryWeapon item,string instanceId=null,int ownedQuantity=1)
        {
            InstanceId=instanceId;
            OwnedQuantity=item?Mathf.Max(1,ownedQuantity):0;
            Item=item;skillName=item?item.displayName:"빈 칸";category=item?"무기":"";
            description=item?item.description:"";obtainPath=item?item.obtainPath:"";
            // 기존 카드의 보조 문구에 같은 무기의 총수량을 표시합니다.
            nameLabel.text=skillName;categoryLabel.text=OwnedQuantity>1?category+" · 보유 "+OwnedQuantity+"개":category;
            icon.sprite=item?item.icon:null;icon.enabled=item&&item.icon;icon.preserveAspect=true;icon.color=Color.white;
            selectionMark.SetActive(false);
        }
        public void OnPointerClick(PointerEventData e) { if (Item && e.button == PointerEventData.InputButton.Left && !e.dragging) window.Select(this); }
        public void OnBeginDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) window.BeginDrag(this, null, e); }
        public void OnDrag(PointerEventData e) { window.MoveDrag(e); }
        public void OnEndDrag(PointerEventData e) { window.EndDrag(); }
    }
}
