using UnityEngine;
namespace DogLife
{
    // 씬에서 제작한 통로 프리팹. 두 굽이 사이에서 방을 순서대로 해제/준비합니다.
    public sealed class SeasonPassage : MonoBehaviour
    {
        public int previousRoomIndex;
        public GameObject rearSeal, forwardSeal;
        public Collider[] sightBlockers;
        public float rearCameraLimitX;
        [SerializeField, Min(.1f)] float doorOpenSeconds = .7f;
        bool releaseRequested, loadRequested, released;
        float doorProgress;
        Vector3 doorHome;
        static readonly float[] SightZ={-4.1f,0f,4.1f};
        static readonly float[] SightY={.3f,3f,7f};
        public void Configure(int previous)
        {
            previousRoomIndex=previous;
            rearCameraLimitX=transform.position.x+5.2f;
        }
        void Awake()
        {
            if(rearSeal)rearSeal.SetActive(false);
            if(forwardSeal){forwardSeal.SetActive(true);doorHome=forwardSeal.transform.localPosition;}
        }
        public void Request(bool load)
        {
            releaseRequested=true;
            loadRequested|=load;
        }
        void Update()
        {
            var stream=ForestRoomStreaming.Instance;
            if(!stream||!stream.Ready)return;
            if(!released&&releaseRequested&&stream.CurrentRoom==previousRoomIndex&&HiddenBehind())
                released=stream.TryReleaseAtPassage(this);
            if(released&&loadRequested)stream.TryPrepareAtPassage(this);
            if(stream.IsRoomPrepared(previousRoomIndex+1)||stream.CurrentRoom>previousRoomIndex)
            {
                // 불가피하게 늦은 로딩도 석문 뒤에서 끝나므로 빈 방이 노출되지 않습니다.
                doorProgress=Mathf.MoveTowards(doorProgress,1,Time.deltaTime/doorOpenSeconds);
                if(forwardSeal){forwardSeal.transform.localPosition=doorHome+Vector3.up*(10*Mathf.SmoothStep(0,1,doorProgress));if(doorProgress>=1)forwardSeal.SetActive(false);}
            }
        }
        bool HiddenBehind()
        {
            var camera=Camera.main;
            if(!camera||camera.transform.position.x<rearCameraLimitX)return false;
            // 화면 방향만 보지 않고 입구 전체의 시선을 검사해 뒤돌아봐도 팝인이 없게 합니다.
            foreach(float z in SightZ)foreach(float y in SightY)
            {
                Vector3 end=transform.position+new Vector3(0,y,z);
                Vector3 delta=end-camera.transform.position;
                var ray=new Ray(camera.transform.position,delta.normalized);bool blocked=false;
                foreach(var wall in sightBlockers)
                    if(wall&&wall.enabled&&wall.Raycast(ray,out var hit,delta.magnitude)){blocked=true;break;}
                if(!blocked)return false;
            }
            return true;
        }
        public void CloseRear()
        {
            if(rearSeal)rearSeal.SetActive(true);
            var camera=Camera.main?Camera.main.GetComponent<PlayerCamera>():null;
            if(camera)camera.minimumWorldX=Mathf.Max(camera.minimumWorldX,rearCameraLimitX);
        }
    }
}
