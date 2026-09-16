using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife
{
    // 공통 씬은 계속 유지하고 방은 현재/다음 두 개만 비동기로 관리합니다.
    public sealed class ForestRoomStreaming : MonoBehaviour
    {
        public string[] roomScenePaths;
        public StageOneDirector director;
        public int CurrentRoom { get; private set; }
        public bool Ready { get; private set; }
        public bool Busy { get; private set; }
        public string LoadError { get; private set; }
        public ForestRoom Current => rooms == null ? null : rooms[CurrentRoom];
        ForestRoom[] rooms;
        bool cleared;
        PrototypeHUD Hud => director.hud;

        IEnumerator Start()
        {
            rooms = new ForestRoom[roomScenePaths.Length];
            Busy = true;
            var body = Hud.motor.GetComponent<Rigidbody>();
            bool wasKinematic = body.isKinematic, motorEnabled = Hud.motor.enabled, weaponsEnabled = Hud.weapons.enabled;
            Hud.motor.enabled = false; Hud.weapons.enabled = false;
            body.linearVelocity = Vector3.zero; body.isKinematic = true;
            Hud.objective = "Loading forest rooms...";
            yield return LoadRoom(0);
            if (string.IsNullOrEmpty(LoadError) && rooms.Length > 1) yield return LoadRoom(1);
            if (!string.IsNullOrEmpty(LoadError)) yield break; // 바닥 없는 상태에서 입력을 풀지 않습니다.
            CurrentRoom = 0;
            rooms[0].BeginEncounter();
            body.isKinematic = wasKinematic;
            Hud.motor.enabled = motorEnabled; Hud.weapons.enabled = weaponsEnabled;
            Ready = true; Busy = false;
        }

        IEnumerator LoadRoom(int index)
        {
            string path = roomScenePaths[index];
            if (!Application.CanStreamedLevelBeLoaded(path))
            {
                Fail("Room scene is missing from Build Settings: " + path); yield break;
            }
            var scene = SceneManager.GetSceneByPath(path);
            if (!scene.isLoaded)
            {
                var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
                if (operation == null) { Fail("Could not load room: " + path); yield break; }
                yield return operation;
                scene = SceneManager.GetSceneByPath(path);
            }
            rooms[index] = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ForestRoom>(true)).SingleOrDefault();
            if (!rooms[index] || rooms[index].index != index) Fail("Room configuration is invalid: " + path);
        }

        void Fail(string message)
        {
            LoadError = message; Busy = true;
            Hud.objective = "Room loading failed / F5: Restart";
            Debug.LogError(message, this);
        }

        void Update()
        {
            if (!Ready || !string.IsNullOrEmpty(LoadError) || Hud.health.Dead || Hud.completed) return;
            var room = Current;
            if (!cleared)
            {
                int remaining = room.Enemies.Count(h => h && !h.Dead);
                Hud.objective = $"FOREST / Room {CurrentRoom + 1}/{rooms.Length} / Enemies left: {remaining}";
                if (remaining == 0) { cleared = true; director.RecordStreamedClear(CurrentRoom + 1); }
            }
            if (cleared)
            {
                // 다음 씬이 준비되기 전에는 문을 열지 않습니다.
                bool nextReady = CurrentRoom == rooms.Length - 1 || rooms[CurrentRoom + 1];
                if (!Busy && nextReady)
                {
                    room.forwardGate.SetActive(false);
                    Hud.objective = CurrentRoom == rooms.Length - 1 ? "GUARDIAN DEFEATED / Reach the green exit" : "ROOM CLEAR / Enter the next room";
                    if (CurrentRoom < rooms.Length - 1 && Hud.motor.transform.position.x >= room.exitX + 3f)
                        StartCoroutine(Advance());
                    else if (room.exit && Vector3.Distance(Hud.motor.transform.position, room.exit.position) < 2f)
                    { Hud.completed = true; director.EndRun(); }
                }
                else Hud.objective = "ROOM CLEAR / Preparing next room...";
            }
            foreach (var station in room.recoveryStations)
                if (station && station.gameObject.activeSelf && Vector3.Distance(Hud.motor.transform.position, station.position) < 2f)
                { Hud.health.Restore(); station.gameObject.SetActive(false); }
        }

        IEnumerator Advance()
        {
            Busy = true;
            int previous = CurrentRoom++;
            var room = Current;
            room.rearSeal.SetActive(true); // 시야를 막은 다음 이전 방을 제거합니다.
            Physics.SyncTransforms();
            Hud.motor.SetRespawnPoint(new Vector3(room.entryX + 4f, 2.5f, 0));
            var camera = Camera.main ? Camera.main.GetComponent<PlayerCamera>() : null;
            if (camera) camera.minimumWorldX = room.entryX + 1f;
            foreach (var projectile in FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
            foreach (var hazard in FindObjectsByType<SeaHazard>(FindObjectsSortMode.None)) Destroy(hazard.gameObject);
            cleared = false;
            room.BeginEncounter();
            // 해제가 완료된 후 로드하므로 순간적으로도 세 방을 상주시켜 놓지 않습니다.
            yield return SceneManager.UnloadSceneAsync(roomScenePaths[previous]);
            rooms[previous] = null;
            if (CurrentRoom + 1 < rooms.Length) yield return LoadRoom(CurrentRoom + 1);
            Busy = !string.IsNullOrEmpty(LoadError);
        }
    }
}
