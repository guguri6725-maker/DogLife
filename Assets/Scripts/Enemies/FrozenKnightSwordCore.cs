using UnityEngine;
using DG.Tweening;
namespace DogLife
{
    // 발악 때만 꽂힌 검을 창 유도 표적으로 사용합니다. 일반 플레이어 공격은 횟수에 포함하지 않습니다.
    public sealed class FrozenKnightSwordCore:MonoBehaviour
    {
        [SerializeField] MeshFilter swordMesh;
        [SerializeField] BoxCollider hitArea;
        [SerializeField] Mesh chippedOnce,chippedTwice;
        [SerializeField] FrozenIceBurst burstPrefab;
        [SerializeField,Min(.01f)] float shake=.035f,shakeDuration=.25f;
        Mesh intact;Renderer surface;Vector3 rest;int needed,hits;bool active;
        public bool Broken=>needed>0&&hits>=needed;
        public int Hits=>hits;
        public Collider HitArea=>hitArea;
        public Vector3 Center=>hitArea.bounds.center;
        void Awake(){intact=swordMesh.sharedMesh;surface=swordMesh.GetComponent<Renderer>();rest=swordMesh.transform.localPosition;hitArea.enabled=false;}
        public void Begin(int required)
        {
            hits=0;needed=Mathf.Max(1,required);active=true;swordMesh.sharedMesh=intact;surface.enabled=true;
            swordMesh.transform.DOKill();swordMesh.transform.localPosition=rest;FitCollider();hitArea.enabled=true;
        }
        void FitCollider()
        {
            var b=swordMesh.sharedMesh.bounds;var bounds=new Bounds();
            for(int i=0;i<8;i++)
            {
                Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                p=hitArea.transform.InverseTransformPoint(swordMesh.transform.TransformPoint(p));
                if(i==0)bounds=new Bounds(p,Vector3.zero);else bounds.Encapsulate(p);
            }
            hitArea.center=bounds.center;hitArea.size=bounds.size;
        }
        public void ReceiveSpear()
        {
            if(!active||Broken)return;hits++;GameAudio.Play(Broken?"WinterBoss_SwordCore_Break":"WinterBoss_SwordCore_Hit",Center,.85f);
            var bounds=surface.bounds;
            if(burstPrefab){var fx=Instantiate(burstPrefab,bounds.center,Quaternion.identity);fx.Play(Broken?FrozenIceBurst.Mode.Shatter:FrozenIceBurst.Mode.Chip,bounds.size,null);}
            swordMesh.transform.DOKill();swordMesh.transform.localPosition=rest;
            if(Broken){surface.enabled=false;hitArea.enabled=false;active=false;return;}
            swordMesh.sharedMesh=hits==1?chippedOnce:chippedTwice;FitCollider();
            swordMesh.transform.DOPunchPosition(new Vector3(shake,0,shake*.5f),shakeDuration,8,.6f);
        }
        public void Clear(){active=false;if(hitArea)hitArea.enabled=false;}
        void OnDisable(){Clear();if(swordMesh)swordMesh.transform.DOKill();}
    }
}
