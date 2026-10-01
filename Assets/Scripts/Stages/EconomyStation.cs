using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DogLife
{
    // 진열 위치와 UI는 Hierarchy에서 제작하고, 실행 중에는 상품 프리팹만 배치합니다.
    public sealed class EconomyStation : MonoBehaviour
    {
        public enum StationKind {Shop,Fusion}
        public StationKind kind;
        public WeaponCatalog catalog;
        public Transform[] displays=new Transform[0];
        [Min(0)] public int price=30;
        [Min(1)] public float interactionDistance=5;
        [Min(.01f)] public float displayScale=.65f;
        class Stock {public InventoryWeapon[] items;public bool used;}
        static Dictionary<string,Stock> saved=new Dictionary<string,Stock>();
        Stock stock;GameObject[] visuals;
        public bool Used=>stock!=null&&stock.used;
        public InventoryWeapon[] Items=>stock==null?new InventoryWeapon[0]:stock.items;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStock(){saved.Clear();}
        public static void ResetForNewRun(){saved.Clear();}
        void Start()
        {
            string key=gameObject.scene.path+"/"+name;
            if(!saved.TryGetValue(key,out stock))
            {
                stock=new Stock{items=new InventoryWeapon[kind==StationKind.Shop?displays.Length:0]};
                var pool=catalog?catalog.weapons.Where(w=>w&&w.weaponForm==InventoryWeapon.WeaponForm.Basic).ToArray():new InventoryWeapon[0];
                for(int i=0;i<stock.items.Length;i++)if(pool.Length>0)stock.items[i]=pool[Random.Range(0,pool.Length)];
                saved[key]=stock;
            }
            visuals=new GameObject[stock.items.Length];
            for(int i=0;i<visuals.Length;i++)if(stock.items[i]&&stock.items[i].visualPrefab&&displays[i])
            {visuals[i]=Instantiate(stock.items[i].visualPrefab,displays[i],false);visuals[i].transform.localScale*=displayScale;}
        }
        public bool InReach(PlayerInventory player)
        {return player&&Vector3.Distance(player.transform.position,transform.position)<=interactionDistance;}
        public bool Buy(int index,RunEconomy wallet,out string message)
        {
            message="구매할 수 없는 상품입니다.";
            if(kind!=StationKind.Shop||stock==null||index<0||index>=stock.items.Length||!stock.items[index]||!wallet||!InReach(wallet.inventory))return false;
            if(!wallet.Spend(price)){message="골드가 부족합니다.";return false;}
            var item=stock.items[index];
            if(!wallet.inventory.AddWeapon(item)){wallet.Add(price);return false;}
            stock.items[index]=null;if(visuals[index])Destroy(visuals[index]);message=item.displayName+" 구매 완료";return true;
        }
        public bool Fuse(string first,string second,RunEconomy wallet,out string message)
        {
            message="같은 계열의 일반 무기 두 개를 선택하세요.";
            if(kind!=StationKind.Fusion||stock==null||stock.used||!wallet||!InReach(wallet.inventory)){message="이 합성방은 이미 사용했거나 너무 멉니다.";return false;}
            var a=wallet.inventory.FindInstance(first);var b=wallet.inventory.FindInstance(second);
            if(!a||!b||first==second||a.weaponForm!=InventoryWeapon.WeaponForm.Basic||b.weaponForm!=InventoryWeapon.WeaponForm.Basic||a.boneOrigin!=b.boneOrigin)return false;
            InventoryWeapon result=a.weaponCategory==b.weaponCategory?a.sameWeaponEvolution:null;
            if(a.weaponCategory!=b.weaponCategory)
            {var pool=catalog.weapons.Where(w=>w&&w.boneOrigin==a.boneOrigin&&w.weaponForm==InventoryWeapon.WeaponForm.Synthesized).ToArray();if(pool.Length>0)result=pool[Random.Range(0,pool.Length)];}
            if(!result||!wallet.inventory.FuseInstances(first,second,result)){message="합성 결과 데이터가 연결되지 않았습니다.";return false;}
            stock.used=true;message=result.displayName+" 합성 완료! 이 방의 합성을 사용했습니다.";return true;
        }
    }
}
