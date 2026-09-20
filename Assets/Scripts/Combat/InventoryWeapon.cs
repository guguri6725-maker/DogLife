using UnityEngine;
namespace DogLife
{
    [CreateAssetMenu(menuName="DogLife/Inventory Weapon")]
    public sealed class InventoryWeapon : ScriptableObject
    {
        public enum WeaponCategory { Sword, Spear, Axe, Bow, Dagger, Boomerang }
        public enum WeaponForm { Basic, Synthesized }
        public enum BonePart { Tibia, Fibula, Scapula, Rib, Radius, Clavicle }
        [Header("분류")]
        public string weaponId;
        public WeaponCategory weaponCategory;
        public WeaponForm weaponForm;
        public BonePart bonePart;
        public string displayName;
        public string boneName;
        public enum BoneOrigin { Humanoid = 0, Dog = 1 }
        public BoneOrigin boneOrigin;
        [TextArea] public string description;
        [TextArea] public string obtainPath;
        public Sprite icon;
        public GameObject visualPrefab;
        [Tooltip("필드에 놓을 획득용 프리팹. 이 프리팹도 같은 무기 데이터를 참조합니다.")]
        public GameObject pickupPrefab;
        [Header("스탯 — 확정 여부가 꺼진 수치는 미정")]
        public bool damageDefined;
        [Min(0)] public float damage=3;
        [Min(0), Tooltip("부메랑 귀환 타격의 공격력")] public float returnDamage;
        public bool rangeDefined;
        [Min(0)] public float meleeRange=2;
        public bool attackIntervalDefined;
        [Min(0)] public float attackInterval;
        [Header("막타 카르마 변화")]
        public int normalKillKarma;
        public int bossKillKarma;
        [Header("동일 일반 무기 2개 합성 결과")]
        public InventoryWeapon sameWeaponEvolution;
        [TextArea] public string designNotes;
        public string DamageLabel => !damageDefined ? "미정" : returnDamage > 0 ? $"{damage:0.#} + {returnDamage:0.#} (왕복)" : damage.ToString("0.#");
        public string RangeLabel => rangeDefined ? meleeRange.ToString("0.#") + " m" : "미정";
    }
}
