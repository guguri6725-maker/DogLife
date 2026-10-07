using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    // 일반 전투방마다 다섯 마리를 순차 생성합니다. 보스방은 별도 분기를 사용합니다.
    [RequireComponent(typeof(ForestRoom))]
    public sealed class RoomMonsterSpawner : MonoBehaviour
    {
        public const int MonstersPerRoom = 5;
        public GameObject[] monsterPrefabs;
        [SerializeField,Tooltip("지정된 방은 일반 몬스터 대신 보스 한 마리만 소환합니다.")] private GameObject bossPrefab;
        [SerializeField] FrozenKnightCinematicStage bossCinematicStage;
        public Vector3 spawnCenter;
        public Vector2 spawnArea = new Vector2(14, 8);
        public bool Prepared { get; private set; }
        bool preparing;
        readonly Collider[] spawnOverlaps = new Collider[64];

        public static int[] PickTypes(int count)
        {
            if(count<2)throw new System.ArgumentException("At least two monster types are required.");
            var types=new int[MonstersPerRoom];
            bool allSame=true;
            for(int i=0;i<types.Length;i++)
            {
                types[i]=Random.Range(0,count);
                if(types[i]!=types[0])allSame=false;
            }
            // 기존처럼 한 종류만 나오는 조합은 피합니다.
            if(allSame)types[types.Length-1]=(types[0]+Random.Range(1,count))%count;
            return types;
        }

        public bool Prepare()
        {
            if(Prepared)return true;
            if(preparing)return false;
            var preparation=PrepareRoutine(false);
            while(preparation.MoveNext()) { }
            return Prepared;
        }

        // 스트리밍에서는 위치 검색과 개별 생성/활성화를 서로 다른 프레임에 처리합니다.
        public IEnumerator PrepareAsync()
        {
            while(preparing)yield return null;
            if(Prepared)yield break;
            yield return PrepareRoutine(true);
        }

        IEnumerator PrepareRoutine(bool spreadAcrossFrames)
        {
            preparing=true;
            try
            {
                var room=GetComponent<ForestRoom>();
                if(bossPrefab)
                {
                    if(!room.encounter){Debug.LogError("Boss room has no encounter root",this);yield break;}
                    var bossAgent=bossPrefab.GetComponent<NavMeshAgent>();
                    if(!bossAgent){Debug.LogError("Boss needs NavMeshAgent",this);yield break;}
                    var filter=new NavMeshQueryFilter{agentTypeID=bossAgent.agentTypeID,areaMask=bossAgent.areaMask};
                    // 고정형 크라켄은 뒤쪽 벽 가까운 지정 위치도 사용할 수 있습니다.
                    var stationaryKraken=bossPrefab.GetComponent<KrakenBoss>();
                    float rearInset=stationaryKraken?stationaryKraken.RearWallClearance:8f;
                    NavMeshHit nav;bool found=false;Vector3 origin=spawnCenter;
                    for(int attempt=0;attempt<80;attempt++)
                    {
                        Vector3 center=spawnCenter;
                        // 커진 고정형 보스는 기존 소환점 대신 몸통이 벽 안에 들어오는 지점부터 검사한다.
                        if(stationaryKraken)center.x=Mathf.Min(center.x,room.exitX-rearInset);
                        Vector3 candidate=center+(attempt==0?Vector3.zero:new Vector3(Random.Range(-5f,5f),0,Random.Range(-4f,4f)));
                        if(!NavMesh.SamplePosition(candidate,out nav,3,filter))continue;
                        if(nav.position.x<room.entryX+8||nav.position.x>room.exitX-rearInset+.01f)continue;
                        int count=Physics.OverlapSphereNonAlloc(nav.position+Vector3.up*3.4f,3.1f,spawnOverlaps,1<<8,QueryTriggerInteraction.Ignore);
                        if(count>0)continue;
                        origin=nav.position;found=true;break;
                    }
                    if(!found){Debug.LogError("No clear boss spawn position: "+gameObject.scene.path,this);yield break;}
                    if(spreadAcrossFrames)yield return null;
                    var boss=Instantiate(bossPrefab,origin,Quaternion.Euler(0,-90,0),room.encounter.transform);boss.name=bossPrefab.name;
                    var boar=boss.GetComponent<SpringBoarBoss>();if(boar)boar.BindRoom(room);
                    var reaper=boss.GetComponent<HarvestReaperBoss>();if(reaper)reaper.BindRoom(room);
                    var kraken=boss.GetComponent<KrakenBoss>();if(kraken)kraken.BindRoom(room);
                    var knight=boss.GetComponent<FrozenKnightBoss>();if(knight){knight.BindRoom(room);knight.BindCinematicStage(bossCinematicStage);}
                    Prepared=true;yield break;
                }
                var pool=new List<GameObject>();
                if(monsterPrefabs!=null)foreach(var prefab in monsterPrefabs)if(prefab&&!pool.Contains(prefab))pool.Add(prefab);
                if(pool.Count<2||!room.encounter){Debug.LogError("Room monster configuration is incomplete",this);yield break;}
                var types=PickTypes(pool.Count);var positions=new List<Vector3>();
                // 모든 위치를 먼저 검증하므로 실패한 방이 부분 생성/자동 클리어되지 않습니다.
                for(int i=0;i<types.Length;i++)
                {
                    var prefab=pool[types[i]];var agent=prefab.GetComponent<NavMeshAgent>();
                    if(!agent){Debug.LogError("Room monster needs a NavMeshAgent: "+prefab.name,this);yield break;}
                    var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
                    float radius=Mathf.Max(.65f,agent.radius*Mathf.Abs(prefab.transform.localScale.x));
                    bool found=false;
                    for(int attempt=0;attempt<180;attempt++)
                    {
                        if(spreadAcrossFrames&&attempt>0&&attempt%12==0)yield return null;
                        Vector3 candidate=spawnCenter+new Vector3(Random.Range(-spawnArea.x*.5f,spawnArea.x*.5f),0,Random.Range(-spawnArea.y*.5f,spawnArea.y*.5f));
                        NavMeshHit hit;if(!NavMesh.SamplePosition(candidate,out hit,4,filter))continue;
                        if(hit.position.x<room.entryX+7||hit.position.x>room.exitX-5)continue;
                        bool overlap=false;foreach(var p in positions)if(Vector3.Distance(p,hit.position)<3.5f){overlap=true;break;}
                        if(overlap)continue;
                        var center=hit.position+Vector3.up*(radius+.2f);
                        int count=Physics.OverlapSphereNonAlloc(center,radius,spawnOverlaps,~0,QueryTriggerInteraction.Ignore);
                        overlap=count==spawnOverlaps.Length; // 버퍼가 찬 위치는 안전하게 제외합니다.
                        for(int c=0;c<count&&!overlap;c++)
                            if(!spawnOverlaps[c].GetComponentInParent<CombatHealth>())overlap=true;
                        if(overlap)continue;
                        positions.Add(hit.position);found=true;break;
                    }
                    if(!found){Debug.LogError("Could not find "+types.Length+" clear monster spawn positions: "+gameObject.scene.path,this);yield break;}
                    if(spreadAcrossFrames)yield return null;
                }
                room.encounter.SetActive(false);
                var spawned=new GameObject[types.Length];
                for(int i=0;i<types.Length;i++)
                {
                    var prefab=pool[types[i]];
                    var agent=prefab.GetComponent<NavMeshAgent>();
                    var origin=positions[i]+Vector3.up*agent.baseOffset*Mathf.Abs(prefab.transform.localScale.y);
                    var monster=Instantiate(prefab,origin,Quaternion.Euler(0,-90,0),room.encounter.transform);
                    monster.name=prefab.name+"_"+(i+1);
                    monster.SetActive(false);
                    spawned[i]=monster;
                    if(spreadAcrossFrames)yield return null;
                }
                if(spreadAcrossFrames)
                {
                    room.encounter.SetActive(true);
                    foreach(var monster in spawned)
                    {
                        monster.SetActive(true);
                        yield return null;
                    }
                }
                else foreach(var monster in spawned)monster.SetActive(true);
                Prepared=true;
                }
            finally { preparing=false; }
        }
    }
}
