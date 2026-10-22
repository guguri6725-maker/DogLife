using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // 계절 이동으로 승인된 경우에만 소지품과 골드를 다음 씬에 전달합니다.
    public sealed class RunEconomy : MonoBehaviour
    {
        public PlayerInventory inventory;
        [SerializeField, Min(0)] int gold;
        // 일반 몬스터는 한 마리당 20G를 지급합니다.
        [SerializeField, Min(0)] int monsterReward=20, bossReward=100;
        public int Gold=>gold;
        [SerializeField, Min(0)] int healingPotions;
        public int HealingPotions=>healingPotions;
        [SerializeField, Min(0)] float healingPotionCooldown=5f;
        float nextHealingPotionAt;
        public float HealingPotionCooldownRemaining=>Mathf.Max(0,nextHealingPotionAt-Time.time);
        public void SetHealingPotions(int value){healingPotions=Mathf.Max(0,value);}
        // 구매는 보관만 하고, 실제 사용에 성공했을 때만 한 개를 소모합니다.
        public bool TryBuyHealingPotion(int price)
        {
            if(healingPotions==int.MaxValue||!Spend(price))return false;
            healingPotions++;return true;
        }
        public bool TryUseHealingPotion(out string message)
        {
            message="지금은 물약을 사용할 수 없습니다.";
            if(!inventory||BossCinematicUI.Hidden||StoryBook.BlocksInput||GameSettingsWindow.IsOpen||EconomyWindow.BlocksInput||SkillWindow.BlocksGameplayInput||SeasonLoadingScreen.IsLoading||Time.timeScale<=0)return false;
            var hp=inventory.GetComponent<CombatHealth>();
            if(!hp||hp.Dead||hp.RecoveryBlocked)return false;
            if(healingPotions<=0){message="회복 물약이 없습니다.";return false;}
            // 게임 시간 기준으로 재사용을 제한하므로 일시정지 중에는 쿨타임도 멈춥니다.
            if(HealingPotionCooldownRemaining>0){message="물약 재사용까지 "+Mathf.CeilToInt(HealingPotionCooldownRemaining)+"초";return false;}
            if(!hp.TryHeal(1)){message="체력이 가득 찼습니다.";return false;}
            healingPotions--;nextHealingPotionAt=Time.time+healingPotionCooldown;message="체력 1칸 회복";return true;
        }
        public static RunEconomy Instance {get;private set;}
        static List<InventoryWeapon> carried;
        static int carriedGold, equippedIndex,carriedActiveSlot,carriedPotions;
        static int[] carriedSlots;
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
                gold=carriedGold;healingPotions=carriedPotions;inventory.RestoreRunInventory(carried,equippedIndex,carriedSlots,carriedActiveSlot);
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
            carriedSlots=inventory.SlotIndices();carriedActiveSlot=inventory.ActiveSlot;
            carried=new List<InventoryWeapon>(inventory.OwnedWeapons);carriedGold=gold;destination=scene;equippedIndex=-1;
            carriedPotions=healingPotions;
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
