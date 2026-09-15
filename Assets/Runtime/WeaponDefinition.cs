using UnityEngine;
namespace DogLife
{
    public enum WeaponKind { Rifle, Shotgun, Sword, ChargeSword }
    [CreateAssetMenu(menuName = "DogLife/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string displayName;
        public WeaponKind kind;
        public float damage = 3, cooldown = .2f, projectileSpeed = 30, range = 40;
        public int pellets = 1;
        public float spread = 0, meleeRange = 2, chargeSeconds = 5;
        public GameObject visualPrefab;
        public Color color = Color.yellow;
    }
}
