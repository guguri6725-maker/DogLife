using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;

namespace DogLife
{
    // 같은 무기는 기존 Hierarchy 카드 하나에 묶고 실제 보유 인스턴스는 유지합니다.
    [DefaultExecutionOrder(-100)]
    public sealed class SkillWindow : MonoBehaviour
    {
        public GameObject windowRoot;
        public PlayerInventory inventory;
        public Button equipButton, unequipButton;
        public Text ownedCount;
        public SkillCard[] skills;
        public SkillSlot[] slots;
        public Text detailName, detailType, detailDescription, detailSource, equippedCount;
        public Image detailIcon;
        public RectTransform dragGhost;
        public Image dragIcon;
        public Text dragName;
        public Canvas canvas;
        public PlayerMotor motor;
        public PlayerWeapons weapons;
        public PlayerCamera playerCamera;
        // 인벤토리는 현재 게임 씬에 하나만 두고, 열린 상태도 해당 인스턴스에서 읽습니다.
        public static SkillWindow Instance { get; private set; }
        public static bool IsOpen => Instance && Instance.opened;
        public static bool BlocksGameplayInput => IsOpen || Time.frameCount == inputConsumedFrame || GameSettingsWindow.BlocksGameplayInput || EconomyWindow.BlocksInput;
        static int inputConsumedFrame = -1;
        public int SelectedSlot {get;private set;}
        public SkillCard Selected { get; private set; }
        public SkillCard Dragged { get; private set; }
        public SkillSlot DragSource { get; private set; }
        bool opened, motorInput, weaponInput, cameraInput, cursorVisible;
        float previousTimeScale;
        CursorLockMode cursorMode;
        SkillCard cardTemplate;
        RectTransform listContent;
        Vector2 firstCardPosition;
        float cardStride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { Instance = null; inputConsumedFrame = -1; }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Debug.LogWarning("중복 인벤토리의 입력 처리를 비활성화했습니다.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        void ResizeCards(int count)
        {
            if(!cardTemplate)
            {
                var original=skills[0];var rect=(RectTransform)original.transform;
                listContent=(RectTransform)rect.parent;firstCardPosition=rect.anchoredPosition;
                cardStride=skills.Length>1?Mathf.Abs(((RectTransform)skills[1].transform).anchoredPosition.y-firstCardPosition.y):rect.rect.height+8;
                cardTemplate=Instantiate(original,windowRoot.transform,false);
                cardTemplate.name="WeaponCardTemplate";cardTemplate.gameObject.SetActive(false);
            }
            var cards=new System.Collections.Generic.List<SkillCard>(skills);
            while(cards.Count>count){var last=cards[cards.Count-1];last.gameObject.SetActive(false);Destroy(last.gameObject);cards.RemoveAt(cards.Count-1);}
            while(cards.Count<count){var card=Instantiate(cardTemplate,listContent,false);card.window=this;card.gameObject.SetActive(true);cards.Add(card);}
            skills=cards.ToArray();
            for(int i=0;i<skills.Length;i++){skills[i].name="WeaponCard_"+(i+1);((RectTransform)skills[i].transform).anchoredPosition=firstCardPosition+Vector2.down*cardStride*i;}
            listContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,count==0?0:((RectTransform)cardTemplate.transform).rect.height+(count-1)*cardStride);
        }

