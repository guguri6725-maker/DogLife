using UnityEngine;
namespace DogLife
{
    [CreateAssetMenu(menuName="DogLife/Weapon Catalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        public InventoryWeapon[] weapons;
    }
}
