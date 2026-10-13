using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private List<InventoryWeapon> ownedWeapons=new List<InventoryWeapon>();
        [SerializeField] private InventoryWeapon equippedWeapon;
        [SerializeField, HideInInspector] private List<string> instanceIds=new List<string>();
        [SerializeField, HideInInspector] private string equippedInstanceId;
        public string EquippedInstanceId { get { EnsureInstances();return equippedInstanceId; } }
        public string GetInstanceId(int index){EnsureInstances();return instanceIds[index];}
        void EnsureInstances()
        {
            while(instanceIds.Count<ownedWeapons.Count)instanceIds.Add(System.Guid.NewGuid().ToString("N"));
            if(instanceIds.Count>ownedWeapons.Count)instanceIds.RemoveRange(ownedWeapons.Count,instanceIds.Count-ownedWeapons.Count);
            if(equippedWeapon&&string.IsNullOrEmpty(equippedInstanceId))
            {int i=ownedWeapons.IndexOf(equippedWeapon);if(i>=0)equippedInstanceId=instanceIds[i];}
        }
        public IReadOnlyList<InventoryWeapon> OwnedWeapons => ownedWeapons;
        public InventoryWeapon FindInstance(string id){EnsureInstances();int i=instanceIds.IndexOf(id??"");return i>=0?ownedWeapons[i]:null;}
        // 합성은 두 인스턴스를 한 번에 교체해 UI가 중간 상태를 읽지 않게 합니다.
        public bool FuseInstances(string first,string second,InventoryWeapon result)
        {
            EnsureInstances();int a=instanceIds.IndexOf(first??""),b=instanceIds.IndexOf(second??"");
            if(a<0||b<0||a==b||!result)return false;
            bool replace=equippedInstanceId==first||equippedInstanceId==second;
            int high=Mathf.Max(a,b),low=Mathf.Min(a,b);ownedWeapons.RemoveAt(high);instanceIds.RemoveAt(high);ownedWeapons.RemoveAt(low);instanceIds.RemoveAt(low);
            ownedWeapons.Add(result);string id=System.Guid.NewGuid().ToString("N");instanceIds.Add(id);
            // 합성 결과는 가방에만 보관합니다. 장착 중인 재료를 소모했다면 장착을 해제합니다.
            if(replace){equippedWeapon=null;equippedInstanceId=null;Apply();}else Changed?.Invoke();return true;
        }
        public void RestoreRunInventory(IEnumerable<InventoryWeapon> items,int equippedIndex)
        {
            ownedWeapons=new List<InventoryWeapon>(items);instanceIds.Clear();equippedWeapon=null;equippedInstanceId=null;EnsureInstances();
            if(equippedIndex>=0&&equippedIndex<ownedWeapons.Count){equippedWeapon=ownedWeapons[equippedIndex];equippedInstanceId=instanceIds[equippedIndex];}Apply();
        }
        public InventoryWeapon EquippedWeapon => equippedWeapon;
        public event System.Action Changed;
        GameObject visual;
        public GameObject EquippedVisual=>visual;
        public void SetHeldVisible(bool value){if(visual)visual.SetActive(value);}
        void Start(){EnsureInstances();if(!ownedWeapons.Contains(equippedWeapon)){equippedWeapon=null;equippedInstanceId=null;}Apply();}
        public bool AddWeapon(InventoryWeapon item)
        {
            if(!item)return false;
            EnsureInstances();ownedWeapons.Add(item);instanceIds.Add(System.Guid.NewGuid().ToString("N"));Changed?.Invoke();return true;
        }
        public bool Equip(InventoryWeapon item)
        {
            int index=item?ownedWeapons.IndexOf(item):-1;
            return index>=0&&EquipInstance(GetInstanceId(index));
        }
        public bool EquipInstance(string id)
        {
            EnsureInstances();int index=instanceIds.IndexOf(id);if(index<0)return false;
            equippedWeapon=ownedWeapons[index];equippedInstanceId=id;Apply();return true;
        }
        public void Unequip(){equippedWeapon=null;equippedInstanceId=null;Apply();}
        public bool RemoveWeapon(InventoryWeapon item)
        {
            int index=item?ownedWeapons.IndexOf(item):-1;
            return index>=0&&RemoveInstance(GetInstanceId(index));
        }
        public bool RemoveInstance(string id)
        {
            EnsureInstances();int index=instanceIds.IndexOf(id);if(index<0)return false;
            ownedWeapons.RemoveAt(index);instanceIds.RemoveAt(index);
            if(equippedInstanceId==id){equippedWeapon=null;equippedInstanceId=null;Apply();}
            else Changed?.Invoke();
            return true;
        }
        void Apply()
        {
            var combo=GetComponent<BasicWeaponCombat>();if(combo)combo.Cancel();
            if(visual)Destroy(visual);
            var weapons=GetComponent<PlayerWeapons>();
            if(equippedWeapon&&equippedWeapon.visualPrefab&&weapons&&weapons.weaponSocket)
            {
                visual=Instantiate(equippedWeapon.visualPrefab,weapons.GetWeaponSocket(equippedWeapon));
                WeaponEquipmentVisuals.Decorate(visual,equippedWeapon);
                if(combo&&combo.BoomerangInFlight&&equippedWeapon.weaponCategory==InventoryWeapon.WeaponCategory.Boomerang)visual.SetActive(false);
            }
            Changed?.Invoke();
        }
    }
}

