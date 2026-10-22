using System;
namespace DogLife
{
    // 유물 효과 종류. 수치는 WeaponRelic.value에 담아 재사용합니다.
    public enum RelicEffect
    {
        SpearFasterFinisher, BowSplitShot, BoomerangDouble,
        AxeShockwave, DaggerPhantomStrike, SwordFinisherSurge,
        DashPower, Vengeance, ShadowFinisher, FusionResonance
    }
    // 런 중에만 유지되는 공격 패턴 변형 카드. 코드에 직접 정의해 애셋 없이 동작합니다.
    [Serializable]
    public sealed class WeaponRelic
    {
        public string id, displayName, description;
        public InventoryWeapon.WeaponCategory[] appliesTo;
        public bool synthesizedOnly;
        public RelicEffect effect;
        public float value;
        public int unlockCost;
        public bool AppliesTo(InventoryWeapon.WeaponCategory category)
            => appliesTo == null || appliesTo.Length == 0 || Array.IndexOf(appliesTo, category) >= 0;
    }
    public static class RelicCatalog
    {
        public static readonly WeaponRelic[] All =
        {
            new WeaponRelic{id="spear_fast_finisher",displayName="파도타기 찌르기",
                description="창 연속 찌르기의 마무리 발동 횟수가 12회에서 8회로 줄어듭니다.",
                appliesTo=new[]{InventoryWeapon.WeaponCategory.Spear},effect=RelicEffect.SpearFasterFinisher,value=8,unlockCost=30},
            new WeaponRelic{id="bow_split_shot",displayName="분산 사격",
                description="완전히 당긴 화살이 세 갈래로 퍼져 나갑니다.",
                appliesTo=new[]{InventoryWeapon.WeaponCategory.Bow},effect=RelicEffect.BowSplitShot,value=8,unlockCost=35},
            new WeaponRelic{id="boomerang_double",displayName="쌍부메랑",
                description="부메랑을 던질 때 한 자루를 더 엇갈려 던집니다.",
                appliesTo=new[]{InventoryWeapon.WeaponCategory.Boomerang},effect=RelicEffect.BoomerangDouble,value=12,unlockCost=35},
            new WeaponRelic{id="axe_shockwave",displayName="대지 가르기",
                description="도끼 마무리 타격이 지면 충격파를 더해 주변 적도 함께 타격합니다.",
                appliesTo=new[]{InventoryWeapon.WeaponCategory.Axe},effect=RelicEffect.AxeShockwave,value=.5f,unlockCost=30},
            new WeaponRelic{id="dagger_phantom",displayName="그림자 복제",
                description="단검 첫 타격에 등 뒤로도 환영 일격이 함께 터집니다.",
                appliesTo=new[]{InventoryWeapon.WeaponCategory.Dagger},effect=RelicEffect.DaggerPhantomStrike,value=.6f,unlockCost=30},
            new WeaponRelic{id="sword_finisher_surge",displayName="잔영 베기",
                description="검 마무리 타격의 피해가 크게 늘어납니다.",
                appliesTo=new[]{InventoryWeapon.WeaponCategory.Sword},effect=RelicEffect.SwordFinisherSurge,value=.35f,unlockCost=30},
            new WeaponRelic{id="dash_power",displayName="들숨 찌르기",
                description="대시 직후 첫 공격의 피해가 늘어납니다.",
                effect=RelicEffect.DashPower,value=.4f,unlockCost=25},
            new WeaponRelic{id="vengeance",displayName="역린",
                description="피격 직후의 다음 공격이 더 강하게 들어갑니다.",
                effect=RelicEffect.Vengeance,value=.5f,unlockCost=25},
            new WeaponRelic{id="shadow_finisher",displayName="그림자 전투술",
                description="카르마가 어둠에 가까울수록 마무리 타격 피해가 늘어납니다.",
                effect=RelicEffect.ShadowFinisher,value=.3f,unlockCost=30},
            new WeaponRelic{id="fusion_resonance",displayName="융합 공명",
                description="합성 무기 장착 중에는 마무리 타격 보너스가 추가로 늘어납니다.",
                synthesizedOnly=true,effect=RelicEffect.FusionResonance,value=.15f,unlockCost=40},
        };
        public static WeaponRelic Find(string id) => Array.Find(All, r => r.id == id);
    }
}
