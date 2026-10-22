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
        public Transform fusionFocus;
        [Min(0)] public int price=30;
        public const int HealingPotionPrice=50;
        public const int HealingPotionStock=2;
        public int RemainingHealingPotions=>stock==null||kind!=StationKind.Shop?0:Mathf.Max(0,HealingPotionStock-stock.potionsSold);
        // 구매 수량을 상점 재고에 저장하여 방 재방문으로 물약이 보충되지 않게 합니다.
        public bool BuyHealingPotion(RunEconomy wallet,out string message)
        {
            message="상점 가까이에서 구매하세요.";
            if(kind!=StationKind.Shop||stock==null||!wallet||!InReach(wallet.inventory))return false;
            if(RemainingHealingPotions==0){message="이 상점의 회복 물약은 품절입니다.";return false;}
            if(!wallet.TryBuyHealingPotion(HealingPotionPrice)){message="골드가 부족하거나 더 보관할 수 없습니다.";return false;}
            stock.potionsSold++;
            message="회복 물약 구매 완료 · Q로 사용 · 체력 1칸 회복";return true;
        }
        [Min(1)] public float interactionDistance=5;
        [Min(.01f)] public float displayScale=.65f;
        // 방을 다시 불러와도 성공한 합성 횟수를 유지합니다.
        const int FusionLimit=2;
        class Stock {public InventoryWeapon[] items;public int fusionCount,potionsSold;public bool fusing;}
        static Dictionary<string,Stock> saved=new Dictionary<string,Stock>();
        Stock stock;GameObject[] visuals;
        public int RemainingFusions=>Mathf.Max(0,FusionLimit-(stock==null?0:stock.fusionCount));
        public bool Used=>RemainingFusions==0;
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
        { InventoryWeapon result;return Fuse(first,second,wallet,out message,out result); }
        public bool Fuse(string first,string second,RunEconomy wallet,out string message,out InventoryWeapon result)
        {
            result=null;
            message="같은 계열의 일반 무기 두 개를 선택하세요.";
            if(kind!=StationKind.Fusion||stock==null||Used||stock.fusing||!wallet||!InReach(wallet.inventory)){message="합성 횟수를 모두 사용했거나 너무 멉니다.";return false;}
            var a=wallet.inventory.FindInstance(first);var b=wallet.inventory.FindInstance(second);
            if(!a||!b||first==second||a.weaponForm!=InventoryWeapon.WeaponForm.Basic||b.weaponForm!=InventoryWeapon.WeaponForm.Basic||a.boneOrigin!=b.boneOrigin)return false;
            result=a.weaponCategory==b.weaponCategory?a.sameWeaponEvolution:null;
            if(a.weaponCategory!=b.weaponCategory)
            {var pool=catalog?catalog.weapons.Where(w=>w&&w.boneOrigin==a.boneOrigin&&w.weaponForm==InventoryWeapon.WeaponForm.Synthesized).ToArray():new InventoryWeapon[0];if(pool.Length>0)result=pool[Random.Range(0,pool.Length)];}
            if(!result){message="합성 결과 데이터가 연결되지 않았습니다.";return false;}
            // 인벤토리 이벤트의 재진입을 막고, 성공했을 때만 사용 횟수를 차감합니다.
            stock.fusing=true;
            try
            {
                if(!wallet.inventory.FuseInstances(first,second,result)){message="합성 재료를 다시 선택하세요.";return false;}
                stock.fusionCount++;
            }
            finally {stock.fusing=false;}
            message=result.displayName+" 합성 완료! 남은 합성 "+RemainingFusions+"회";return true;
        }
    }
}
