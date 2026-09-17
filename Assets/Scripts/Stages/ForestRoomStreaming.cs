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
        // 계절 맵: 전투 중 현재 방만 유지하고, 클리어한 뒤에 다음 방을 준비합니다.
        public bool loadNextOnClear;
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
            if (!loadNextOnClear && string.IsNullOrEmpty(LoadError) && rooms.Length > 1) yield return LoadRoom(1);
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
            // Inspector에서 미리 베이크한 방별 경로를 사용해 로딩 비용과 메시 읽기 메모리를 줄입니다.
            else if (rooms[index].navigation && !rooms[index].navigation.navMeshData)
                Fail("Room navigation has not been baked: " + path);
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
            // 이전 방 해제 중 연속 처치가 발생해도 세 번째 방 로딩을 동시에 시작하지 않습니다.
            if (Busy) return;
            var room = Current;
            if (!cleared)
            {
                int remaining = room.Enemies.Count(h => h && !h.Dead);
                Hud.objective = $"{director.stageLabel} / Room {CurrentRoom + 1}/{rooms.Length} / Enemies left: {remaining}";
                if (remaining == 0)
                {
                    cleared = true; director.RecordStreamedClear(CurrentRoom + 1);
                    if (loadNextOnClear && CurrentRoom + 1 < rooms.Length)
                        StartCoroutine(PrepareNext());
                }
            }
            if (cleared)
            {
                // 다음 씬이 준비되기 전에는 문을 열지 않습니다.
                bool nextReady = CurrentRoom == rooms.Length - 1 || rooms[CurrentRoom + 1];
                if (!Busy && nextReady)
                {
                    if (room.forwardVines) room.forwardVines.Open();
                    else room.forwardGate.SetActive(false);
                    Hud.objective = CurrentRoom == rooms.Length - 1 ? "GUARDIAN DEFEATED / Reach the green exit" : "ROOM CLEAR / Enter the next room";
                    bool passageOpen = !room.forwardVines || room.forwardVines.IsOpen;
                    // 계절 맵은 통로 중간이 아니라 다음 방 안으로 들어온 뒤 이전 방을 해제합니다.
                    float advanceX = loadNextOnClear && CurrentRoom + 1 < rooms.Length ? rooms[CurrentRoom + 1].entryX + 3f : room.exitX + 3f;
                    if (passageOpen && CurrentRoom < rooms.Length - 1 && Hud.motor.transform.position.x >= advanceX)
                        StartCoroutine(Advance());
                    else if (passageOpen && CurrentRoom == rooms.Length - 1 && room.exit && Vector3.Distance(Hud.motor.transform.position, room.exit.position) < 2f)
                    { Hud.completed = true; director.EndRun(); }
                }
                else Hud.objective = "ROOM CLEAR / Preparing next room...";
            }
            foreach (var station in room.recoveryStations)
                if (station && station.gameObject.activeSelf && Vector3.Distance(Hud.motor.transform.position, station.position) < 2f)
                { Hud.health.Restore(); station.gameObject.SetActive(false); }
        }

        IEnumerator PrepareNext()
        {
            Busy = true;
            yield return LoadRoom(CurrentRoom + 1);
            Busy = !string.IsNullOrEmpty(LoadError);
        }

        IEnumerator Advance()
        {
            Busy = true;
            int previous = CurrentRoom++;
            var room = Current;
            if (room.rearSeal) room.rearSeal.SetActive(true);
            // 뒤쪽 덩굴이 완전히 닫힌 후 이전 방을 해제하여 전환 순간을 가립니다.
            if (room.rearVines) room.rearVines.Close();
            Physics.SyncTransforms();
            Hud.motor.SetRespawnPoint(new Vector3(room.entryX + 4f, 2.5f, 0));
            var camera = Camera.main ? Camera.main.GetComponent<PlayerCamera>() : null;
            if (camera) camera.minimumWorldX = room.entryX + 1f;
            foreach (var projectile in FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
            foreach (var hazard in FindObjectsByType<SeaHazard>(FindObjectsSortMode.None)) Destroy(hazard.gameObject);
            if (room.rearVines)
                while (!room.rearVines.IsClosed) yield return null;
            cleared = false;
            room.BeginEncounter();
            // 해제가 완료된 후 로드하므로 순간적으로도 세 방을 상주시켜 놓지 않습니다.
            yield return SceneManager.UnloadSceneAsync(roomScenePaths[previous]);
            rooms[previous] = null;
            if (!loadNextOnClear && CurrentRoom + 1 < rooms.Length) yield return LoadRoom(CurrentRoom + 1);
            Busy = !string.IsNullOrEmpty(LoadError);
        }
    }
}
