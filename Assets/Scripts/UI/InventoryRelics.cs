using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace DogLife
{
    public sealed class InventoryRelics : MonoBehaviour
    {
        SkillWindow window; RelicLoadout loadout;
        Text heading, explanation; RectTransform tooltip;
        readonly Image[] pictures=new Image[RelicLoadout.MaxSlots];
        readonly Text[] names=new Text[RelicLoadout.MaxSlots];
        public static void Attach(SkillWindow window)
        {
            var panel=AdventureUI.Rect("Owned abilities",window.windowRoot.transform,new Vector2(0,-320),new Vector2(1224,70));
            var view=panel.gameObject.AddComponent<InventoryRelics>();view.window=window;
            view.loadout=RelicLoadout.Get(window.inventory.gameObject);
            AdventureUI.Surface(panel.gameObject);
            var font=window.detailName.font;
            view.heading=AdventureUI.Label(panel,"Heading","보유 능력",new Vector2(-500,0),new Vector2(210,64),18,font);
            var frame=window.windowRoot.transform.Find("Frame");
            if(frame)foreach(var key in new[]{"Hint","Footer","FooterDivider"}){var old=frame.Find(key);if(old)old.gameObject.SetActive(false);}
            for(int i=0;i<namesLength;i++)
            {
                var card=AdventureUI.Rect("Ability "+i,panel,new Vector2(-260+i*250,0),new Vector2(240,52));
                var surface=AdventureUI.Surface(card.gameObject);surface.raycastTarget=true;
                view.pictures[i]=AdventureUI.Rect("Skill artwork",card,new Vector2(-95,0),new Vector2(36,36)).gameObject.AddComponent<Image>();view.pictures[i].raycastTarget=false;
                view.names[i]=AdventureUI.Label(card,"Name","",new Vector2(23,0),new Vector2(186,46),16,font);
                var hover=card.gameObject.AddComponent<InventoryRelicHover>();hover.owner=view;hover.index=i;
            }
            view.tooltip=AdventureUI.Rect("Ability description",window.windowRoot.transform,new Vector2(0,-145),new Vector2(470,240));
            AdventureUI.Surface(view.tooltip.gameObject).raycastTarget=false;
            view.explanation=AdventureUI.Label(view.tooltip,"Description","",Vector2.zero,new Vector2(426,208),22,font);
            view.explanation.alignment=TextAnchor.MiddleCenter;view.explanation.supportRichText=true;
            view.tooltip.gameObject.SetActive(false);
            view.loadout.Changed+=view.Refresh;view.Refresh();
        }
        const int namesLength=RelicLoadout.MaxSlots;
        void OnEnable(){if(loadout)Refresh();}
        void Refresh()
        {
            Hide();heading.text="보유 능력  "+loadout.Active.Count+" / "+RelicLoadout.MaxSlots+"\n마우스로 설명 확인";
            for(int i=0;i<names.Length;i++)
            {
                bool owned=i<loadout.Active.Count;
                names[i].text=owned?loadout.Active[i].WeaponLabel+"\n"+loadout.DisplayName(loadout.Active[i]):"빈 능력 칸";
                pictures[i].sprite=owned?Resources.Load<Sprite>("UI/RelicArt/"+loadout.Active[i].id):null;
                pictures[i].enabled=owned&&pictures[i].sprite;
                names[i].color=owned?AdventureUI.Cream:new Color(.6f,.55f,.46f);
                CenterContents(names[i],pictures[i]);
            }
        }
        static void CenterContents(Text label,Image picture)
        {
            // 빈 칸은 중앙에, 아이콘이 있으면 실제 글자 너비와 함께 묶어서 중앙에 배치합니다.
            var card=(RectTransform)label.transform.parent;
            float available=Mathf.Max(1,card.rect.width-20);
            float iconWidth=picture.enabled?picture.rectTransform.rect.width:0;
            float gap=picture.enabled?10:0;
            float textWidth=Mathf.Min(label.preferredWidth,Mathf.Max(1,available-iconWidth-gap));
            label.alignment=TextAnchor.MiddleCenter;
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,picture.enabled?textWidth:available);
            label.rectTransform.anchoredPosition=new Vector2((iconWidth+gap)*.5f,0);
            if(picture.enabled)
                picture.rectTransform.anchoredPosition=new Vector2(-(textWidth+gap)*.5f,0);
        }
        public void Show(int index)
        {
            if(!loadout||index>=loadout.Active.Count)return;
            var relic=loadout.Active[index];
            explanation.text="<b><color=#FFC071>"+loadout.DisplayName(relic)+"</color></b>\n"+relic.WeaponLabel+" · 일반형/합성형\n\n"+relic.DescriptionAt(loadout.Level(relic.id));
            tooltip.anchoredPosition=new Vector2(Mathf.Clamp(-260+index*250,-390,390),-145);
            tooltip.SetAsLastSibling();tooltip.gameObject.SetActive(true);
        }
        public void Hide(){if(tooltip)tooltip.gameObject.SetActive(false);}
        void OnDisable(){Hide();}
        void OnDestroy(){if(loadout)loadout.Changed-=Refresh;}
    }
    public sealed class InventoryRelicHover:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public InventoryRelics owner; public int index;
        public void OnPointerEnter(PointerEventData e){owner.Show(index);}
        public void OnPointerExit(PointerEventData e){owner.Hide();}
        void OnDisable(){if(owner)owner.Hide();}
    }
}

