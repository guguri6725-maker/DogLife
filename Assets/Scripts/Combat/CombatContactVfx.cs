using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    // 무기 형태와 뼈 소재에 관계없이 동일한 Hierarchy 제작 프리팹을 사용합니다.
    public static class CombatContactVfx
    {
        static SynthesizedComboVfx prefab;
        static readonly List<SynthesizedComboVfx> active=new List<SynthesizedComboVfx>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Warmup(){active.Clear();prefab=Resources.Load<SynthesizedComboVfx>("SharedWeaponContact");}
        public static void Spawn(Vector3 point,Vector3 direction,Color color,float power,float scale=1)
        {
            // 호출부의 소재 색상은 받되, 공통 피격 색상은 프리팹의 연출에서 통일합니다.
            active.RemoveAll(fx=>!fx);
            if(active.Count>=24)return;
            if(!prefab)prefab=Resources.Load<SynthesizedComboVfx>("SharedWeaponContact");
            if(!prefab)return;
            var fx=Object.Instantiate(prefab);
            fx.InitializeContact(point,direction,power,scale);
            active.Add(fx);
        }
    }
}
