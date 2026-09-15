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
        public string stageLabel = "FOREST 01", bossLabel = "FOREST GUARDIAN";
        public int Cleared { get; private set; }
        public bool Complete => hud.completed;
        CombatHealth[][] enemies;
        bool[] usedStations;
        void Start()
        {
            usedStations = new bool[recoveryStations.Length];
            enemies = new CombatHealth[encounters.Length][];
            for (int i = 0; i < encounters.Length; i++)
            {
                enemies[i] = encounters[i].GetComponentsInChildren<CombatHealth>(true);
                encounters[i].SetActive(i == 0);
            }
            hud.health.Died += EndRun;
        }
        void OnDestroy() { if (hud && hud.health) hud.health.Died -= EndRun; }
        // 플레이어 사망이나 클리어 시 모든 전투 동작을 정지합니다.
        void EndRun()
        {
            hud.motor.MoveInput = 0; hud.motor.readInput = false; hud.weapons.readInput = false;
            hud.motor.enabled = false; hud.weapons.enabled = false;
            var body = hud.motor.GetComponent<Rigidbody>(); body.linearVelocity = Vector3.zero; body.isKinematic = true;
            foreach (var enemy in FindObjectsByType<StageEnemy>(FindObjectsSortMode.None))
            {
                enemy.enabled = false;
                enemy.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            }
            foreach (var boss in FindObjectsByType<SeaBoss>(FindObjectsSortMode.None)) boss.enabled = false;
            foreach (var hazard in FindObjectsByType<SeaHazard>(FindObjectsSortMode.None)) Destroy(hazard.gameObject);
            foreach (var projectile in FindObjectsByType<SideViewProjectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
        }
        // 현재 방의 적 수를 확인해 다음 방과 출구를 순서대로 엽니다.
        void Update()
        {
            if (hud.health.Dead || Complete) return;
            if (Cleared < encounters.Length)
            {
                int remaining = 0;
                foreach (var hp in enemies[Cleared]) if (hp && !hp.Dead) remaining++;
                hud.objective = Cleared == encounters.Length - 1 ? $"{bossLabel}  /  Defeat the boss  /  HP {enemies[Cleared][0].Current:0}" : $"{stageLabel}  /  Encounter {Cleared + 1}/{encounters.Length - 1}  /  Enemies left: {remaining}";
                if (remaining == 0)
                {
                    gates[Cleared].SetActive(false);
                    Cleared++;
                    if (Cleared < encounters.Length) encounters[Cleared].SetActive(true);
                }
            }
            else
            {
                hud.objective = bossLabel + " DEFEATED  /  Reach the green exit";
                if (Mathf.Abs(hud.motor.transform.position.x - exit.position.x) < 1.5f)
                {
                    hud.completed = true; EndRun();
                }
            }
            for (int i = 0; i < recoveryStations.Length; i++)
                if (!usedStations[i] && Mathf.Abs(hud.motor.transform.position.x - recoveryStations[i].position.x) < 1.5f)
                {
                    usedStations[i] = true; hud.health.Restore(); recoveryStations[i].gameObject.SetActive(false);
                }
        }
    }
}
