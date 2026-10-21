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
        [SerializeField,HideInInspector] string[] quickSlotIds=new string[2];
        [SerializeField,HideInInspector] int activeSlot;
        [SerializeField,HideInInspector] bool slotsInitialized;
        public int ActiveSlot=>activeSlot;
        public string SlotInstanceId(int slot){EnsureInstances();return slot>=0&&slot<2?quickSlotIds[slot]:null;}
        public int[] SlotIndices(){EnsureInstances();return new[]{instanceIds.IndexOf(quickSlotIds[0]??""),instanceIds.IndexOf(quickSlotIds[1]??"")};}
        public string EquippedInstanceId { get { EnsureInstances();return equippedInstanceId; } }
        public string GetInstanceId(int index){EnsureInstances();return instanceIds[index];}
        void EnsureInstances()
        {
            while(instanceIds.Count<ownedWeapons.Count)instanceIds.Add(System.Guid.NewGuid().ToString("N"));
            if(instanceIds.Count>ownedWeapons.Count)instanceIds.RemoveRange(ownedWeapons.Count,instanceIds.Count-ownedWeapons.Count);
            if(equippedWeapon&&string.IsNullOrEmpty(equippedInstanceId))
            {int i=ownedWeapons.IndexOf(equippedWeapon);if(i>=0)equippedInstanceId=instanceIds[i];}
            if(quickSlotIds==null||quickSlotIds.Length!=2)quickSlotIds=new string[2];
            if(!slotsInitialized){quickSlotIds[0]=equippedInstanceId;activeSlot=0;slotsInitialized=true;}
            for(int n=0;n<2;n++)if(!instanceIds.Contains(quickSlotIds[n]??""))quickSlotIds[n]=null;
            activeSlot=Mathf.Clamp(activeSlot,0,1);
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
            EnsureInstances();
            if(replace){equippedWeapon=null;equippedInstanceId=null;Apply();}else Changed?.Invoke();return true;
        }
        public void RestoreRunInventory(IEnumerable<InventoryWeapon> items,int equippedIndex,int[] savedSlots=null,int savedActive=0)
        {
            ownedWeapons=new List<InventoryWeapon>(items);instanceIds.Clear();equippedWeapon=null;equippedInstanceId=null;quickSlotIds=new string[2];slotsInitialized=true;EnsureInstances();
            // 기존 저장 데이터는 현재 무기를 1번 칸으로 옮겨 호환합니다.
            var indices=savedSlots!=null&&savedSlots.Length==2?savedSlots:new[]{equippedIndex,-1};
            for(int n=0;n<2;n++)if(indices[n]>=0&&indices[n]<instanceIds.Count)quickSlotIds[n]=instanceIds[indices[n]];
            if(quickSlotIds[1]==quickSlotIds[0])quickSlotIds[1]=null;
            activeSlot=savedSlots==null?0:Mathf.Clamp(savedActive,0,1);SyncSlot();
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
            EnsureInstances();int assigned=System.Array.IndexOf(quickSlotIds,id);
            return assigned>=0?SelectSlot(assigned):AssignSlot(activeSlot,id);
        }
        // 같은 인스턴스를 두 칸에 복제하지 않고 슬롯 사이에서는 서로 교환합니다.
        public bool AssignSlot(int slot,string id)
        {
            EnsureInstances();if(slot<0||slot>1||!instanceIds.Contains(id??""))return false;
            int other=1-slot;
            if(quickSlotIds[other]==id)quickSlotIds[other]=quickSlotIds[slot];
            quickSlotIds[slot]=id;activeSlot=slot;SyncSlot();return true;
        }
        public bool SelectSlot(int slot)
        {
            EnsureInstances();if(slot<0||slot>1||string.IsNullOrEmpty(quickSlotIds[slot]))return false;
            if(activeSlot==slot&&equippedInstanceId==quickSlotIds[slot])return true;
            activeSlot=slot;SyncSlot();return true;
        }
        public void ClearSlot(int slot)
        {
            EnsureInstances();if(slot<0||slot>1)return;
            quickSlotIds[slot]=null;if(activeSlot==slot)SyncSlot();else Changed?.Invoke();
        }
        public void Unequip(){ClearSlot(activeSlot);}
        void SyncSlot()
        {
            equippedInstanceId=quickSlotIds[activeSlot];int index=instanceIds.IndexOf(equippedInstanceId??"");
            equippedWeapon=index>=0?ownedWeapons[index]:null;Apply();
        }
        public bool TryQuickSwap(int slot)
        {
            var health=GetComponent<CombatHealth>();var weapons=GetComponent<PlayerWeapons>();var motor=GetComponent<PlayerMotor>();
            if(Time.timeScale<=0||SkillWindow.BlocksGameplayInput||StoryBook.BlocksInput||BossCinematicUI.Hidden||
                (health&&health.Dead)||!weapons||!weapons.enabled||!weapons.readInput||(motor&&(!motor.enabled||!motor.readInput)))return false;
            return SelectSlot(slot);
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Keypad1))TryQuickSwap(0);
            else if(Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.Keypad2))TryQuickSwap(1);
        }
        public bool RemoveWeapon(InventoryWeapon item)
        {
            int index=item?ownedWeapons.IndexOf(item):-1;
            return index>=0&&RemoveInstance(GetInstanceId(index));
        }
        public bool RemoveInstance(string id)
        {
            EnsureInstances();int index=instanceIds.IndexOf(id);if(index<0)return false;
            ownedWeapons.RemoveAt(index);instanceIds.RemoveAt(index);EnsureInstances();
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

