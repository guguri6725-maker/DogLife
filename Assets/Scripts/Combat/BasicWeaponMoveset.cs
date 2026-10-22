using UnityEngine;
namespace DogLife
{
    [CreateAssetMenu(menuName="DogLife/Basic Weapon Moveset")]
    public sealed class BasicWeaponMoveset : ScriptableObject
    {
        public InventoryWeapon.WeaponCategory category;
        public AnimationClip[] attacks;
        public AnimationClip axeWhirlwindEnter,axeWhirlwindLoop,axeWhirlwindExit;
        [Header("활 충전")]
        [Tooltip("버튼을 누르는 동안 재생하고 마지막 자세를 유지할 당기기 동작")]
        public AnimationClip bowDrawClip;
        [Header("활 시각 효과 — 피해 판정과 별도")]
        public BowFlightVisual bowFlightVisual;
        public BowReleaseVisual bowReleaseVisual;
        public Material bowChargeMaterial;
        public BowChargeVisual bowChargeVisual;
        [Tooltip("활 자세의 조준축을 맞추는 캐릭터 전체 회전. 허리 뼈를 비틀지 않습니다.")]
        public float bowVisualYawOffset;
        public float[] strikeTimes;
        public float[] secondaryStrikeTimes;
        [Header("공격음 타이밍")]
        [Tooltip("타격보다 먼저 휘두르는 소리를 재생할 시간. 공격 속도에 맞춰 함께 조정됩니다.")]
        public float[] attackAudioLeadTimes;
        [Range(.5f,1f)] public float comboLinkNormalized=1f;
        [Header("플레이 테스트용 동작 수치 — 기획 확정 스탯과 별도")]
        public float reach=2;
        [Min(.1f),Tooltip("공격 애니메이션과 타격 타이밍에 함께 적용하는 기본 속도")]
        public float attackSpeed=1f;
        [Min(.1f),Tooltip("일반형 대비 합성형 공격 속도 배율")]
        public float synthesizedAttackSpeed=2f;
        [Range(0,1),Tooltip("창 마지막 연속 찌르기의 개별 타격 피해 배율")]
        public float spearFlurryDamageMultiplier=.15f;
        [Min(1),Tooltip("활 완충 시 기본 공격력에 곱하는 배율")]
        public float bowFullChargeMultiplier=2.6f;
        public float projectileSpeed=10;
        [SerializeField, Min(.01f), Tooltip("투사체의 충돌 반경 (m)")]
        private float projectileHitRadius=.12f;
        public float ProjectileHitRadius=>Mathf.Max(.01f,projectileHitRadius);
        [SerializeField, Min(.01f), Tooltip("부메랑 판정의 세로 반두께. 가로 사거리와 별도로 조절합니다.")]
        private float boomerangHalfThickness=.06f;
        public float BoomerangHalfThickness=>Mathf.Max(.01f,boomerangHalfThickness);
        public float inputBuffer=.28f;
        public Material effectMaterial;
    }
}
