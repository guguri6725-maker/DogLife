using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife
{
    // 통로의 가려진 구간에서 방을 교체하며 방 하나만 메모리에 유지합니다.
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
        // 계절 맵: 클리어한 뒤에 다음 방을 준비합니다.
        public bool loadNextOnClear;
        [SerializeField] private SeasonPassage passagePrefab;
        [SerializeField] private Vector3[] passagePositions = new Vector3[0];
        SeasonPassage[] activePassages;
        bool UsesPassages => passagePrefab && passagePositions.Length == roomScenePaths.Length-1 && passagePositions.Length>0;
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
        // 방 구조물은 해제하고 클리어/회복 상태와 소량의 미수집 아이템만 보관합니다.
        sealed class Visit
        {
            public bool cleared,captured;
            public bool[] stations;
            public readonly System.Collections.Generic.List<GameObject> pickups=new System.Collections.Generic.List<GameObject>();
        }
        Visit[] visits;
        SeasonPassage transit;
        int loadedRoom=-1,departedRoom=-1;
        float initialCameraLimit = float.NegativeInfinity;
        // CurrentRoom은 현재 로드된 방이며 진행도는 director.Cleared에 별도로 보존합니다.
        public int OccupiedRoom { get; private set; }
        PrototypeHUD Hud => director.hud;

        IEnumerator Start()
        {
            rooms = new ForestRoom[roomScenePaths.Length];
            visits=Enumerable.Range(0,rooms.Length).Select(i=>new Visit()).ToArray();
            var initialCamera=Camera.main?Camera.main.GetComponent<PlayerCamera>():null;
            if(initialCamera)initialCameraLimit=initialCamera.minimumWorldX;
            if(UsesPassages){activePassages=new SeasonPassage[passagePositions.Length];KeepNearbyPassages(0);}
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
            if (!UsesPassages && !loadNextOnClear && string.IsNullOrEmpty(LoadError) && rooms.Length > 1) yield return LoadRoom(1);
            if (!string.IsNullOrEmpty(LoadError)) yield break; // 바닥 없는 상태에서 입력을 풀지 않습니다.
            CurrentRoom = loadedRoom = 0;
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
                if(visits[index].cleared)
                {
                    rooms[index].RestoreClear();
                    rooms[index].PrepareDoorsForEntry(index>0);
                    RestoreVisit(index);
                    yield break;
                }
                rooms[index].PrepareDoorsForEntry(index>0);
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
            // 임시 테스트 단축키: 일반 빌드에서도 사용합니다.
            if(CampaignBuildMode.ExhibitionEnabled&&Input.GetKeyDown(KeyCode.L)&&Time.timeScale>0&&!SkillWindow.BlocksGameplayInput)
                RequestBossTestJump();
            if (!Ready || !string.IsNullOrEmpty(LoadError) || Hud.health.Dead || Hud.completed) return;
            // 다음 방 준비 중에는 중복 로딩과 전투 시작을 방지합니다.
            if (Busy) return;
            if(UsesPassages){TickPassageRooms();return;}
            var room = Current;
            for(int i=0;i<rooms.Length;i++)
                if(rooms[i]&&Hud.motor.transform.position.x>=rooms[i].entryX&&Hud.motor.transform.position.x<=rooms[i].exitX)OccupiedRoom=i;
            // 통로 안에서는 현재 방이 없는 구간도 정상 상태입니다.
            if(!room)
            {
                Hud.objective="다음 방으로 이동";
                if(UsesPassages&&CurrentRoom+1<rooms.Length&&IsRoomPrepared(CurrentRoom+1)&&
                    Hud.motor.transform.position.x>=rooms[CurrentRoom+1].entryX+3f)StartCoroutine(Advance());
                return;
            }
            if (!cleared)
            {
                int remaining = room.Enemies.Count(h => h && !h.Dead);
                Hud.objective = $"{director.stageLabel} / Room {CurrentRoom + 1}/{rooms.Length} / Enemies left: {remaining}";
                if (remaining == 0)
                {
                    cleared = true; room.MarkCleared(); director.RecordStreamedClear(CurrentRoom + 1);
                    if (!UsesPassages && loadNextOnClear && CurrentRoom + 1 < rooms.Length)
                        StartCoroutine(PrepareNext());
                }
            }
            RecoverAtVisitedStations();
            if (cleared)
            {
                if(CurrentRoom==rooms.Length-1)
                {
                    // 보스 사망 연출과 카메라 복귀가 끝나면 귀환 통로도 개방합니다.
                    if(!BossCinematicUI.Hidden)OpenReturnRoute(room);
                    Hud.objective="보스 처치 / 빛나는 추억에 다가가 F키로 상호작용";
                    return;
                }
                // 통로 모드에서는 방 클리어 즉시 출구를 열고, 안쪽 석문이 로딩을 보호합니다.
                bool nextReady = UsesPassages || CurrentRoom == rooms.Length - 1 || rooms[CurrentRoom + 1];
                if (!Busy && nextReady)
                {
                    OpenReturnRoute(room);
                    Hud.objective = "ROOM CLEAR / Enter the next room";
                    bool passageOpen = !room.forwardVines || room.forwardVines.IsOpen;
                    // 기존 씬의 전환도 지원하며, 새 통로 모드는 별도 트리거에서 처리합니다.
                    float advanceX = UsesPassages ? (IsRoomPrepared(CurrentRoom+1)?rooms[CurrentRoom+1].entryX+3f:float.PositiveInfinity) : loadNextOnClear && CurrentRoom + 1 < rooms.Length ? rooms[CurrentRoom + 1].entryX + 3f : room.exitX + 3f;
                    if (passageOpen && CurrentRoom < rooms.Length - 1 && Hud.motor.transform.position.x >= advanceX)
                        StartCoroutine(Advance());
                }
                else Hud.objective = "ROOM CLEAR / Preparing next room...";
            }
        }

        void RecoverAtVisitedStations()
        {
            foreach(var visited in rooms.Where(r=>r&&r.CombatStarted))
            foreach (var station in visited.recoveryStations)
                if (station && station.gameObject.activeSelf && Vector3.Distance(Hud.motor.transform.position, station.position) < 2f)
                { Hud.health.Restore(); station.gameObject.SetActive(false); }
        }

        void OpenReturnRoute(ForestRoom room)
        {
            // 보스 바로가기 테스트처럼 이전 방이 없는 경우에는 빈 공간으로 열지 않습니다.
            bool hasPrevious=CurrentRoom>0&&rooms[CurrentRoom-1];
            room.OpenClearedDoors(hasPrevious);
            if(UsesPassages&&hasPrevious&&activePassages[CurrentRoom-1])activePassages[CurrentRoom-1].OpenRear();
            var camera=Camera.main?Camera.main.GetComponent<PlayerCamera>():null;
            if(camera&&(CurrentRoom==0||hasPrevious))camera.minimumWorldX=initialCameraLimit;
        }

        IEnumerator PrepareNext()
        {
            Busy = true;
            yield return LoadRoom(CurrentRoom + 1);
            Busy = !string.IsNullOrEmpty(LoadError);
        }

        public bool IsRoomPrepared(int index)=>rooms!=null&&index>=0&&index<rooms.Length&&rooms[index]&&rooms[index].GeometryReady&&!Busy;
        void KeepNearbyPassages(int roomIndex)
        {
            if(!UsesPassages)return;
            for(int i=0;i<activePassages.Length;i++)
            {
                bool needed=i==roomIndex-1||i==roomIndex;
                // 현재 방 양쪽의 통로만 유지합니다.
                if(needed&&!activePassages[i])
                {
                    activePassages[i]=Instantiate(passagePrefab,passagePositions[i],Quaternion.identity);
                    activePassages[i].Configure(i);
                    activePassages[i].SetRoomOpen(roomIndex,true);
                }
                else if(!needed&&activePassages[i])
                {
                    // Destroy 대기 프레임에도 먼 통로가 렌더링/충돌하지 않게 먼저 끕니다.
                    activePassages[i].gameObject.SetActive(false);
                    Destroy(activePassages[i].gameObject);activePassages[i]=null;
                }
            }
        }
        // 이전 트리거 API는 유지하되 실제 교체 방향은 플레이어 위치로 결정합니다.
        public bool TryReleaseAtPassage(SeasonPassage passage) { TickPassage(passage);return transit==passage; }
        public void TryPrepareAtPassage(SeasonPassage passage) { TickPassage(passage); }
        public bool WasCleared(int index)=>visits!=null&&index>=0&&index<visits.Length&&visits[index].cleared;
        public int LoadedRoomCount=>rooms==null?0:rooms.Count(r=>r);
        public bool InPassage=>transit;
        // 방을 해제한 통로에서도 지도에 실제 연결 구간을 표시합니다.
        public int MapPassageIndex => transit ? transit.previousRoomIndex : -1;

        void TickPassageRooms()
        {
            if(transit)
            {
                if(loadedRoom>=0)
                {
                    var destination=rooms[loadedRoom];float x=Hud.motor.transform.position.x;
                    bool inside=loadedRoom==transit.previousRoomIndex?x<=destination.exitX-3:x>=destination.entryX+3;
                    if(inside)
                    {
                        transit=null;OccupiedRoom=CurrentRoom=loadedRoom;KeepNearbyPassages(loadedRoom);
                        if(!destination.Cleared)
                        {
                            if(destination.rearSeal)destination.rearSeal.SetActive(true);
                            if(destination.rearVines)destination.rearVines.Close();
                            destination.BeginEncounter();
                        }
                        SetCameraLimits(destination.Cleared?initialCameraLimit:destination.entryX+1,float.PositiveInfinity);
                        Hud.motor.SetRespawnPoint(new Vector3(destination.entryX+5,2.5f,0));
                    }
                }
                Hud.objective="통로 이동 / 앞뒤 방으로 이동 가능";
                return;
            }
            var room=Current;if(!room)return;
            OccupiedRoom=CurrentRoom;
            if(!room.Cleared&&room.CombatStarted&&room.Enemies!=null&&room.Enemies.All(h=>!h||h.Dead))
            {
                room.MarkCleared();visits[CurrentRoom].cleared=true;
                director.RecordStreamedClear(Mathf.Max(director.Cleared,CurrentRoom+1));
            }
            RecoverAtVisitedStations();
            if(!room.Cleared)
            {
                Hud.objective=$"{director.stageLabel} / Room {CurrentRoom+1}/{rooms.Length} / Enemies left: {room.Enemies?.Count(h=>h&&!h.Dead)??0}";
                return;
            }
            if(BossCinematicUI.Hidden)return;
            room.OpenClearedDoors(WasCleared(CurrentRoom-1));
            SetCameraLimits(initialCameraLimit,float.PositiveInfinity);
            Hud.objective=CurrentRoom==rooms.Length-1?"보스 처치 / 빛나는 추억에 다가가 F키로 상호작용":"ROOM CLEAR / 이전 방 또는 다음 방으로 이동";
        }
        void SetCameraLimits(float min,float max)
        {
            var camera=Camera.main?Camera.main.GetComponent<PlayerCamera>():null;
            if(camera){camera.minimumWorldX=min;camera.maximumWorldX=max;}
        }
        public void TickPassage(SeasonPassage passage)
        {
            if(!UsesPassages||!Ready||Busy||Hud.health.Dead||Hud.completed||!string.IsNullOrEmpty(LoadError)||BossCinematicUI.Hidden)return;
            int left=passage.previousRoomIndex,right=left+1;
            if(transit&&transit!=passage)return;
            float x=Hud.motor.transform.position.x;
            if(loadedRoom>=0)
            {
                if(loadedRoom!=left&&loadedRoom!=right)return;
                var room=rooms[loadedRoom];
                if(!room.Cleared&&room.CombatStarted)return;
                // 추억 생성 코루틴이 보스 위치를 읽기 전에 방을 해제하지 않습니다.
                if(loadedRoom==rooms.Length-1&&StoryBook.Instance&&!FindFirstObjectByType<BossMemoryPickup>())return;
                bool fromLeft=loadedRoom==left;
                // 플레이어와 카메라가 모두 굽이 안에 들어온 뒤 양쪽 문을 닫습니다.
                if(!passage.CanRelease(fromLeft,x))return;
                if(transit)
                {
                    // 방을 불러온 뒤 반대쪽으로 되돌아선 경우에만 다시 교체합니다.
                    int desired=passage.DesiredRoom(x);
                    if(desired<0||desired==loadedRoom)return;
                }
                transit=passage;StartCoroutine(ReleaseRoom(passage,loadedRoom));
            }
            else if(transit==passage)
            {
                // 출발 쪽 굽이에서 즉시 같은 방을 다시 로드하지 않고 이동 의도를 기다립니다.
                float local=x-passage.transform.position.x;
                int target=departedRoom==left?(local>9.6f?right:local<6.4f?left:-1):(local<8.4f?left:local>11.6f?right:-1);
                if(target<0||target>=rooms.Length)return;
                if(target==left&&!WasCleared(target))return;
                StartCoroutine(PrepareDestination(passage,target));
            }
        }
        IEnumerator ReleaseRoom(SeasonPassage passage,int index)
        {
            Busy=true;departedRoom=index;passage.CloseRear();passage.CloseForward();
            while(!passage.IsRearClosed||!passage.IsForwardClosed)yield return null;
            // 닫히는 동안 플레이어가 방으로 되돌아갔다면 해제를 취소합니다.
            if(!passage.PlayerInside(Hud.motor.transform.position.x))
            {passage.SetRoomOpen(index,true);transit=null;Busy=false;yield break;}
            SetCameraLimits(passage.LeftLimit,passage.RightLimit);
            CaptureVisit(index);
            foreach(var projectile in FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))Destroy(projectile.gameObject);
            yield return SceneManager.UnloadSceneAsync(roomScenePaths[index]);
            rooms[index]=null;loadedRoom=-1;OccupiedRoom=-1;
            // 이 프레임에는 방 없이 현재 통로만 남습니다.
            for(int i=0;i<activePassages.Length;i++)if(activePassages[i]&&activePassages[i]!=passage)
            {Destroy(activePassages[i].gameObject);activePassages[i]=null;}
            Hud.motor.SetRespawnPoint(Hud.motor.transform.position+Vector3.up*.2f);
            yield return null;Busy=false;
        }
        IEnumerator PrepareDestination(SeasonPassage passage,int index)
        {
            Busy=true;
            yield return LoadRoom(index);
            if(!string.IsNullOrEmpty(LoadError))yield break;
            loadedRoom=CurrentRoom=index;cleared=rooms[index].Cleared;
            // 방 내부에 진입하기 전 양쪽 통로부터 복원해야 반대편 출구에 빈 공간이 보이지 않습니다.
            KeepNearbyPassages(index);
            yield return null;
            // 목적지 방과 양쪽 통로 준비가 완료된 뒤 해당 방향 문만 엽니다.
            rooms[index].PrepareDoorsForEntry(index>0);
            passage.SetRoomOpen(index,true);
            SetCameraLimits(index==passage.previousRoomIndex?initialCameraLimit:passage.LeftLimit,index==passage.previousRoomIndex?passage.RightLimit:float.PositiveInfinity);
            Busy=false;
        }
        void CaptureVisit(int index)
        {
            var room=rooms[index];if(!room.Cleared)return;var visit=visits[index];visit.cleared=room.Cleared;visit.captured=true;
            visit.stations=room.recoveryStations.Select(t=>t&&t.gameObject.activeSelf).ToArray();
            visit.pickups.Clear();
            // 아이템은 작은 인스턴스만 비활성 보관합니다. 방 메시/내비메시/적은 모두 해제합니다.
            foreach(var pickup in FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None))
                if(pickup.gameObject.scene==room.gameObject.scene||(pickup.transform.position.x>=room.entryX&&pickup.transform.position.x<=room.exitX))Park(pickup.gameObject,visit);
            foreach(var memory in FindObjectsByType<BossMemoryPickup>(FindObjectsSortMode.None))
                if(memory.transform.position.x>=room.entryX&&memory.transform.position.x<=room.exitX)Park(memory.gameObject,visit);
        }
        void Park(GameObject item,Visit visit)
        {
            item.transform.SetParent(null,true);SceneManager.MoveGameObjectToScene(item,gameObject.scene);
            item.SetActive(false);visit.pickups.Add(item);
        }
        void RestoreVisit(int index)
        {
            var room=rooms[index];var visit=visits[index];if(!visit.captured)return;
            // 씬 기본 아이템의 재생성을 막고 이전에 남겨둔 것만 복구합니다.
            foreach(var root in room.gameObject.scene.GetRootGameObjects())
                foreach(var pickup in root.GetComponentsInChildren<WeaponPickup>(true)){pickup.gameObject.SetActive(false);Destroy(pickup.gameObject);}
            for(int i=0;i<room.recoveryStations.Length&&i<visit.stations.Length;i++)
                if(room.recoveryStations[i])room.recoveryStations[i].gameObject.SetActive(visit.stations[i]);
            foreach(var item in visit.pickups)if(item)item.SetActive(true);
            visit.pickups.Clear();
        }

        // Test shortcut uses the same geometry/spawner pipeline as normal room progression.
        public bool RequestSummerBossTestJump()
        {
            return RequestBossTestJump();
        }
        public bool RequestBossTestJump()
        {
            if(!Ready||Busy||!string.IsNullOrEmpty(LoadError))return false;
            // 계절 보스는 마지막 방입니다. 방 재배치 후에도 이전 씬 이름을 따라가지 않습니다.
            if(roomScenePaths==null||roomScenePaths.Length==0)return false;
            int index=roomScenePaths.Length-1;
            if(string.IsNullOrEmpty(roomScenePaths[index]))return false;
            StartCoroutine(JumpToSummerBoss(index));return true;
        }
        IEnumerator JumpToSummerBoss(int target)
        {
            Busy=true;
            foreach(var boss in FindObjectsByType<KrakenPhaseOne>(FindObjectsSortMode.None))boss.CancelEncounter();
            var motor=Hud.motor;var body=motor.GetComponent<Rigidbody>();
            motor.enabled=false;Hud.weapons.enabled=false;body.linearVelocity=Vector3.zero;body.isKinematic=true;
            Hud.health.invulnerable=true;Hud.objective="Loading boss test...";
            foreach(var projectile in FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))Destroy(projectile.gameObject);
            for(int i=0;i<rooms.Length;i++){
                var scene=SceneManager.GetSceneByPath(roomScenePaths[i]);
                if(scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
                rooms[i]=null;
            }
            yield return LoadRoom(target);
            if(!string.IsNullOrEmpty(LoadError))yield break;
            CurrentRoom=loadedRoom=target;transit=null;cleared=false;Hud.completed=false;var room=Current;
            KeepNearbyPassages(CurrentRoom);
            Vector3 spawn=new Vector3(room.entryX+5,2.5f,0);
            motor.SetRespawnPoint(spawn);motor.Respawn();motor.transform.position=spawn;
            var camera=Camera.main?Camera.main.GetComponent<PlayerCamera>():null;
            if(camera)camera.minimumWorldX=room.entryX+1;
            Hud.health.Restore();Hud.health.invulnerable=false;
            body.isKinematic=false;body.useGravity=true;motor.enabled=true;Hud.weapons.enabled=true;
            if(room.rearSeal)room.rearSeal.SetActive(true);if(room.rearVines)room.rearVines.Close();
            Physics.SyncTransforms();room.BeginEncounter();Busy=false;
        }

        IEnumerator Advance()
        {
            Busy = true;
            int previous = CurrentRoom++;
            cleared=false;
            KeepNearbyPassages(CurrentRoom);
            var room = Current;
            if (room.rearSeal) room.rearSeal.SetActive(true);
            // 새 전투방에 충분히 들어오면 뒤쪽 문을 닫습니다.
            if (room.rearVines) room.rearVines.Close();
            if(UsesPassages&&activePassages[previous])activePassages[previous].CloseRear();
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
            // 이전 방의 처치, 상점, 드롭 상태를 재방문에도 그대로 유지합니다.
            if (!UsesPassages && !loadNextOnClear && CurrentRoom + 1 < rooms.Length) yield return LoadRoom(CurrentRoom + 1);
            Busy = !string.IsNullOrEmpty(LoadError);
        }
    }
}
