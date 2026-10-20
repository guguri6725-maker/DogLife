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
        BoxCollider wallBody;Vector3 wallSize,wallCenter;bool rising;
        public bool Broken=>!health||health.Dead||!gameObject.activeSelf;
        public int Hits=>hits;
        public bool IsPillar=>kind==Kind.Pillar;
        void Awake()
        {
            health=GetComponent<CombatHealth>();surface=visual.GetComponent<Renderer>();surfaceBlock=new MaterialPropertyBlock();visualRest=visual.localPosition;
            wallBody=body as BoxCollider;
            if(wallBody){wallSize=wallBody.size;wallCenter=wallBody.center;}
        }
        public void Initialize(FrozenKnightBoss source,Kind value,PlayerMotor target,int count,float duration,float moveSpeed=0,float protection=0)
        {
            owner=source;kind=value;player=target;requiredHits=Mathf.Max(1,count);lifetime=duration;speed=moveSpeed;shield=protection;
            if(value==Kind.Pillar)GameAudio.Play("WinterBoss_IceWall_Rise",transform.position,.65f);hits=0;born=Time.time;health.maxHealth=requiredHits;health.Restore();size=visual.localScale;
            body.enabled=value!=Kind.Pickup;visual.localScale=Vector3.zero;
            if(value==Kind.Pillar)
            {
                // 긴 벽은 폭을 유지하며 바닥부터 올라옵니다. 보이지 않는 완성 크기의 충돌을 먼저 켜지 않습니다.
                rising=true;body.enabled=false;
                visual.localScale=new Vector3(size.x,.001f,size.z);
                visual.localPosition=visualRest-Vector3.up*size.y*.5f;
            }
        }
        public override bool BlocksDamage(float amount)
        {
            if(kind==Kind.Pickup)return true;
            if(kind==Kind.Core&&!spearDamage)return true;
            if(Time.time<nextHit&&!spearDamage)return true;
            nextHit=Time.time+hitDebounce;hits++;if(hits>=requiredHits&&kind==Kind.Pillar)GameAudio.Play("WinterBoss_IceWall_Break",transform.position,.7f);
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
        void Update()
        {
            if(!owner||!owner.gameObject.activeInHierarchy){Destroy(gameObject);return;}
            float age=Time.time-born;if(kind!=Kind.Pillar&&lifetime>0&&age>=lifetime){Destroy(gameObject);return;}
            float rise=Mathf.SmoothStep(0,1,age/riseTime);
            if(kind==Kind.Pillar)
            {
                visual.localScale=new Vector3(size.x,Mathf.Max(.001f,size.y*rise),size.z);
                if(rising)
                {
                    visual.localPosition=visualRest-Vector3.up*size.y*.5f*(1-rise);
                    if(wallBody)
                    {
                        wallBody.size=new Vector3(wallSize.x,Mathf.Max(.001f,wallSize.y*rise),wallSize.z);
                        wallBody.center=wallCenter-Vector3.up*wallSize.y*.5f*(1-rise);
                    }
                    // Inspector의 PlayerBody 전용 충돌을 유지하며, 적의 NavMesh는 잘라내지 않습니다.
                    body.enabled=rise>.02f;
                    if(rise>=1)rising=false;
                }
            }
            else visual.localScale=size*rise;
            if(surface){surface.GetPropertyBlock(surfaceBlock);surfaceBlock.SetFloat("_CrackProgress",fracture);surface.SetPropertyBlock(surfaceBlock);}
            if(kind!=Kind.Pickup)return;
            if(player&&Vector3.Distance(player.transform.position,transform.position)<pickupRadius)
            {
                GameAudio.Play("WinterBoss_Crystal_Get",player.transform.position,.65f);player.GetComponent<CombatHealth>().GrantFrostProtection(shield);
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
