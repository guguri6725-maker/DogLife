using System;
namespace DogLife
{
    // 유물 효과 종류. 수치는 WeaponRelic.value에 담아 재사용합니다.
    public enum RelicEffect
    {
        SpearFasterFinisher, BowSplitShot, BoomerangDouble,
        AxeShockwave, DaggerPhantomStrike, SwordFinisherSurge,
        DashPower, Vengeance, ShadowFinisher, FusionResonance,
        SwordSpin, AxeWhirlwind, SpearRipple, SpearStorm, BowPierce, DaggerChain, BoomerangChase, BoomerangOrbit, DaggerHaste
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
        public int MaxLevel => effect==RelicEffect.SwordFinisherSurge||effect==RelicEffect.SpearRipple||effect==RelicEffect.AxeWhirlwind||effect==RelicEffect.DaggerPhantomStrike||effect==RelicEffect.BowSplitShot||effect==RelicEffect.BoomerangOrbit?3:1;
        public string DescriptionAt(int level){
            level=Math.Max(1,Math.Min(MaxLevel,level));
            string text=description;
            switch(effect){
                case RelicEffect.SwordFinisherSurge:text="검 3타가 전 방향을 베고 관통 검기를 "+level+"회 연속 발사합니다.";break;
                case RelicEffect.SpearRipple:text="창 1·2타 적중 시 전방으로 충격파를 "+level+"회 연속 발사합니다.";break;
                case RelicEffect.AxeWhirlwind:text="3타에서 자동으로 "+(level+1)+"초간 회전 타격합니다. 다른 공격은 불가하며 이동·구르기로 취소되지 않습니다.";break;
                case RelicEffect.DaggerPhantomStrike:text="타격할 때마다 그림자 "+level+"마리가 서로 다른 방향에서 공격합니다. 지상 4방향과 공중 대각선 4방향을 순환합니다.";break;
                case RelicEffect.BowSplitShot:text="완충 시 화살 "+(level*2+1)+"발을 발사합니다. 같은 적에게도 각각 명중합니다.";break;
                case RelicEffect.BoomerangOrbit:text="부메랑 장착 중 추가 "+level+"자루가 일정 간격으로 주위를 맴돕니다.";break;
            }
            return text;
        }
        public string WeaponLabel {
            get {
                if(appliesTo==null||appliesTo.Length!=1)return "공통 기술";
                switch(appliesTo[0]){
                    case InventoryWeapon.WeaponCategory.Sword:return "검 전용";
                    case InventoryWeapon.WeaponCategory.Dagger:return "단검 전용";
                    case InventoryWeapon.WeaponCategory.Spear:return "창 전용";
                    case InventoryWeapon.WeaponCategory.Axe:return "도끼 전용";
                    case InventoryWeapon.WeaponCategory.Bow:return "활 전용";
                    case InventoryWeapon.WeaponCategory.Boomerang:return "부메랑 전용";
                    default:return "무기 기술";
                }
            }
        }
        public bool AppliesTo(InventoryWeapon.WeaponCategory category)
            => appliesTo == null || appliesTo.Length == 0 || Array.IndexOf(appliesTo, category) >= 0;
    }
    public static class RelicCatalog
    {
        public static readonly WeaponRelic[] All =
        {
            new WeaponRelic{id="sword_finisher_surge",displayName="잔영 베기",description="검 3타가 전 방향을 베고 관통 검기를 발사합니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Sword},effect=RelicEffect.SwordFinisherSurge,value=1f,unlockCost=30},
            new WeaponRelic{id="sword_spin",displayName="회전베기",description="검 1·2타가 360도 회전 공격으로 바뀝니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Sword},effect=RelicEffect.SwordSpin,value=1f,unlockCost=30},
            new WeaponRelic{id="axe_shockwave",displayName="대지 가르기",description="도끼 1·2타 적중 시 주변에 충격파가 퍼집니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Axe},effect=RelicEffect.AxeShockwave,value=0.5f,unlockCost=30},
            new WeaponRelic{id="axe_whirlwind",displayName="회전 도끼",description="3타에서 자동으로 2초간 회전 타격합니다. 다른 공격은 불가하며 이동·구르기로 취소되지 않습니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Axe},effect=RelicEffect.AxeWhirlwind,value=1f,unlockCost=30},
            new WeaponRelic{id="spear_ripple",displayName="파문의 창",description="창 1·2타 적중 시 전방으로 충격파가 퍼집니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Spear},effect=RelicEffect.SpearRipple,value=0.5f,unlockCost=30},
            new WeaponRelic{id="spear_storm",displayName="창의 폭풍",description="3타의 12회 연속 찌르기가 전방 부채꼴 전체를 타격합니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Spear},effect=RelicEffect.SpearStorm,value=1f,unlockCost=30},
            new WeaponRelic{id="bow_split_shot",displayName="분산 사격",description="완충 시 화살 3발을 발사합니다. 같은 적에게도 각각 명중합니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Bow},effect=RelicEffect.BowSplitShot,value=8f,unlockCost=30},
            new WeaponRelic{id="bow_pierce",displayName="관통하는 화살",description="모든 화살이 적을 관통합니다. 벽에 닿으면 사라집니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Bow},effect=RelicEffect.BowPierce,value=1f,unlockCost=30},
            new WeaponRelic{id="dagger_phantom",displayName="그림자 복제",description="타격할 때마다 그림자 한 마리가 공격합니다. 적의 지상 4방향과 공중 대각선 4방향을 순환합니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Dagger},effect=RelicEffect.DaggerPhantomStrike,value=0.35f,unlockCost=30},
            new WeaponRelic{id="dagger_chain",displayName="질풍의 단검",description="단검 공격 시 2초간 이동속도가 20% 증가합니다. 재공격 시 지속시간만 갱신됩니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Dagger},effect=RelicEffect.DaggerHaste,value=0.2f,unlockCost=30},
            new WeaponRelic{id="boomerang_chase",displayName="추격용 부메랑",description="투척한 부메랑이 주변 적을 추적해 최대 4회 타격 후 돌아옵니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Boomerang},effect=RelicEffect.BoomerangChase,value=4f,unlockCost=30},
            new WeaponRelic{id="boomerang_orbit",displayName="맴도는 부메랑",description="부메랑 장착 중 추가 한 자루가 상시 주변을 맴돕니다.",appliesTo=new[]{InventoryWeapon.WeaponCategory.Boomerang},effect=RelicEffect.BoomerangOrbit,value=1f,unlockCost=30},
        };
        public static WeaponRelic Find(string id) => Array.Find(All, r => r.id == id);
    }
}
