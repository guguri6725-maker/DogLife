using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // 이번 런(계절들)에서만 유지되는 유물 장착 상태. 새 게임/재도전 시 SeasonRunSave를 통해 초기화됩니다.
    public sealed class RelicLoadout : MonoBehaviour
    {
        public const int MaxSlots = 3;
        readonly List<WeaponRelic> active = new List<WeaponRelic>();
        public IReadOnlyList<WeaponRelic> Active => active;
        public bool HasRoom => active.Count < MaxSlots;
        float lastDashAt = -10, lastHitAt = -10;
        // 다른 스크립트는 이 메서드로 가져와 사용합니다. 프리팹에 미리 붙여 둘 필요가 없습니다.
        public static RelicLoadout Get(GameObject player)
        {
            var loadout = player.GetComponent<RelicLoadout>();
            if (!loadout) loadout = player.AddComponent<RelicLoadout>();
            return loadout;
        }
        void Awake()
        {
            var motor = GetComponent<PlayerMotor>();
            if (motor) motor.Dashed += () => lastDashAt = Time.time;
            var health = GetComponent<CombatHealth>();
            if (health) health.Damaged += _ => lastHitAt = Time.time;
        }
        public bool Has(string id) => active.Exists(r => r.id == id);
        public bool TryAdd(WeaponRelic relic)
        {
            if (relic == null || !HasRoom || Has(relic.id)) return false;
            active.Add(relic); return true;
        }
        public void Clear() => active.Clear();
        public string[] Ids() => active.ConvertAll(r => r.id).ToArray();
        public void RestoreIds(IEnumerable<string> ids)
        {
            active.Clear();
            if (ids == null) return;
            foreach (var id in ids)
            {
                var relic = RelicCatalog.Find(id);
                if (relic != null && active.Count < MaxSlots) active.Add(relic);
            }
        }
        float Sum(RelicEffect effect, InventoryWeapon.WeaponCategory category, bool synthesized)
        {
            float total = 0;
            foreach (var relic in active)
                if (relic.effect == effect && relic.AppliesTo(category) && (!relic.synthesizedOnly || synthesized)) total += relic.value;
            return total;
        }
        public bool Has(RelicEffect effect, InventoryWeapon.WeaponCategory category, bool synthesized = false) => Sum(effect, category, synthesized) > 0;
        public float Value(RelicEffect effect, InventoryWeapon.WeaponCategory category, bool synthesized = false) => Sum(effect, category, synthesized);
        public int SpearFinisherThreshold()
        {
            foreach (var relic in active) if (relic.effect == RelicEffect.SpearFasterFinisher) return Mathf.RoundToInt(relic.value);
            return 12;
        }
        // darkFactor: 0(중립/빛)~1(완전한 어둠). 전투 스크립트에서 카르마 값으로 계산해 전달합니다.
        public float DamageMultiplier(InventoryWeapon.WeaponCategory category, bool synthesized, bool finisher, float darkFactor)
        {
            float bonus = 0;
            if (finisher)
            {
                bonus += Sum(RelicEffect.SwordFinisherSurge, category, synthesized);
                if (synthesized) bonus += Sum(RelicEffect.FusionResonance, category, synthesized);
                bonus += Sum(RelicEffect.ShadowFinisher, category, synthesized) * Mathf.Clamp01(darkFactor);
            }
            if (Time.time - lastDashAt <= .6f) bonus += Sum(RelicEffect.DashPower, category, synthesized);
            if (Time.time - lastHitAt <= 1.2f) bonus += Sum(RelicEffect.Vengeance, category, synthesized);
            return 1f + bonus;
        }
    }
}
