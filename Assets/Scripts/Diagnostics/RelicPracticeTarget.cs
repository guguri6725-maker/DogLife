using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(CombatHealth))]
    public sealed class RelicPracticeTarget:MonoBehaviour
    {
        CombatHealth health;
        void Awake(){health=GetComponent<CombatHealth>();health.minimumHealth=1;}
        void OnEnable(){if(health)health.Damaged+=OnHit;}
        void OnDisable(){if(health)health.Damaged-=OnHit;}
        bool restorePending;
        void OnHit(float damage){restorePending=true;}
        void LateUpdate(){if(restorePending){restorePending=false;health.Restore();}}
    }
}
