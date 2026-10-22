using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // 유물 3장 중 1장을 고르는 전체 화면 카드. RelicShrine이 상호작용 시점에 띄웁니다.
    // 전부 코드로 만든 화면이라 씬/프리팹 작업 없이 동작합니다.
    public static class RelicDraftUI
    {
        static bool open;
        public static bool IsOpen => open;
        // 전시회 빌드 기준: 발자국 해금 없이 전체 유물 목록에서 바로 뽑습니다.
        public static WeaponRelic[] PrepareChoices(GameObject player, RelicLoadout loadout)
        {
            if (!loadout.HasRoom) return null;
            var inventory = player.GetComponent<PlayerInventory>();
            InventoryWeapon.WeaponCategory? equipped = inventory && inventory.EquippedWeapon ? inventory.EquippedWeapon.weaponCategory : (InventoryWeapon.WeaponCategory?)null;
            var pool = new List<WeaponRelic>();
            foreach (var relic in RelicCatalog.All) if (!loadout.Has(relic.id)) pool.Add(relic);
            if (pool.Count == 0) return null;
            return PickChoices(pool, equipped, Mathf.Min(3, pool.Count));
        }
        static WeaponRelic[] PickChoices(List<WeaponRelic> pool, InventoryWeapon.WeaponCategory? equipped, int count)
        {
            var weighted = new List<WeaponRelic>();
            foreach (var relic in pool)
            {
                bool matchesEquipped = equipped.HasValue && relic.appliesTo != null && relic.appliesTo.Length > 0 && relic.AppliesTo(equipped.Value);
                int weight = matchesEquipped ? 3 : 1;
                for (int i = 0; i < weight; i++) weighted.Add(relic);
            }
            var result = new List<WeaponRelic>();
            var rng = new System.Random();
            while (result.Count < count && weighted.Count > 0)
            {
                var pick = weighted[rng.Next(weighted.Count)];
                weighted.RemoveAll(r => r.id == pick.id);
                result.Add(pick);
            }
            return result.ToArray();
        }
        public static void Show(GameObject player, RelicLoadout loadout, WeaponRelic[] choices, System.Action onClosed)
        {
            if (open || choices == null || choices.Length == 0) { onClosed?.Invoke(); return; }
            open = true;
            var motor = player.GetComponent<PlayerMotor>();
            var weapons = player.GetComponent<PlayerWeapons>();
            var camera = Camera.main ? Camera.main.GetComponent<PlayerCamera>() : null;
            bool motorInput = motor && motor.readInput, weaponInput = weapons && weapons.readInput, cameraInput = camera && camera.readInput;
            float previousScale = Time.timeScale;
            var cursorMode = Cursor.lockState; bool cursorVisible = Cursor.visible;
            if (motor) motor.readInput = false;
            if (weapons) weapons.readInput = false;
            if (camera) camera.readInput = false;
            Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var font = StoryBook.Instance ? StoryBook.Instance.Library.font : Font.CreateDynamicFontFromOSFont("Malgun Gothic", 28);
            var root = new GameObject("Relic Draft", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(player.transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30500;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var shade = AdventureUI.Rect("Shade", root.transform, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
            shade.gameObject.AddComponent<Image>().color = new Color(.035f, .022f, .016f, .72f);
            AdventureUI.Label(root.transform, "Heading", "유물을 하나 고르세요", new Vector2(0, 300), new Vector2(900, 70), 38, font).color = AdventureUI.Gold;
            float[] xs = { -380, 0, 380 };
            for (int i = 0; i < choices.Length; i++)
            {
                var relic = choices[i];
                var card = AdventureUI.Rect("Card" + i, root.transform, new Vector2(xs[i], -20), new Vector2(340, 420));
                AdventureUI.Surface(card.gameObject);
                AdventureUI.Label(card, "Name", relic.displayName, new Vector2(0, 150), new Vector2(300, 60), 26, font).color = AdventureUI.Gold;
                var desc = AdventureUI.Label(card, "Description", relic.description, new Vector2(0, 10), new Vector2(300, 220), 21, font);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap; desc.verticalOverflow = VerticalWrapMode.Overflow;
                AdventureUI.Button(card, "Pick", "장착", new Vector2(0, -170), new Vector2(240, 64), font, () =>
                {
                    loadout.TryAdd(relic);
                    Close(root, motor, weapons, camera, motorInput, weaponInput, cameraInput, previousScale, cursorMode, cursorVisible, onClosed);
                });
            }
            AdventureUI.Theme(root);
        }
        static void Close(GameObject root, PlayerMotor motor, PlayerWeapons weapons, PlayerCamera camera, bool motorInput, bool weaponInput, bool cameraInput, float previousScale, CursorLockMode cursorMode, bool cursorVisible, System.Action onClosed)
        {
            open = false;
            if (root) Object.Destroy(root);
            Time.timeScale = previousScale; Cursor.lockState = cursorMode; Cursor.visible = cursorVisible;
            if (motor) motor.readInput = motorInput;
            if (weapons) weapons.readInput = weaponInput;
            if (camera) camera.readInput = cameraInput;
            onClosed?.Invoke();
        }
    }
}