        void Start()
        {
            AdventureUI.Theme(windowRoot);
            windowRoot.SetActive(false);
            dragGhost.gameObject.SetActive(false);
            foreach (var slot in slots) slot.Refresh();
            if (inventory) inventory.Changed += RefreshInventory;
            RefreshInventory();
            UpdateCount();
        }
        void Update()
        {
            if(GameSettingsWindow.BlocksGameplayInput||EconomyWindow.BlocksInput)return;
            if (Input.GetKeyDown(KeyCode.E)) SetOpen(!opened);
            else if (opened && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
        }
        public void Close() { SetOpen(false); }
        public void SetOpen(bool value)
        {
            // 숨겨진 인벤토리가 연출 도중 입력을 가져가지 않게 합니다.
            if(value&&BossCinematicUI.Hidden)return;
            if(value&&StoryBook.BlocksInput)return;
            if(value&&EconomyWindow.BlocksInput)return;
            if (Instance != this || opened == value) return;
            opened = value;
            GameAudio.Play(value?"BagOpen":"BagClose",Vector3.zero,.6f,ui:true);
            // 닫기 클릭이나 Esc가 같은 프레임의 공격·카메라 입력으로 전달되지 않게 합니다.
            inputConsumedFrame = Time.frameCount;
            windowRoot.SetActive(value);
            if (value)
            {
                // UI 조작 중 공격·시점 이동을 막고, 닫을 때 기존 상태를 복원합니다.
                CombatFeedback.CancelGlobalHitSlow();
                previousTimeScale = Time.timeScale; Time.timeScale = 0;
                cursorMode = Cursor.lockState; cursorVisible = Cursor.visible;
                if (motor) { motorInput = motor.readInput; motor.readInput = false; motor.MoveInput = Vector2.zero; }
                if (weapons) { weaponInput = weapons.readInput; weapons.readInput = false; }
                if (playerCamera) { cameraInput = playerCamera.readInput; playerCamera.readInput = false; }
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else
            {
                EndDrag(); Time.timeScale = previousTimeScale;
                if (motor) motor.readInput = motorInput;
                if (weapons) weapons.readInput = weaponInput;
                if (playerCamera) playerCamera.readInput = cameraInput;
                Cursor.lockState = cursorMode; Cursor.visible = cursorVisible;
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            }
        }
        public void Select(SkillCard card)
        {
            if (!card || !card.Item) return;
            Selected = card;
            detailName.text = card.skillName;
            var weapon=card.Item;
            detailType.text = (weapon.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid?"인간뼈":"강아지뼈")+" · "+(weapon.weaponForm==InventoryWeapon.WeaponForm.Basic?"일반형":"합성형");
            if(card.OwnedQuantity>1)detailType.text+=" · 보유 "+card.OwnedQuantity+"개";
            detailDescription.text = "뼈 종류   "+(string.IsNullOrWhiteSpace(weapon.boneName)?"미지정":weapon.boneName)+"\n\n"+weapon.description;
            detailSource.text = "공격력     "+weapon.DamageLabel+"\n공격 범위     "+weapon.RangeLabel;
            detailIcon.sprite = card.icon.sprite; detailIcon.color = card.icon.color;
            foreach (var item in skills) item.selectionMark.SetActive(item == card);
            UpdateCount();
        }
        public void BeginDrag(SkillCard card, SkillSlot source, PointerEventData data)
        {
            if (!opened || !card || !card.Item) return;
            Dragged = card; DragSource = source; Select(card);
            dragIcon.sprite = card.icon.sprite; dragIcon.color = card.icon.color;
            dragName.text = card.skillName; dragGhost.gameObject.SetActive(true);
            MoveDrag(data);
        }
        public void MoveDrag(PointerEventData data)
        {
            if (!Dragged) return;
            // Canvas 배율과 해상도에 맞춰 미리 배치된 드래그 이미지만 이동합니다.
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,
                data.position, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var point);
            dragGhost.anchoredPosition = point + new Vector2(26, -26);
        }
        public void DropOn(SkillSlot target)
        {
            if (!Dragged || !Dragged.Item || !target || !inventory) return;
            SelectedSlot=System.Array.IndexOf(slots,target);
            // 슬롯에서 끌 때는 그 칸의 실제 무기를, 목록에서는 사용 가능한 복사본을 옮깁니다.
            var id=DragSource?inventory.SlotInstanceId(System.Array.IndexOf(slots,DragSource)):ResolveInstance(Dragged.Item,SelectedSlot);
            inventory.AssignSlot(SelectedSlot,id);EndDrag();
        }
        public void EndDrag()
        {
            Dragged = null; DragSource = null;
            if (dragGhost) dragGhost.gameObject.SetActive(false);
            foreach (var slot in slots) slot.SetHover(false);
        }
        public void SelectSlot(SkillSlot slot)
        {
            SelectedSlot=System.Array.IndexOf(slots,slot);
            if(slot.Equipped)Select(slot.Equipped);
            foreach(var item in slots)item.Refresh();UpdateCount();
        }
        public void Unequip(SkillSlot slot) { if(inventory)inventory.ClearSlot(System.Array.IndexOf(slots,slot)); }
        public void EquipSelected() { if (inventory && Selected && Selected.Item) inventory.AssignSlot(SelectedSlot,ResolveInstance(Selected.Item,SelectedSlot)); }
        public void UnequipWeapon() { if (inventory) inventory.ClearSlot(SelectedSlot); }
        string ResolveInstance(InventoryWeapon weapon,int slot)
        {
            // 같은 무기 두 개를 소유하면 각각 다른 장착칸에 넣을 수 있습니다.
            var current=inventory.SlotInstanceId(slot);
            if(inventory.FindInstance(current)==weapon)return current;
            var other=inventory.SlotInstanceId(1-slot);
            string fallback=null;
            for(int i=0;i<inventory.OwnedWeapons.Count;i++)
            {
                if(inventory.OwnedWeapons[i]!=weapon)continue;
                var id=inventory.GetInstanceId(i);fallback=id;
                if(id!=other)return id;
            }
            return fallback;
        }
        void RefreshInventory()
        {
            var selected=Selected?Selected.Item:null;
            EndDrag();
            Selected=null;
            // 표시만 종류별로 묶습니다. 저장 순서와 장착·합성용 인스턴스 ID는 유지합니다.
            var groups=Enumerable.Range(0,inventory?inventory.OwnedWeapons.Count:0)
                .Where(i=>inventory.OwnedWeapons[i])
                .GroupBy(i=>inventory.OwnedWeapons[i]).ToArray();
            ResizeCards(groups.Length);
            var order=groups.Select(g=>g.First())
                .OrderBy(i=>inventory.OwnedWeapons[i]? (int)inventory.OwnedWeapons[i].weaponForm:int.MaxValue)
                .ThenBy(i=>inventory.OwnedWeapons[i]? (int)inventory.OwnedWeapons[i].weaponCategory:int.MaxValue)
                .ThenBy(i=>inventory.OwnedWeapons[i]&&inventory.OwnedWeapons[i].boneOrigin==InventoryWeapon.BoneOrigin.Dog?0:1)
                .ThenBy(i=>inventory.OwnedWeapons[i]?inventory.OwnedWeapons[i].weaponId:"",System.StringComparer.Ordinal)
                .ToArray();
            var quantities=inventory?inventory.OwnedWeapons.Where(w=>w).GroupBy(w=>w).ToDictionary(g=>g.Key,g=>g.Count()):null;
            for(int i=0;i<skills.Length;i++)
            {
                int source=order[i];var item=inventory.OwnedWeapons[source];
                skills[i].Bind(item,inventory.GetInstanceId(source),item?quantities[item]:0);
                if(item==selected)Selected=skills[i];
            }
            foreach(var slot in slots)
            {
                SkillCard card=null;
                foreach(var item in skills)if(item.Item&&inventory&&item.Item==inventory.FindInstance(inventory.SlotInstanceId(System.Array.IndexOf(slots,slot)))){card=item;break;}
                slot.SetSkill(card);
            }
            if(!Selected)foreach(var card in skills)if(card.Item){Selected=card;break;}
            if(Selected)Select(Selected);
            else
            {
                detailName.text="보유한 무기가 없습니다";detailType.text="맨손 · 기본 공격";
                detailDescription.text="무기를 획득하면 목록에서 선택해 장착할 수 있습니다.\n\n일반형 무기를 장착하면 무기별 콤보를 사용합니다.";
                detailSource.text="뼈 종류     —\n분류     —\n공격력     —\n공격 범위     —";detailIcon.enabled=false;
            }
            if(Selected)detailIcon.enabled=true;
            UpdateCount();
        }
        void UpdateCount()
        {
            int count=0;if(inventory)for(int n=0;n<2;n++)if(!string.IsNullOrEmpty(inventory.SlotInstanceId(n)))count++;
            equippedCount.text=count+" / 2 장착  ·  [1] [2] 무기 전환";
            if(ownedCount)ownedCount.text=(inventory?inventory.OwnedWeapons.Count:0)+"개 보유";
            if(equipButton)equipButton.interactable=Selected&&Selected.Item&&inventory&&Selected.Item!=inventory.FindInstance(inventory.SlotInstanceId(SelectedSlot));
            if(unequipButton)unequipButton.interactable=inventory&&!string.IsNullOrEmpty(inventory.SlotInstanceId(SelectedSlot));
        }
        void OnDestroy()
        {
            if(inventory)inventory.Changed-=RefreshInventory;
            if(Instance==this)Instance=null;
        }
        void OnDisable() { if (opened) SetOpen(false); }
    }
}

