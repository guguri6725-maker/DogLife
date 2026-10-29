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
        // Hierarchy에 배치한 두 병을 저장된 상점 재고와 동기화합니다.
        public GameObject[] healingPotionDisplays=new GameObject[0];
        public Transform fusionFocus;
        // 튜토리얼은 메뉴 클릭이 아니라 실제 거래 성공을 확인합니다.
        public event System.Action WeaponPurchased, WeaponFused;
        [System.NonSerialized] public bool interactionLocked;
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
            RefreshPotionDisplays();
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
            {
                visuals[i]=Instantiate(stock.items[i].visualPrefab,displays[i],false);
                visuals[i].transform.localScale*=displayScale;
                PlaceOnDish(visuals[i],displays[i]);
            }
            RefreshPotionDisplays();
        }
        void RefreshPotionDisplays()
        {
            for(int i=0;i<healingPotionDisplays.Length;i++)
                if(healingPotionDisplays[i])healingPotionDisplays[i].SetActive(i<RemainingHealingPotions);
        }
        static void PlaceOnDish(GameObject item,Transform anchor)
        {
            // 무기마다 다른 피벗 대신 실제 메시의 바닥과 중심을 그릇에 맞춥니다.
            var renderers=item.GetComponentsInChildren<Renderer>().Where(r=>r is MeshRenderer||r is SkinnedMeshRenderer).ToArray();
            if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float width=Mathf.Max(bounds.size.x,bounds.size.z);
            if(width>1.05f)item.transform.localScale*=1.05f/width;
            bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            item.transform.position+=anchor.position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        }
        public bool InReach(PlayerInventory player)
        {return !interactionLocked&&player&&Vector3.Distance(player.transform.position,transform.position)<=interactionDistance;}
        public bool Buy(int index,RunEconomy wallet,out string message)
        {
            message="구매할 수 없는 상품입니다.";
            if(kind!=StationKind.Shop||stock==null||index<0||index>=stock.items.Length||!stock.items[index]||!wallet||!InReach(wallet.inventory))return false;
            if(!wallet.Spend(price)){message="골드가 부족합니다.";return false;}
            var item=stock.items[index];
            if(!wallet.inventory.AddWeapon(item)){wallet.Add(price);return false;}
            stock.items[index]=null;if(visuals[index])Destroy(visuals[index]);message=item.displayName+" 구매 완료";WeaponPurchased?.Invoke();return true;
        }
        public bool Fuse(string first,string second,RunEconomy wallet,out string message)
        { InventoryWeapon result;return Fuse(first,second,wallet,out message,out result); }
        public bool Fuse(string first,string second,RunEconomy wallet,out string message,out InventoryWeapon result)
        {
            result=null;
            message="일반 무기 두 개를 선택하세요. 다른 뼈끼리도 합성할 수 있습니다.";
            if(kind!=StationKind.Fusion||stock==null||Used||stock.fusing||!wallet||!InReach(wallet.inventory)){message="합성 횟수를 모두 사용했거나 너무 멉니다.";return false;}
            var a=wallet.inventory.FindInstance(first);var b=wallet.inventory.FindInstance(second);
            if(!a||!b||first==second||a.weaponForm!=InventoryWeapon.WeaponForm.Basic||b.weaponForm!=InventoryWeapon.WeaponForm.Basic)return false;
            result=ChooseFusionResult(a,b,catalog);
            if(!result){message="합성 결과 데이터가 연결되지 않았습니다.";return false;}
            // 인벤토리 이벤트의 재진입을 막고, 성공했을 때만 사용 횟수를 차감합니다.
            stock.fusing=true;
            try
            {
                if(!wallet.inventory.FuseInstances(first,second,result)){message="합성 재료를 다시 선택하세요.";return false;}
                stock.fusionCount++;
            }
            finally {stock.fusing=false;}
            message=result.displayName+" 합성 완료! 남은 합성 "+RemainingFusions+"회";WeaponFused?.Invoke();return true;
        }
        static InventoryWeapon ChooseFusionResult(InventoryWeapon a,InventoryWeapon b,WeaponCatalog source)
        {
            // 뼈 종류를 먼저 반반 추첨하므로 카탈로그의 무기 개수에 확률이 치우치지 않습니다.
            var origin=a.boneOrigin==b.boneOrigin?a.boneOrigin:
                (Random.Range(0,2)==0?InventoryWeapon.BoneOrigin.Dog:InventoryWeapon.BoneOrigin.Humanoid);
            bool sameCategory=a.weaponCategory==b.weaponCategory;
            var evolution=(a.boneOrigin==origin?a:b).sameWeaponEvolution;
            if(sameCategory&&evolution&&evolution.boneOrigin==origin&&
                evolution.weaponForm==InventoryWeapon.WeaponForm.Synthesized&&evolution.weaponCategory==a.weaponCategory)return evolution;
            // 같은 무기 종류는 해당 합성형, 서로 다른 무기 종류는 기존처럼 무작위 합성형입니다.
            var pool=source?source.weapons.Where(w=>w&&w.boneOrigin==origin&&
                w.weaponForm==InventoryWeapon.WeaponForm.Synthesized&&(!sameCategory||w.weaponCategory==a.weaponCategory)).ToArray():new InventoryWeapon[0];
            return pool.Length>0?pool[Random.Range(0,pool.Length)]:null;
        }
    }
}
