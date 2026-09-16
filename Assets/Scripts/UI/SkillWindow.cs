using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DogLife
{
    // Hierarchy에 만든 창을 제어합니다. UI 생성이나 Inspector 자동 연결은 하지 않습니다.
    [DefaultExecutionOrder(-100)]
    public sealed class SkillWindow : MonoBehaviour
    {
        public GameObject windowRoot;
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
        public static bool IsOpen { get; private set; }
        public static bool BlocksGameplayInput => IsOpen || Time.frameCount == inputConsumedFrame;
        static int inputConsumedFrame = -1;
        public SkillCard Selected { get; private set; }
        public SkillCard Dragged { get; private set; }
        public SkillSlot DragSource { get; private set; }
        bool opened, motorInput, weaponInput, cameraInput, cursorVisible;
        float previousTimeScale;
        CursorLockMode cursorMode;

        void Start()
        {
            windowRoot.SetActive(false);
            dragGhost.gameObject.SetActive(false);
            IsOpen = false;
            foreach (var slot in slots) slot.Refresh();
            if (skills.Length > 0) Select(skills[0]);
            UpdateCount();
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.K)) SetOpen(!opened);
            else if (opened && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
        }
        public void Close() { SetOpen(false); }
        public void SetOpen(bool value)
        {
            if (opened == value) return;
            opened = value; IsOpen = value;
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
            if (!card) return;
            Selected = card;
            detailName.text = card.skillName; detailType.text = card.category;
            detailDescription.text = card.description; detailSource.text = card.obtainPath;
            detailIcon.sprite = card.icon.sprite; detailIcon.color = card.icon.color;
            foreach (var item in skills) item.selectionMark.SetActive(item == card);
        }
        public void BeginDrag(SkillCard card, SkillSlot source, PointerEventData data)
        {
            if (!opened || !card) return;
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
            if (!Dragged || !target) return;
            var source = DragSource;
            if (!source) foreach (var slot in slots) if (slot.Equipped == Dragged) { source = slot; break; }
            if (source != target)
            {
                var replaced = target.Equipped;
                target.SetSkill(Dragged);
                if (source) source.SetSkill(DragSource ? replaced : null);
            }
            UpdateCount(); EndDrag();
        }
        public void EndDrag()
        {
            Dragged = null; DragSource = null;
            if (dragGhost) dragGhost.gameObject.SetActive(false);
            foreach (var slot in slots) slot.SetHover(false);
        }
        public void Unequip(SkillSlot slot) { slot.SetSkill(null); UpdateCount(); }
        void UpdateCount()
        {
            int count = 0; foreach (var slot in slots) if (slot.Equipped) count++;
            equippedCount.text = count + " / " + slots.Length + " 장착";
        }
        void OnDisable() { if (opened) SetOpen(false); }
    }
}
