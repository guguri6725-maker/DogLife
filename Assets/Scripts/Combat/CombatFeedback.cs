using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    // 전역 시간을 바꾸지 않고 맞은 몸과 근접 공격 애니메이션만 짧게 멈춥니다.
    [DefaultExecutionOrder(15000)]
    public sealed class CombatFeedback : MonoBehaviour
    {
        [SerializeField,Range(0,.12f)] private float normalStop=.04f;
        [SerializeField,Range(0,.15f)] private float heavyStop=.08f;
        [SerializeField,Range(0,30)] private float recoilAngle=13;
        [SerializeField,Min(.05f)] private float recoilDuration=.22f;
        [SerializeField,Tooltip("비인간형 몬스터의 피격 반응 관절을 직접 연결합니다.")] private Transform recoilBone;
        [SerializeField,Range(.5f,3)] private float contactEffectScale=1;
        Animator animator;
        NavMeshAgent agent;
        Transform bone;
        float stopUntil,nextStop,recoilStarted=-10,originalSpeed,agentSpeed,strength;
        bool stopped,poseApplied;
        Quaternion baseRotation;
        Vector3 recoilAxis;
        public bool IsStopped=>stopped&&Time.time<stopUntil;
        float nextMeleeStagger,poise,lastMeleeHit,poiseImmuneUntil;
        bool poiseReady;
        bool IsBoss=>GetComponent<SpringBoarBoss>()||GetComponent<SpringBoarPhaseTwo>()||GetComponent<KrakenBoss>()||GetComponent<KrakenPhaseTwo>()||GetComponent<HarvestReaperBoss>()||GetComponent<FrozenKnightBoss>();
        // 패턴의 공격 판정·연출은 중단하지 않고 안전한 복귀 구간만 연장합니다.
        bool BossOpening()
        {
            var spring2=GetComponent<SpringBoarPhaseTwo>();
            if(spring2&&spring2.Active)return spring2.VulnerableWindow;
            var spring=GetComponent<SpringBoarBoss>();
            if(spring&&spring.enabled)return spring.State==SpringBoarBoss.Phase.Recovery||spring.State==SpringBoarBoss.Phase.Stunned;
            var summer2=GetComponent<KrakenPhaseTwo>();
            if(summer2&&summer2.enabled&&summer2.State!=KrakenPhaseTwo.Phase.Dormant)return summer2.State==KrakenPhaseTwo.Phase.Idle;
            var summer=GetComponent<KrakenBoss>();
            if(summer&&summer.enabled)return summer.State==KrakenBoss.Phase.Recovery||summer.State==KrakenBoss.Phase.Groggy;
            var autumn=GetComponent<HarvestReaperBoss>();
            if(autumn&&autumn.enabled)return autumn.State==HarvestReaperBoss.Phase.Recovery;
            var winter=GetComponent<FrozenKnightBoss>();
            return winter&&winter.enabled&&winter.State==FrozenKnightBoss.CombatState.Recovery;
        }
        public void ApplyMeleeAdvantage(InventoryWeapon.WeaponCategory category,bool finisher,float points)
        {
            var health=GetComponent<CombatHealth>();
            if(!health||health.player||health.practiceTarget||health.Dead||health.Current<=0||health.invulnerable)return;
            if(IsBoss)
            {
                if(Time.time<poiseImmuneUntil)return;
                if(Time.time-lastMeleeHit>5&&!poiseReady)poise=0;
                lastMeleeHit=Time.time;poise=Mathf.Min(100,poise+Mathf.Max(0,points));
                poiseReady=poise>=100;return;
            }
            // 구조물은 제외하고, 모든 계절의 잡몹 AI가 공유하는 정지 처리를 사용합니다.
            if(!GetComponent<MonsterHurtbox>()||Time.time<nextMeleeStagger)return;
            float duration=category==InventoryWeapon.WeaponCategory.Axe?(finisher?.32f:.16f):category==InventoryWeapon.WeaponCategory.Dagger?(finisher?.16f:.07f):(finisher?.24f:.12f);
            nextMeleeStagger=Time.time+duration+.35f;
            Hold(duration);
        }
        void Hold(float duration)
        {
            if(!stopped)
            {
                if(animator){originalSpeed=animator.speed;animator.speed=0;}
                if(agent){agentSpeed=agent.speed;agent.speed=0;}
            }
            stopped=true;stopUntil=Mathf.Max(stopUntil,Time.time+duration);
            nextStop=Mathf.Max(nextStop,stopUntil+.07f);
        }
        void Awake()
        {
            animator=GetComponentInChildren<Animator>();agent=GetComponent<NavMeshAgent>();
            bone=recoilBone;var bones=GetComponentsInChildren<Transform>();
            foreach(string name in new[]{"Spine2","Chest","Spine","Head"})
            {
                if(bone)break;
                foreach(var t in bones)if(t.name==name){bone=t;break;}
                if(bone)break;
            }
        }
        public static bool Paused(Component actor)
        {var feedback=actor.GetComponent<CombatFeedback>();return feedback&&feedback.IsStopped;}
        public static bool DealDamage(CombatHealth target,float amount,Vector3 contact,Vector3 direction,float power,Transform attacker,Color tint,bool useFullPlayerDamage=false,bool emphasized=false,int numberGroup=0)
        {
            if(!target)return false;
            float before=target.Current;target.Damage(amount,useFullPlayerDamage,emphasized,numberGroup,contact);
            if(target.Current>=before)return false;
            // 실제 피해가 들어간 플레이어 공격에만 추가 명중 충격을 줍니다.
            if(!target.player&&attacker&&attacker.GetComponent<PlayerWeapons>())
            {
                var view=Camera.main;var playerCamera=view?view.GetComponent<PlayerCamera>():null;
                if(playerCamera)playerCamera.WeaponContact(attacker,power);
            }
            var targetFeedback=target.GetComponent<CombatFeedback>();
            if(!target.player)CombatContactVfx.Spawn(contact,direction,tint,power,targetFeedback?targetFeedback.contactEffectScale:1);
            if(!target.Dead&&target.gameObject.activeInHierarchy)
            {
                var feedback=target.GetComponent<CombatFeedback>();if(!feedback)feedback=target.gameObject.AddComponent<CombatFeedback>();
                feedback.React(direction,power);
            }
            if(attacker&&attacker.gameObject.activeInHierarchy)
            {
                var feedback=attacker.GetComponent<CombatFeedback>();if(!feedback)feedback=attacker.gameObject.AddComponent<CombatFeedback>();
                feedback.Freeze(power);
            }
            return true;
        }
        public void Freeze(float power)
        {
            if(Time.time<nextStop)return;
            float duration=power>=1.4f?heavyStop:normalStop;
            nextStop=Time.time+duration+.07f;
            if(!stopped)
            {
                if(animator){originalSpeed=animator.speed;animator.speed=0;}
                if(agent){agentSpeed=agent.speed;agent.speed=0;}
            }
            stopped=true;stopUntil=Time.time+duration;
        }
        void React(Vector3 direction,float power)
        {
            Freeze(power);strength=Mathf.Clamp(power,.6f,1.65f);recoilStarted=Time.time;
            Vector3 worldAxis=Vector3.Cross(Vector3.up,direction.normalized).normalized;
            recoilAxis=bone&&bone.parent?bone.parent.InverseTransformDirection(worldAxis):worldAxis;
            if(recoilAxis.sqrMagnitude<.01f)recoilAxis=Vector3.right;
        }
        void RestorePose(){if(poseApplied&&bone)bone.localRotation=baseRotation;poseApplied=false;}
        void Update()
        {
            RestorePose();
            if(stopped&&!IsStopped)Resume();
            if(poiseReady)
            {
                var health=GetComponent<CombatHealth>();
                if(!health||health.Dead||health.Current<=0||health.invulnerable){poiseReady=false;poise=0;}
                else if(BossOpening())
                {
                    // 약 3~4회 콤보를 누적하면 0.65초의 추가 공격 기회, 이후 8초간 재발동 방지.
                    poiseReady=false;poise=0;poiseImmuneUntil=Time.time+8;Hold(.65f);
                }
            }
        }
        void LateUpdate()
        {
            if(!bone)return;
            float t=(Time.time-recoilStarted)/recoilDuration;
            if(t<0||t>=1)return;
            baseRotation=bone.localRotation;
            float envelope=Mathf.Sin(Mathf.PI*Mathf.Sqrt(t))*(1-t);
            bone.localRotation=Quaternion.AngleAxis(recoilAngle*strength*envelope,recoilAxis)*baseRotation;poseApplied=true;
        }
        void Resume()
        {
            if(!stopped)return;stopped=false;
            if(animator)animator.speed=originalSpeed;
            if(agent)agent.speed=agentSpeed;
        }
        public void CancelHitStop(){Resume();}
        void OnDisable(){RestorePose();Resume();recoilStarted=-10;nextStop=0;nextMeleeStagger=poise=lastMeleeHit=poiseImmuneUntil=0;poiseReady=false;}
    }
}
