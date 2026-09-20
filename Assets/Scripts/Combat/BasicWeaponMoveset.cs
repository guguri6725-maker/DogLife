using UnityEngine;
namespace DogLife
{
    [CreateAssetMenu(menuName="DogLife/Basic Weapon Moveset")]
    public sealed class BasicWeaponMoveset : ScriptableObject
    {
        public InventoryWeapon.WeaponCategory category;
        public AnimationClip[] attacks;
        public float[] strikeTimes;
        public float[] secondaryStrikeTimes;
        [Range(.5f,1f)] public float comboLinkNormalized=1f;
        [Header("플레이 테스트용 동작 수치 — 기획 확정 스탯과 별도")]
        public float reach=2;
        public float projectileSpeed=10;
        [SerializeField, Min(.01f), Tooltip("투사체의 충돌 반경 (m)")]
        private float projectileHitRadius=.12f;
        public float ProjectileHitRadius=>Mathf.Max(.01f,projectileHitRadius);
        public float inputBuffer=.28f;
        public Material effectMaterial;
    }
}
