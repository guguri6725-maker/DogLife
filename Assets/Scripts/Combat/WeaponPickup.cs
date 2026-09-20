using UnityEngine;

namespace DogLife
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class WeaponPickup : MonoBehaviour
    {
        [SerializeField] private InventoryWeapon weapon;
        bool collected;
        public InventoryWeapon Weapon=>weapon;
        public bool TryCollect(PlayerInventory inventory)
        {
            if(collected||!weapon||!inventory)return false;
            var health=inventory.GetComponent<CombatHealth>();
            if(health&&health.Dead)return false;
            // Each pickup adds a separate instance, even for the same weapon data.
            if(!inventory.AddWeapon(weapon))return false;
            collected=true;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
        void OnTriggerEnter(Collider other){TryCollect(other.GetComponentInParent<PlayerInventory>());}
        void OnTriggerStay(Collider other){if(!collected)TryCollect(other.GetComponentInParent<PlayerInventory>());}
    }
}
