using UnityEngine;
namespace DogLife
{
    // 전투방 활성화, 문 개방, 회복 지점과 스테이지 완료를 관리합니다.
    public sealed class StageOneDirector : MonoBehaviour
    {
        public GameObject[] encounters;
        public GameObject[] gates;
        public Transform exit;
        public Transform[] recoveryStations;
        public PrototypeHUD hud;
        public ForestRoomStreaming streaming;
        public string stageLabel = "FOREST 01", bossLabel = "FOREST GUARDIAN";
        public int Cleared { get; private set; }
        public bool Complete => hud.completed;
        CombatHealth[][] enemies;
        bool[] usedStations;
        bool ended;
        void Start()
        {
            hud.health.Died += EndRun;
            if (streaming) return;
            usedStations = new bool[recoveryStations.Length];
            enemies = new CombatHealth[encounters.Length][];
            for (int i = 0; i < encounters.Length; i++)
            {
                enemies[i] = encounters[i].GetComponentsInChildren<CombatHealth>(true);
                encounters[i].SetActive(i == 0);
            }
        }
        void OnDestroy() { if (hud && hud.health) hud.health.Died -= EndRun; }
        // 플레이어 사망이나 클리어 시 모든 전투 동작을 정지합니다.
        public void EndRun()
        {
            // 사망 이벤트와 클리어 처리가 겹쳐도 정리는 한 번만 실행합니다.
            if(ended)return;
            ended=true;
            hud.motor.MoveInput = Vector2.zero; hud.motor.readInput = false; hud.weapons.readInput = false;
            hud.motor.enabled = false; hud.weapons.enabled = false;
            var body = hud.motor.GetComponent<Rigidbody>(); body.linearVelocity = Vector3.zero; body.isKinematic = true;
            foreach (var enemy in FindObjectsByType<StageEnemy>(FindObjectsSortMode.None))
            {
                enemy.enabled = false;
                // NavMesh가 이동하는 적은 Kinematic이므로 물리 속도를 쓰지 않습니다.
                var enemyBody = enemy.GetComponent<Rigidbody>();
                if (!enemyBody.isKinematic) enemyBody.linearVelocity = Vector3.zero;
            }
            StopEnemies<SpringBoarBoss>(); StopEnemies<SpringBoarPhaseTwo>(); StopEnemies<HarvestReaperBoss>();
            StopEnemies<KrakenBoss>(); StopEnemies<KrakenPhaseOne>();
            StopEnemies<FrozenKnightBoss>();
            StopEnemies<BlossomSkeletonEnemy>(); StopEnemies<BlossomSpiritEnemy>();
            StopEnemies<FloralDeerEnemy>(); StopEnemies<SakuraRabbitEnemy>();
            StopEnemies<FallMonsterEnemy>(); StopEnemies<SummerMonsterEnemy>(); StopEnemies<WinterMonsterEnemy>();
            ClearHazards<WinterIcePatch>(); ClearHazards<SummerHomingBubble>();
            ClearHazards<CombatProjectile>();
        }
        // 적별 OnDisable에서 공격 판정과 효과를 정리하는 기존 동작을 유지합니다.
        static void StopEnemies<T>() where T : Behaviour
        { foreach(var enemy in FindObjectsByType<T>(FindObjectsSortMode.None))enemy.enabled=false; }
        static void ClearHazards<T>() where T : Component
        { foreach(var hazard in FindObjectsByType<T>(FindObjectsSortMode.None))Destroy(hazard.gameObject); }
        // 현재 방의 적 수를 확인해 다음 방과 출구를 순서대로 엽니다.
        void Update()
        {
            if (streaming) return;
            if (hud.health.Dead || Complete) return;
            if (Cleared < encounters.Length)
            {
                int remaining = 0;
                foreach (var hp in enemies[Cleared]) if (hp && !hp.Dead) remaining++;
                hud.objective = Cleared == encounters.Length - 1 ? $"{bossLabel}  /  Defeat the boss  /  HP {enemies[Cleared][0].Current:0}" : $"{stageLabel}  /  Encounter {Cleared + 1}/{encounters.Length - 1}  /  Enemies left: {remaining}";
                if (remaining == 0)
                {
                    var vines = gates[Cleared].GetComponentInChildren<VineGate>();
                    if (vines) vines.Open(); else gates[Cleared].SetActive(false);
                    Cleared++;
                    if (Cleared < encounters.Length) encounters[Cleared].SetActive(true);
                    else if(StoryBook.Instance)StoryBook.Instance.QueueClear(this);
                }
            }
            else
            {
                hud.objective = bossLabel + " DEFEATED  /  Reach the green exit";
                if (Vector3.Distance(hud.motor.transform.position, exit.position) < 2f)
                {
                    hud.completed = true; EndRun();
                }
            }
            for (int i = 0; i < recoveryStations.Length; i++)
                if (!usedStations[i] && Vector3.Distance(hud.motor.transform.position, recoveryStations[i].position) < 2f)
                {
                    usedStations[i] = true; hud.health.Restore(); recoveryStations[i].gameObject.SetActive(false);
                }
        }
        public void RecordStreamedClear(int count)
        {
            Cleared = count;
            if(streaming&&count==streaming.roomScenePaths.Length&&StoryBook.Instance)StoryBook.Instance.QueueClear(this);
        }
    }
}
