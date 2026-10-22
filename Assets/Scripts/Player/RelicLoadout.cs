using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // Current-season loadout. Cleared at season completion; retry restores the entry snapshot.
    public sealed class RelicLoadout : MonoBehaviour
    {
        public const int MaxSlots = 4;
        readonly Dictionary<string,int> levels=new Dictionary<string,int>();
        readonly List<WeaponRelic> active = new List<WeaponRelic>();
        public IReadOnlyList<WeaponRelic> Active => active;
        public bool HasRoom => active.Count < MaxSlots;
        public event System.Action Changed;
        // 다른 스크립트는 이 메서드로 가져와 사용합니다. 프리팹에 미리 붙여 둘 필요가 없습니다.
        public static RelicLoadout Get(GameObject player)
        {
            var loadout = player.GetComponent<RelicLoadout>();
            if (!loadout) loadout = player.AddComponent<RelicLoadout>();
            return loadout;
        }
        public bool Has(string id) => active.Exists(r => r.id == id);
        public int Level(string id)=>levels.TryGetValue(id,out int level)?level:0;
        public int Level(RelicEffect effect){foreach(var relic in active)if(relic.effect==effect)return Level(relic.id);return 0;}
        public bool CanUpgrade(WeaponRelic relic)=>relic!=null&&Has(relic.id)&&Level(relic.id)<relic.MaxLevel;
        public string DisplayName(WeaponRelic relic)=>relic.displayName+(relic.MaxLevel>1?" Lv."+Level(relic.id):"");
        public bool TryAdd(WeaponRelic relic)
        {
            if(relic==null)return false;
            if(Has(relic.id)){if(!CanUpgrade(relic))return false;levels[relic.id]++;Changed?.Invoke();return true;}
            if(!HasRoom)return false;active.Add(relic);levels[relic.id]=1;Changed?.Invoke();return true;
        }
        public void Clear() {active.Clear();levels.Clear();Changed?.Invoke();}
        public string[] Ids(){var ids=new List<string>();foreach(var r in active)for(int i=0;i<Level(r.id);i++)ids.Add(r.id);return ids.ToArray();}
        public void RestoreIds(IEnumerable<string> ids)
        {
            active.Clear();levels.Clear();
            if (ids == null) {Changed?.Invoke();return;}
            foreach (var id in ids)
            {
                var relic = RelicCatalog.Find(id);
                if(relic==null)continue;
                if(Has(relic.id)){levels[relic.id]=Mathf.Min(relic.MaxLevel,Level(relic.id)+1);}
                else if(active.Count<MaxSlots){active.Add(relic);levels[relic.id]=1;}
            }
            Changed?.Invoke();
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
        public bool Replace(int index, WeaponRelic relic)
        {
            if(relic==null||Has(relic.id)||index<0||index>=active.Count)return false;
            levels.Remove(active[index].id);active[index]=relic;levels[relic.id]=1;Changed?.Invoke();return true;
        }
        public int SpearFinisherThreshold()=>12;
        // Common numerical modifiers are excluded from the current catalog.
        public float DamageMultiplier(InventoryWeapon.WeaponCategory category,bool synthesized,bool finisher,float darkFactor)=>1f;
    }
}
