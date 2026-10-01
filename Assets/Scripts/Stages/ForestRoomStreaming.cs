using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife
{
    // 공통 씬은 계속 유지하고 방은 현재/다음 두 개만 비동기로 관리합니다.
    public sealed class ForestRoomStreaming : MonoBehaviour
    {
        // 방 씬들이 공유하는 관리자입니다. 계절 씬이 종료되면 함께 해제합니다.
        public static ForestRoomStreaming Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { Instance = null; }

        void Awake()
        {
            if(Instance && Instance != this)
            {
                Debug.LogWarning("중복 방 스트리밍 관리자를 비활성화했습니다.", this);
                enabled=false;
                return;
            }
            Instance=this;
        }
        public string[] roomScenePaths;
        public StageOneDirector director;
        // 계절 맵: 전투 중 현재 방만 유지하고, 클리어한 뒤에 다음 방을 준비합니다.
        public bool loadNextOnClear;
        [SerializeField, Tooltip("플레이 중 다음 방 로딩의 프레임당 처리량을 낮춥니다. 로딩 완료까지는 조금 더 걸릴 수 있습니다.")]
        private bool smoothRoomLoading = true;
        [SerializeField, Tooltip("방 인스턴스는 만들지 않고 시작 로딩에서 에셋 데이터만 준비합니다.")]
        private string[] geometryWarmupPaths = new string[0];
        // 스테이지가 끝날 때까지 데이터를 유지해 첫 로드의 큰 등록 비용을 전투 중 반복하지 않습니다.
        GameObject[] geometryAssets;
        ThreadPriority previousLoadingPriority;
        bool changedLoadingPriority;
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
            geometryAssets = new GameObject[geometryWarmupPaths.Length];
            for(int i=0;i<geometryWarmupPaths.Length;i++)
            {
                var request=Resources.LoadAsync<GameObject>(geometryWarmupPaths[i]);
                yield return request;
                geometryAssets[i]=request.asset as GameObject;
                if(!geometryAssets[i])
                {
                    Fail("Room geometry asset is missing: "+geometryWarmupPaths[i]);
                    yield break;
                }
                yield return null;
            }
            yield return LoadRoom(0);
            if (!loadNextOnClear && string.IsNullOrEmpty(LoadError) && rooms.Length > 1) yield return LoadRoom(1);
            if (!string.IsNullOrEmpty(LoadError)) yield break; // 바닥 없는 상태에서 입력을 풀지 않습니다.
            CurrentRoom = 0;
            if(rooms[0].rearSeal)rooms[0].rearSeal.SetActive(true);
            if(rooms[0].rearVines)rooms[0].rearVines.Close();
            rooms[0].BeginEncounter();
            body.isKinematic = wasKinematic;
            Hud.motor.enabled = motorEnabled; Hud.weapons.enabled = weaponsEnabled;
            Ready = true; Busy = false;
        }

        IEnumerator LoadRoom(int index)
        {
            if(Ready&&smoothRoomLoading)
            {
                previousLoadingPriority=Application.backgroundLoadingPriority;
                Application.backgroundLoadingPriority=ThreadPriority.Low;
                changedLoadingPriority=true;
            }
            try { yield return LoadRoomContent(index); }
            finally { RestoreLoadingPriority(); }
        }

        void RestoreLoadingPriority()
        {
            if(!changedLoadingPriority)return;
            if(Application.backgroundLoadingPriority==ThreadPriority.Low)
                Application.backgroundLoadingPriority=previousLoadingPriority;
            changedLoadingPriority=false;
        }

        void OnDestroy()
        {
            RestoreLoadingPriority();
            if(Instance==this)Instance=null;
        }

        IEnumerator LoadRoomContent(int index)
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
            else
            {
                yield return rooms[index].PrepareGeometry();
                if(!rooms[index].GeometryReady)
                {
                    Fail(rooms[index].GeometryError ?? "Room geometry preparation failed: " + path);
                    yield break;
                }
                // 씬 활성화/물리 등록과 몬스터 생성을 같은 프레임에 겹치지 않습니다.
                yield return null;
                var spawner=rooms[index].GetComponent<RoomMonsterSpawner>();
                if(spawner)
                {
                    yield return spawner.PrepareAsync();
                    if(!spawner.Prepared)Fail("Room monster spawn failed: "+path);
                    else rooms[index].BeginEncounter(false);
                }
            }
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
                    // 옆 공간은 현재 방 씬에 포함되어 메인 진행 순서를 바꾸지 않습니다.
                    foreach (var gate in room.sideVines) if (gate) gate.Open();
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
            // 문 닫힘 완료를 기다리지 않고 닫히기 시작한 순간부터 전투합니다.
            room.BeginEncounter();
            Physics.SyncTransforms();
            // 계절 방은 최초 스폰과 같은 5m 안쪽을 사용해 문턱의 거친 바닥을 피합니다.
            Hud.motor.SetRespawnPoint(new Vector3(room.entryX + (loadNextOnClear ? 5f : 4f), 2.5f, 0));
            var camera = Camera.main ? Camera.main.GetComponent<PlayerCamera>() : null;
            if (camera) camera.minimumWorldX = room.entryX + 1f;
            foreach (var projectile in FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
            if (room.rearVines)
                while (!room.rearVines.IsClosed) yield return null;
            cleared = false;
            // 해제가 완료된 후 로드하므로 순간적으로도 세 방을 상주시켜 놓지 않습니다.
            yield return SceneManager.UnloadSceneAsync(roomScenePaths[previous]);
            rooms[previous] = null;
            if (!loadNextOnClear && CurrentRoom + 1 < rooms.Length) yield return LoadRoom(CurrentRoom + 1);
            Busy = !string.IsNullOrEmpty(LoadError);
        }
    }
}
