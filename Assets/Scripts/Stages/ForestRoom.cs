using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;

namespace DogLife
{
    // 씬 안의 참조만 보관합니다. 다른 방의 오브젝트는 직접 참조하지 않습니다.
    public sealed class ForestRoom : MonoBehaviour
    {
        public int index;
        public float entryX, exitX;
        public GameObject encounter, forwardGate, rearSeal;
        public VineGate forwardVines, rearVines;
        // 상점과 합성 공간의 옆문은 현재 방을 클리어하면 함께 열립니다.
        public VineGate[] sideVines = new VineGate[0];
        public Transform[] recoveryStations;
        public Transform exit;
        // 로딩/스폰 위치 검증 중에만 숨깁니다. 랜덤 스폰 방은 로딩 완료 즉시 활성화합니다.
        public bool prepareOnLoad;
        public Unity.AI.Navigation.NavMeshSurface navigation;
        [SerializeField, Tooltip("씬에 비활성 상태로 저장한 구조물입니다. 스트리밍 시 프레임을 나눠 활성화합니다.")]
        private GameObject[] streamedGeometry = new GameObject[0];
        [SerializeField] private string[] streamedPrefabPaths = new string[0];
        public bool GeometryReady => geometryReady;
        public string GeometryError { get; private set; }
        [SerializeField, Min(.1f)] private float activationBudgetMs = 2f;
        [SerializeField, Min(1)] private int maxGeometryGroupsPerFrame = 1;
        bool geometryReady;
        JobHandle collisionBake;
        bool collisionBakePending;

        struct CollisionBakeJob : IJob
        {
            public EntityId mesh;
            public bool convex;
            public MeshColliderCookingOptions options;
            public void Execute() { Physics.BakeMesh(mesh, convex, options); }
        }

        void OnDestroy()
        {
            // 씬이 중도 해제돼도 작업 중인 메시를 먼저 해제하지 않습니다.
            if(collisionBakePending)collisionBake.Complete();
        }

        IEnumerator Start()
        {
            // 방 씬을 단독 실행해 점검할 때도 구조물을 준비합니다.
            if(!ForestRoomStreaming.Instance)yield return PrepareGeometry();
        }

        public IEnumerator PrepareGeometry()
        {
            if(geometryReady)yield break;
            // 씬 등록과 대형 충돌 메시의 활성화를 같은 프레임에 처리하지 않습니다.
            yield return null;
            var geometry = new List<GameObject>(streamedGeometry);
            // 씬에는 경로만 보관합니다. 큰 메시/재질의 로딩과 인스턴스 생성을 묶음별로 분리합니다.
            foreach(var path in streamedPrefabPaths)
            {
                var request=Resources.LoadAsync<GameObject>(path);
                yield return request;
                var prefab=request.asset as GameObject;
                if(!prefab)
                {
                    GeometryError="Missing room geometry: "+path;
                    yield break;
                }
                var item=Instantiate(prefab,transform,false);
                string prefix="Room"+index+"_";
                item.name=prefab.name.StartsWith(prefix) ? prefab.name.Substring(prefix.Length) : prefab.name;
                geometry.Add(item);
                yield return null;
            }
            // 활성화 중 메인 스레드에서 충돌 메시를 굽지 않도록 먼저 작업 스레드에서 준비합니다.
            foreach(var item in geometry)
            {
                if(!item||item.activeInHierarchy)continue;
                foreach(var collider in item.GetComponentsInChildren<MeshCollider>(true))
                {
                    var mesh=collider.sharedMesh;
                    if(!mesh||!mesh.isReadable)continue;
                    collisionBake=new CollisionBakeJob { mesh=mesh.GetEntityId(), convex=collider.convex,
                        options=collider.cookingOptions }.Schedule();
                    collisionBakePending=true;
                    JobHandle.ScheduleBatchedJobs();
                    while(!collisionBake.IsCompleted)yield return null;
                    collisionBake.Complete();
                    collisionBakePending=false;
                }
            }
            double frameStarted=Time.realtimeSinceStartupAsDouble;
            int groupsThisFrame=0;
            foreach(var item in geometry)
            {
                if(item&&!item.activeSelf) { item.SetActive(true); groupsThisFrame++; }
                // SetActive 이후 렌더러/물리 등록 비용도 있으므로 최소 한 프레임씩 분리합니다.
                if(groupsThisFrame>=maxGeometryGroupsPerFrame || (Time.realtimeSinceStartupAsDouble-frameStarted)*1000>=activationBudgetMs)
                {
                    yield return null;
                    frameStarted=Time.realtimeSinceStartupAsDouble;
                    groupsThisFrame=0;
                }
            }
            // 문/구조물의 Start가 끝난 후 몬스터를 소환합니다.
            yield return null;
            geometryReady=true;
        }

        [ContextMenu("구조물 미리보기 (편집 전용)")]
        void PreviewGeometry()
        {
            if(Application.isPlaying)return;
            foreach(var item in streamedGeometry)if(item)item.SetActive(true);
        }
        void Awake()
        {
            if (prepareOnLoad && encounter) encounter.SetActive(false);
        }
        public CombatHealth[] Enemies { get; private set; }
        public void BeginEncounter()
        {
            var spawner=GetComponent<RoomMonsterSpawner>();
            if(spawner&&!spawner.Prepare())return;
            encounter.SetActive(true);
            Enemies = encounter.GetComponentsInChildren<CombatHealth>(true);
        }
    }
}
