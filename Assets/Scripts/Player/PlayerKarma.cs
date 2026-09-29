using UnityEngine;

namespace DogLife
{
    public sealed class PlayerKarma : MonoBehaviour
    {
        [SerializeField, Min(1)] float maximum = 100;
        [SerializeField] float value;
        public float Value => value;
        public float Maximum => Mathf.Max(1, maximum);
        public float DarkRatio => Mathf.InverseLerp(-Maximum, Maximum, value);
        public float LightRatio => 1 - DarkRatio;
        public event System.Action<float> Changed;
        public void SetValue(float amount)
        {
            float previous = value;
            value = Mathf.Clamp(amount, -Maximum, Maximum);
            if (!Mathf.Approximately(previous, value)) Changed?.Invoke(value - previous);
        }
        public void Add(float amount) { SetValue(value + amount); }
        public void RecordDefeat(CombatHealth target, InventoryWeapon weapon)
        {
            if (!target || !target.Dead || target.player || !weapon) return;
            var enemy = target.GetComponent<StageEnemy>();
            bool boss = (enemy && enemy.boss) || target.GetComponent<FrozenKnightBoss>();
            Add(boss ? weapon.bossKillKarma : weapon.normalKillKarma);
        }
        void OnValidate() { maximum = Mathf.Max(1, maximum); SetValue(value); }
    }
}
