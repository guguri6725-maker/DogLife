using UnityEngine;
namespace DogLife
{
    public sealed class StageOneDirector : MonoBehaviour
    {
        public GameObject[] encounters;
        public GameObject[] gates;
        public Transform exit;
        public Transform[] recoveryStations;
        public PrototypeHUD hud;
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
        void EndRun()
        {
            hud.motor.MoveInput = 0; hud.motor.readInput = false; hud.weapons.readInput = false;
            hud.motor.enabled = false; hud.weapons.enabled = false;
            var body = hud.motor.GetComponent<Rigidbody>(); body.linearVelocity = Vector3.zero; body.isKinematic = true;
            foreach (var enemy in FindObjectsByType<StageEnemy>(FindObjectsSortMode.None)) enemy.enabled = false;
        }
        void Update()
        {
            if (hud.health.Dead || Complete) return;
            if (Cleared < encounters.Length)
            {
                int remaining = 0;
                foreach (var hp in enemies[Cleared]) if (hp && !hp.Dead) remaining++;
                hud.objective = Cleared == 6 ? $"FOREST GUARDIAN  /  Defeat the boss  /  HP {enemies[6][0].Current:0}" : $"FOREST 01  /  Encounter {Cleared + 1}/6  /  Enemies left: {remaining}";
                if (remaining == 0)
                {
                    gates[Cleared].SetActive(false);
                    Cleared++;
                    if (Cleared < encounters.Length) encounters[Cleared].SetActive(true);
                }
            }
            else
            {
                hud.objective = "FOREST GUARDIAN DEFEATED  /  Reach the green exit";
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
