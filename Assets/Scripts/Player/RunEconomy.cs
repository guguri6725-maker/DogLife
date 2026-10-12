using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // 계절 이동으로 승인된 경우에만 소지품과 골드를 다음 씬에 전달합니다.
    public sealed class RunEconomy : MonoBehaviour
    {
        public PlayerInventory inventory;
        [SerializeField, Min(0)] int gold;
        [SerializeField, Min(0)] int monsterReward=10, bossReward=100;
        public int Gold=>gold;
        public static RunEconomy Instance {get;private set;}
        static List<InventoryWeapon> carried;
        static int carriedGold, equippedIndex;
        static float carriedKarma;
        static float carriedHealth,carriedMaxHealth,carriedDash;
        public static void ClearCarry(){carried=null;destination=null;}
        public void SetGold(int value){gold=Mathf.Max(0,value);}
        static string destination;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRun(){Instance=null;carried=null;destination=null;}
        void Awake()
        {
            Instance=this;
            // F5 재시작이나 타이틀에서 새로 입장하면 상품과 합성 사용 기록도 초기화합니다.
            if(carried==null||gameObject.scene.path!=destination)EconomyStation.ResetForNewRun();
        }
        void Start()
        {
            if(SeasonRunSave.Apply(this)){ClearCarry();SeasonRunSave.RecordEntry(this);return;}
            if(carried!=null&&gameObject.scene.path==destination)
            {
                gold=carriedGold;inventory.RestoreRunInventory(carried,equippedIndex);
                var karma=inventory.GetComponent<PlayerKarma>();if(karma)karma.SetValue(carriedKarma);
                var hp=inventory.GetComponent<CombatHealth>();if(hp&&carriedMaxHealth>0)hp.RestoreCheckpoint(carriedHealth,carriedMaxHealth);
                var motor=inventory.GetComponent<PlayerMotor>();if(motor)motor.dashCooldown=carriedDash;
            }
            carried=null;destination=null;
            SeasonRunSave.RecordEntry(this);
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
        public bool Spend(int amount){if(amount<0||gold<amount)return false;gold-=amount;return true;}
        public void Add(int amount){gold=(int)System.Math.Min(int.MaxValue,(long)gold+Mathf.Max(0,amount));}
        public void CarryTo(string scene)
        {
            if(!inventory)return;
            carried=new List<InventoryWeapon>(inventory.OwnedWeapons);carriedGold=gold;destination=scene;equippedIndex=-1;
            var karma=inventory.GetComponent<PlayerKarma>();carriedKarma=karma?karma.Value:0;
            var hp=inventory.GetComponent<CombatHealth>();carriedHealth=hp?hp.Current:5;carriedMaxHealth=hp?hp.maxHealth:5;
            var motor=inventory.GetComponent<PlayerMotor>();carriedDash=motor?motor.dashCooldown:1;
            for(int i=0;i<carried.Count;i++)if(inventory.GetInstanceId(i)==inventory.EquippedInstanceId)equippedIndex=i;
        }
        public void Reward(CombatHealth target)
        {
            if(!target||target.player||target.practiceTarget)return;
            bool boss=target.GetComponent<FrozenKnightBoss>()||target.GetComponent<SpringBoarBoss>()||target.GetComponent<KrakenBoss>()||target.GetComponent<HarvestReaperBoss>();
            var stage=target.GetComponent<StageEnemy>();boss|=stage&&stage.boss;
            // 얼음벽·훈련 더미처럼 체력만 있는 구조물은 처치 보상에서 제외합니다.
            bool enemy=boss||stage||target.GetComponent<FallMonsterEnemy>()||target.GetComponent<SummerMonsterEnemy>()||target.GetComponent<WinterMonsterEnemy>()||target.GetComponent<BlossomSkeletonEnemy>()||target.GetComponent<BlossomSpiritEnemy>()||target.GetComponent<FloralDeerEnemy>()||target.GetComponent<SakuraRabbitEnemy>();
            if(enemy)Add(boss?bossReward:monsterReward);
        }
    }
}
