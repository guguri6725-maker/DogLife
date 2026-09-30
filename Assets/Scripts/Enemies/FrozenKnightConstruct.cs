using UnityEngine;
using DG.Tweening;
namespace DogLife
{
    // 기둥은 유효 타격 횟수, 중앙 결정은 창 충돌 횟수만 셉니다. VFX 콜라이더는 쓰지 않습니다.
    [RequireComponent(typeof(CombatHealth))]
    public sealed class FrozenKnightConstruct : CombatDamageGate
    {
        public enum Kind { Pillar, Pickup, Core }
        [SerializeField] Transform visual;
        [SerializeField] Collider body;
        [SerializeField] FrozenIceBurst burstPrefab;
        [SerializeField,Min(.01f)] float hitShake=.12f,hitDuration=.24f;
        [SerializeField,Min(1)] int requiredHits=3;
        [SerializeField,Min(.01f)] float hitDebounce=.12f,riseTime=.7f,pickupRadius=1.2f;
        CombatHealth health;FrozenKnightBoss owner;PlayerMotor player;Kind kind;
        int hits;float nextHit,born,lifetime,speed,shield;Vector3 size;bool spearDamage;
        Renderer surface;MaterialPropertyBlock surfaceBlock;Tween fractureTween;float fracture;Vector3 visualRest;
        public bool Broken=>!health||health.Dead||!gameObject.activeSelf;
        public int Hits=>hits;
        public bool IsPillar=>kind==Kind.Pillar;
        void Awake(){health=GetComponent<CombatHealth>();surface=visual.GetComponent<Renderer>();surfaceBlock=new MaterialPropertyBlock();visualRest=visual.localPosition;}
        public void Initialize(FrozenKnightBoss source,Kind value,PlayerMotor target,int count,float duration,float moveSpeed=0,float protection=0)
        {
            owner=source;kind=value;player=target;requiredHits=Mathf.Max(1,count);lifetime=duration;speed=moveSpeed;shield=protection;
            hits=0;born=Time.time;health.maxHealth=requiredHits;health.Restore();size=visual.localScale;
            body.enabled=value!=Kind.Pickup;visual.localScale=Vector3.zero;
        }
        public override bool BlocksDamage(float amount)
        {
            if(kind==Kind.Pickup)return true;
            if(kind==Kind.Core&&!spearDamage)return true;
            if(Time.time<nextHit&&!spearDamage)return true;
            nextHit=Time.time+hitDebounce;hits++;
            visual.DOKill();visual.localPosition=visualRest;
            visual.DOPunchPosition(new Vector3(hitShake,.025f,hitShake*.6f),hitDuration,8,.55f);
            fractureTween?.Kill();fractureTween=DOTween.To(()=>fracture,v=>fracture=v,Mathf.Clamp01(hits/(float)requiredHits),hitDuration).SetEase(Ease.OutCubic);
            if(burstPrefab)
            {
                var burst=Instantiate(burstPrefab,body.bounds.center,Quaternion.identity);
                burst.Play(hits>=requiredHits?FrozenIceBurst.Mode.Shatter:FrozenIceBurst.Mode.Chip,body.bounds.size,null);
            }
            return false;
        }
        public override float FilterDamage(float amount)=>1;
        public void ReceiveSpear(){if(kind!=Kind.Core||Broken)return;spearDamage=true;health.Damage(1);spearDamage=false;}
        // 기마 돌진은 타격 횟수/피격 간격과 무관하게 얼음벽을 한 번에 파괴합니다.
        public void ShatterFromCharge(FrozenKnightBoss source)
        {
            if(source!=owner||source.PhaseNumber!=2||!IsPillar||Broken)return;
            hits=requiredHits;
            if(burstPrefab){var burst=Instantiate(burstPrefab,body.bounds.center,Quaternion.identity);burst.Play(FrozenIceBurst.Mode.Shatter,body.bounds.size,null);}
            body.enabled=false;health.FatalDamage();
        }
        void Update()
        {
            if(!owner||!owner.gameObject.activeInHierarchy){Destroy(gameObject);return;}
            float age=Time.time-born;if(kind!=Kind.Pillar&&lifetime>0&&age>=lifetime){Destroy(gameObject);return;}
            float rise=Mathf.SmoothStep(0,1,age/riseTime);visual.localScale=size*rise;
            if(surface){surface.GetPropertyBlock(surfaceBlock);surfaceBlock.SetFloat("_CrackProgress",fracture);surface.SetPropertyBlock(surfaceBlock);}
            if(kind!=Kind.Pickup)return;
            if(player&&Vector3.Distance(player.transform.position,transform.position)<pickupRadius)
            {
                player.GetComponent<CombatHealth>().GrantFrostProtection(shield);
                if(burstPrefab){var burst=Instantiate(burstPrefab,transform.position,Quaternion.identity);burst.Play(FrozenIceBurst.Mode.Absorb,Vector3.one,player.transform);}
                Destroy(gameObject);return;
            }
            transform.position=Vector3.MoveTowards(transform.position,owner.transform.position+Vector3.up,speed*Time.deltaTime);
            visual.Rotate(0,45*Time.deltaTime,0,Space.Self);
            // 보스에 도착해도 플레이어가 먹은 것으로 처리하지 않습니다.
            if(Vector3.Distance(transform.position,owner.transform.position+Vector3.up)<.35f)Destroy(gameObject);
        }
        void OnDisable(){fractureTween?.Kill();if(visual)visual.DOKill();}
    }
}
