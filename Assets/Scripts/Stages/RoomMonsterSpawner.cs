using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DogLife
{
    // 방 로딩마다 세 마리 조합을 정하고, 스트리밍 준비 중 순차 생성·활성화합니다.
    [RequireComponent(typeof(ForestRoom))]
    public sealed class RoomMonsterSpawner : MonoBehaviour
    {
        public GameObject[] monsterPrefabs;
        public Vector3 spawnCenter;
        public Vector2 spawnArea = new Vector2(14, 8);
        public bool Prepared { get; private set; }
        bool preparing;
        readonly Collider[] spawnOverlaps = new Collider[64];

        public static int[] PickTypes(int count)
        {
            if(count<2)throw new System.ArgumentException("At least two monster types are required.");
            int a=Random.Range(0,count),b=Random.Range(0,count),c=Random.Range(0,count);
            if(a==b&&b==c)c=(a+Random.Range(1,count))%count;
            return new[]{a,b,c};
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
                var pool=new List<GameObject>();
                if(monsterPrefabs!=null)foreach(var prefab in monsterPrefabs)if(prefab&&!pool.Contains(prefab))pool.Add(prefab);
                if(pool.Count<2||!room.encounter){Debug.LogError("Room monster configuration is incomplete",this);yield break;}
                var types=PickTypes(pool.Count);var positions=new List<Vector3>();
                // 먼저 세 위치를 모두 검증하므로 실패한 방이 부분 생성/자동 클리어되지 않습니다.
                for(int i=0;i<3;i++)
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
                    if(!found){Debug.LogError("Could not find three clear monster spawn positions: "+gameObject.scene.path,this);yield break;}
                    if(spreadAcrossFrames)yield return null;
                }
                room.encounter.SetActive(false);
                var spawned=new GameObject[3];
                for(int i=0;i<3;i++)
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
