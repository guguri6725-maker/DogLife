using UnityEngine;
namespace DogLife
{
    // 무기의 공격 방식에 따라 발사체와 근접 공격 처리를 구분합니다.
    public enum WeaponKind { Rifle, Shotgun, Sword, ChargeSword }
    [CreateAssetMenu(menuName = "DogLife/Weapon")]
    // 인스펙터에서 편집하는 무기별 수치와 외형 데이터를 보관합니다.
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
