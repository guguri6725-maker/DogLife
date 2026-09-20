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
        public string skillName, category;
        [TextArea(3, 8)] public string description;
        [TextArea(3, 6)] public string obtainPath;
        public Image icon;
        public Text nameLabel, categoryLabel;
        public GameObject selectionMark;
        public void Bind(InventoryWeapon item,string instanceId=null)
        {
            InstanceId=instanceId;
            Item=item;skillName=item?item.displayName:"빈 칸";category=item?"무기":"";
            description=item?item.description:"";obtainPath=item?item.obtainPath:"";
            nameLabel.text=skillName;categoryLabel.text=category;
            icon.sprite=item?item.icon:null;icon.enabled=item&&item.icon;icon.preserveAspect=true;icon.color=Color.white;
            selectionMark.SetActive(false);
        }
        public void OnPointerClick(PointerEventData e) { if (Item && e.button == PointerEventData.InputButton.Left && !e.dragging) window.Select(this); }
        public void OnBeginDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) window.BeginDrag(this, null, e); }
        public void OnDrag(PointerEventData e) { window.MoveDrag(e); }
        public void OnEndDrag(PointerEventData e) { window.EndDrag(); }
    }
}
