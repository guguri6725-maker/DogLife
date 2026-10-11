using System;
using UnityEngine;

namespace DogLife
{
    // 캐릭터의 체력, 피격 무적, 사망 및 지속 피해를 관리합니다.
    public sealed class CombatHealth : MonoBehaviour
    {
        public float maxHealth = 30;
        public bool player, practiceTarget;
        [NonSerialized] public bool invulnerable;
        [NonSerialized] public float minimumHealth;
        public float Current { get; private set; }
        bool phaseTransitionPending;
        bool awaitingFinalHit;
        bool rewardGranted;
        void GrantReward()
        {
            // 최종 사망만 한 번 지급합니다. 페이즈 전환과 구조물 파괴는 보상이 아닙니다.
            if(rewardGranted||player||practiceTarget)return;rewardGranted=true;
            if(RunEconomy.Instance)RunEconomy.Instance.Reward(this);
        }
        public bool Dead => Current <= 0 && !phaseTransitionPending;
        public event Action Died;
        float invulnerableUntil, restoreAt;
        float frostProtectionUntil;
        public bool FrostProtectionPaused {get;set;}
        public float FrostProtectionRemaining=>Dead?0:Mathf.Max(0,frostProtectionUntil-Time.time);
        public void GrantFrostProtection(float seconds)
        {if(Dead)return;frostProtectionUntil=Mathf.Max(Time.time,frostProtectionUntil)+Mathf.Max(0,seconds);}
        Renderer[] renderers;
        MaterialPropertyBlock block;
        float flashUntil;
        bool flashApplied;
        [SerializeField,Min(0),Tooltip("플레이어 피격 후 깜빡임과 무적이 함께 유지되는 시간")] private float playerHitInvulnerability=.7f;
        [SerializeField,Min(.03f)] private float playerBlinkInterval=.08f;
        Renderer[] blinkRenderers;
        bool[] previousHidden;
        bool blinking,blinkHidden;
        void Awake()
        {
            Current = maxHealth;
            CacheFlashRenderers();
        }
        public void RefreshVisuals() { CacheFlashRenderers(); }
        void CacheFlashRenderers()
        {
            // 피격 반짝임은 모델에만 적용합니다. 포자·범위 표시의 색과 알파는 유지합니다.
            var animator=GetComponentInChildren<Animator>();
            var candidates=animator?animator.GetComponentsInChildren<Renderer>(true):GetComponentsInChildren<Renderer>(true);
            var bodyRenderers=new System.Collections.Generic.List<Renderer>();
            foreach(var renderer in candidates)
            {
                if(!(renderer is SkinnedMeshRenderer)&&!(renderer is MeshRenderer))continue;
                if(renderer.GetComponentInParent<FallAttackVfx>())continue;
                bool opaque=true;
                foreach(var material in renderer.sharedMaterials)
                    if(material&&material.renderQueue>=2500){opaque=false;break;}
                if(opaque)bodyRenderers.Add(renderer);
            }
            renderers=bodyRenderers.ToArray();block=new MaterialPropertyBlock();
        }
        // 피해를 적용하고 사망 대상의 후처리를 실행합니다.
        public void Damage(float amount) { Damage(amount,false); }
        public void Damage(float amount,bool useFullPlayerDamage)
        {
            if (Dead || phaseTransitionPending || invulnerable || Time.time < invulnerableUntil || FrostProtectionRemaining>0 || amount <= 0) return;
            if (TryGetComponent<CombatDamageGate>(out var gate) && gate.isActiveAndEnabled)
            {if(gate.BlocksDamage(amount))return;amount=gate.FilterDamage(amount);}
            var motor = GetComponent<PlayerMotor>();
            if (motor && motor.IsDashing) return;
            // 발악을 공략한 보스는 피해량에 관계없이 다음 유효 타격 한 번으로 마무리됩니다.
            if(awaitingFinalHit)amount=Mathf.Max(amount,Current);
            // 플레이어는 공격 종류와 관계없이 유효한 피격 한 번에 하트 한 칸을 잃습니다.
            if (player && !useFullPlayerDamage) amount = 1;
            float beforeDamage=Current;
            Current = Mathf.Max(Mathf.Min(minimumHealth, Current), Current - amount);
            if(player&&Current<beforeDamage)GameAudio.Variant("Hurt",transform.position,.85f);
            flashUntil = Time.time + .12f;
            if (player)
            {
                invulnerableUntil=Time.time+playerHitInvulnerability;
                if(!Dead&&playerHitInvulnerability>0)BeginBlink();
            }
            if (!Dead) return;
            // 페이즈 전환은 Died 이벤트보다 먼저 처리해 방이 조기 클리어되지 않게 합니다.
            if (TryGetComponent<CombatPhaseTransition>(out var phase) && phase.isActiveAndEnabled && phase.TryBeginNextPhase())
            {
                phaseTransitionPending = true;
                return;
            }
            GrantReward();Died?.Invoke();
            if (practiceTarget) restoreAt = Time.time + 2;
            else if (!player) gameObject.SetActive(false);
        }
        public void Restore() { Current = maxHealth;phaseTransitionPending=false;awaitingFinalHit=false;invulnerableUntil=0;frostProtectionUntil=0;FrostProtectionPaused=false;EndBlink(); }
        public void ReleaseForFinalHit()
        {
            if(!phaseTransitionPending)return;
            phaseTransitionPending=false;Current=1;awaitingFinalHit=true;invulnerable=false;invulnerableUntil=0;
        }
        // 결정 획득 보호는 피격 무적과 합쳐 남은 시간 뒤에 누적합니다.
        public void AddInvulnerability(float seconds){if(Dead)return;invulnerableUntil=Mathf.Max(Time.time,invulnerableUntil)+Mathf.Max(0,seconds);if(player&&seconds>0)BeginBlink();}
        // 발악처럼 사망을 보류한 전투가 끝났을 때만 최종 사망 이벤트를 보냅니다.
        public void CompleteDeferredDeath(bool keepBody=false)
        {
            if(!phaseTransitionPending)return;
            phaseTransitionPending=false;invulnerable=false;Current=0;EndBlink();GrantReward();Died?.Invoke();
            // 연출 보스는 마지막 자세의 시체를 보존할 수 있습니다.
            if(!player&&!keepBody)gameObject.SetActive(false);
        }
        // 실패한 전장 전체 즉사 기믹 전용입니다. 일반 공격은 Damage를 사용합니다.
        public void FatalDamage(){if(Dead)return;Current=0;phaseTransitionPending=false;EndBlink();GrantReward();Died?.Invoke();if(!player)gameObject.SetActive(false);}
        void BeginBlink()
        {
            EndBlink();blinkRenderers=GetComponentsInChildren<Renderer>(true);
            previousHidden=new bool[blinkRenderers.Length];
            for(int i=0;i<blinkRenderers.Length;i++)previousHidden[i]=blinkRenderers[i].forceRenderingOff;
            blinking=true;blinkHidden=false;
        }
        void EndBlink()
        {
            if(!blinking)return;
            for(int i=0;i<blinkRenderers.Length;i++)if(blinkRenderers[i])blinkRenderers[i].forceRenderingOff=previousHidden[i];
            blinking=false;blinkHidden=false;
        }
        void UpdateBlink()
        {
            if(!blinking)return;
            if(Dead||Time.time>=invulnerableUntil){EndBlink();return;}
            bool hidden=Mathf.FloorToInt((invulnerableUntil-Time.time)/Mathf.Max(.03f,playerBlinkInterval))%2==0;
            if(hidden==blinkHidden)return;blinkHidden=hidden;
            for(int i=0;i<blinkRenderers.Length;i++)if(blinkRenderers[i])blinkRenderers[i].forceRenderingOff=previousHidden[i]||hidden;
        }
        void OnDisable(){EndBlink();invulnerableUntil=0;frostProtectionUntil=0;FrostProtectionPaused=false;}
        public void ApplyDot() { if (isActiveAndEnabled) StartCoroutine(Dot()); }
        // 1초 간격으로 세 번 지속 피해를 줍니다.
        System.Collections.IEnumerator Dot()
        {
            for (int i = 0; i < 3; i++) { yield return new WaitForSeconds(1); if (Dead) yield break; Damage(.5f); }
        }
        void Update()
        {
            // 수집 준비 시간에는 획득한 보호 시간을 소비하지 않습니다.
            if(FrostProtectionPaused&&frostProtectionUntil>Time.time-Time.deltaTime)frostProtectionUntil+=Time.deltaTime;
            // 실행 중 스크립트가 다시 로드돼도 비직렬화 캐시를 복구합니다.
            if(block==null||renderers==null)CacheFlashRenderers();
            UpdateBlink();
            if (practiceTarget && Dead && Time.time >= restoreAt) Restore();
            bool flashing=Time.time<flashUntil;
            if(flashing==flashApplied)return;
            flashApplied=flashing;
            if(flashing)block.SetColor("_Color",Color.white);
            foreach (var r in renderers)
            {
                if (!r) continue;
                if (flashing) r.SetPropertyBlock(block);
                else r.SetPropertyBlock(null);
            }
        }
    }
}
