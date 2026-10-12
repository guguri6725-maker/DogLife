using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DogLife
{
    // Existing Hierarchy card design is reused for each owned weapon instance.
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
            if (equipButton) equipButton.onClick.AddListener(EquipSelected);
            if (unequipButton) unequipButton.onClick.AddListener(UnequipWeapon);
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
            inventory.EquipInstance(Dragged.InstanceId);EndDrag();
        }
        public void EndDrag()
        {
            Dragged = null; DragSource = null;
            if (dragGhost) dragGhost.gameObject.SetActive(false);
            foreach (var slot in slots) slot.SetHover(false);
        }
        public void Unequip(SkillSlot slot) { UnequipWeapon(); }
        public void EquipSelected() { if (inventory && Selected && Selected.Item) inventory.EquipInstance(Selected.InstanceId); }
        public void UnequipWeapon() { if (inventory) inventory.Unequip(); }
        void RefreshInventory()
        {
            var selected=Selected?Selected.InstanceId:null;
            EndDrag();
            Selected=null;
            ResizeCards(inventory?inventory.OwnedWeapons.Count:0);
            for(int i=0;i<skills.Length;i++)
            {
                var item=inventory&&i<inventory.OwnedWeapons.Count?inventory.OwnedWeapons[i]:null;
                skills[i].Bind(item,inventory.GetInstanceId(i));
                if(item&&skills[i].InstanceId==selected)Selected=skills[i];
            }
            foreach(var slot in slots)
            {
                SkillCard card=null;
                foreach(var item in skills)if(item.Item&&inventory&&item.InstanceId==inventory.EquippedInstanceId){card=item;break;}
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
            bool equipped=inventory&&inventory.EquippedWeapon;
            equippedCount.text=equipped?"1 / 1 장착":"0 / 1 장착 · 맨손";
            if(ownedCount)ownedCount.text=(inventory?inventory.OwnedWeapons.Count:0)+"개 보유";
            if(equipButton)equipButton.interactable=Selected&&Selected.Item&&inventory&&Selected.InstanceId!=inventory.EquippedInstanceId;
            if(unequipButton)unequipButton.interactable=equipped;
        }
        void OnDestroy()
        {
            if(inventory)inventory.Changed-=RefreshInventory;
            if(equipButton)equipButton.onClick.RemoveListener(EquipSelected);
            if(unequipButton)unequipButton.onClick.RemoveListener(UnequipWeapon);
            if(Instance==this)Instance=null;
        }
        void OnDisable() { if (opened) SetOpen(false); }
    }
}

