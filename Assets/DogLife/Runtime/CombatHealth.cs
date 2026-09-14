using System;
using UnityEngine;

namespace DogLife
{
    public sealed class CombatHealth : MonoBehaviour
    {
        public float maxHealth = 30;
        public bool player, practiceTarget;
        [NonSerialized] public bool invulnerable;
        [NonSerialized] public float minimumHealth;
        public float Current { get; private set; }
        public bool Dead => Current <= 0;
        public event Action Died;
        float invulnerableUntil, restoreAt;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        float flashUntil;
        void Awake() { Current = maxHealth; renderers = GetComponentsInChildren<Renderer>(); block = new MaterialPropertyBlock(); }
        public void Damage(float amount)
        {
            if (Dead || invulnerable || Time.time < invulnerableUntil || amount <= 0) return;
            var motor = GetComponent<SideViewMotor>();
            if (motor && motor.IsDashing) return;
            Current = Mathf.Max(Mathf.Min(minimumHealth, Current), Current - amount);
            flashUntil = Time.time + .12f;
            if (player) invulnerableUntil = Time.time + .7f;
            if (!Dead) return;
            Died?.Invoke();
            if (practiceTarget) restoreAt = Time.time + 2;
            else if (!player) gameObject.SetActive(false);
        }
        public void Restore() { Current = maxHealth; }
        public void ApplyDot() { if (isActiveAndEnabled) StartCoroutine(Dot()); }
        System.Collections.IEnumerator Dot()
        {
            for (int i = 0; i < 3; i++) { yield return new WaitForSeconds(1); if (Dead) yield break; Damage(.5f); }
        }
        void Update()
        {
            if (practiceTarget && Dead && Time.time >= restoreAt) Restore();
            foreach (var r in renderers)
            {
                if (!r) continue;
                if (Time.time < flashUntil) { block.SetColor("_Color", Color.white); r.SetPropertyBlock(block); }
                else r.SetPropertyBlock(null);
            }
        }
    }
}
