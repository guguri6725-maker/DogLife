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
        [SerializeField, Min(.1f)] float doorCloseSeconds = 1.1f;
        const float DoorLift = 10f;
        bool releaseRequested, loadRequested, released, rearClosing;
        float doorProgress, rearProgress;
        Vector3 doorHome, rearHome;
        public bool IsRearClosed => !rearSeal || (rearClosing && rearProgress >= 1f);
        static readonly float[] SightZ={-4.1f,0f,4.1f};
        static readonly float[] SightY={.3f,3f,7f};
        public void Configure(int previous)
        {
            previousRoomIndex=previous;
            rearCameraLimitX=transform.position.x+5.2f;
        }
        void Awake()
        {
            if(rearSeal)
            {
                rearHome=rearSeal.transform.localPosition;
                rearSeal.SetActive(false);
                // 닫힘 요청 전에 천장 위로 올려 활성화 순간에도 벽이 튀어나오지 않습니다.
                rearSeal.transform.localPosition=rearHome+Vector3.up*DoorLift;
            }
            if(forwardSeal){forwardSeal.SetActive(true);doorHome=forwardSeal.transform.localPosition;}
        }
        public void Request(bool load)
        {
            releaseRequested=true;
            loadRequested|=load;
        }
        void Update()
        {
            if(rearClosing&&!IsRearClosed)
            {
                rearProgress=Mathf.MoveTowards(rearProgress,1,Time.deltaTime/Mathf.Max(.1f,doorCloseSeconds));
                rearSeal.transform.localPosition=rearHome+Vector3.up*(DoorLift*(1-Mathf.SmoothStep(0,1,rearProgress)));
            }
            var stream=ForestRoomStreaming.Instance;
            if(!stream||!stream.Ready)return;
            if(!released&&releaseRequested&&stream.CurrentRoom==previousRoomIndex&&HiddenBehind())
                released=stream.TryReleaseAtPassage(this);
            if(released&&loadRequested)stream.TryPrepareAtPassage(this);
            if(stream.IsRoomPrepared(previousRoomIndex+1)||stream.CurrentRoom>previousRoomIndex)
            {
                // 불가피하게 늦은 로딩도 석문 뒤에서 끝나므로 빈 방이 노출되지 않습니다.
                doorProgress=Mathf.MoveTowards(doorProgress,1,Time.deltaTime/doorOpenSeconds);
                if(forwardSeal){forwardSeal.transform.localPosition=doorHome+Vector3.up*(DoorLift*Mathf.SmoothStep(0,1,doorProgress));if(doorProgress>=1)forwardSeal.SetActive(false);}
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
            // 반복 진입해도 하강을 처음부터 다시 시작하지 않습니다. 충돌체도 벽과 함께 이동합니다.
            if(rearClosing)return;
            rearClosing=true;
            if(rearSeal)rearSeal.SetActive(true);
            var camera=Camera.main?Camera.main.GetComponent<PlayerCamera>():null;
            if(camera)camera.minimumWorldX=Mathf.Max(camera.minimumWorldX,rearCameraLimitX);
        }
    }
}
