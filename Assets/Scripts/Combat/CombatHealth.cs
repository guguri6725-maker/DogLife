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
        public bool Dead => Current <= 0 && !phaseTransitionPending;
        public event Action Died;
        float invulnerableUntil, restoreAt;
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
            if (Dead || phaseTransitionPending || invulnerable || Time.time < invulnerableUntil || amount <= 0) return;
            if (TryGetComponent<CombatDamageGate>(out var gate) && gate.isActiveAndEnabled && gate.BlocksDamage(amount)) return;
            var motor = GetComponent<PlayerMotor>();
            if (motor && motor.IsDashing) return;
            // 플레이어는 공격 종류와 관계없이 유효한 피격 한 번에 하트 한 칸을 잃습니다.
            if (player && !useFullPlayerDamage) amount = 1;
            Current = Mathf.Max(Mathf.Min(minimumHealth, Current), Current - amount);
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
            Died?.Invoke();
            if (practiceTarget) restoreAt = Time.time + 2;
            else if (!player) gameObject.SetActive(false);
        }
        public void Restore() { Current = maxHealth;phaseTransitionPending=false;invulnerableUntil=0;EndBlink(); }
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
        void OnDisable(){EndBlink();invulnerableUntil=0;}
        public void ApplyDot() { if (isActiveAndEnabled) StartCoroutine(Dot()); }
        // 1초 간격으로 세 번 지속 피해를 줍니다.
        System.Collections.IEnumerator Dot()
        {
            for (int i = 0; i < 3; i++) { yield return new WaitForSeconds(1); if (Dead) yield break; Damage(.5f); }
        }
        void Update()
        {
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
