using UnityEngine;
namespace DogLife
{
    // 기존 통로의 양쪽 석문을 대칭으로 사용해 정방향/역방향 방 교체를 보호합니다.
    public sealed class SeasonPassage : MonoBehaviour
    {
        public int previousRoomIndex;
        public GameObject rearSeal, forwardSeal;
        public Collider[] sightBlockers;
        public float rearCameraLimitX;
        [SerializeField, Min(.1f)] float doorOpenSeconds=.7f;
        [SerializeField, Min(.1f)] float doorCloseSeconds=1.1f;
        const float DoorLift=10f;
        float rearProgress=1,forwardProgress=1;
        bool rearOpen,forwardOpen;
        Vector3 rearHome,forwardHome;
        public float LeftLimit=>transform.position.x+5.2f;
        public float RightLimit=>transform.position.x+12.8f;
        public bool IsRearClosed=>!rearSeal||(!rearOpen&&rearProgress>=1);
        public bool IsForwardClosed=>!forwardSeal||(!forwardOpen&&forwardProgress>=1);
        static readonly float[] SightZ={-4.1f,0,4.1f};
        static readonly float[] SightY={.3f,3,7};
        public void Configure(int previous){previousRoomIndex=previous;rearCameraLimitX=LeftLimit;}
        void Awake()
        {
            if(rearSeal){rearHome=rearSeal.transform.localPosition;rearSeal.SetActive(true);}
            if(forwardSeal){forwardHome=forwardSeal.transform.localPosition;forwardSeal.SetActive(true);}
        }
        public void Request(bool load) { /* 기존 Hierarchy 트리거 연결을 유지하며 위치로 이동 방향을 판단합니다. */ }
        void Update()
        {
            MoveDoor(rearSeal,rearHome,rearOpen,ref rearProgress);
            MoveDoor(forwardSeal,forwardHome,forwardOpen,ref forwardProgress);
            var stream=ForestRoomStreaming.Instance;if(stream)stream.TickPassage(this);
        }
        void MoveDoor(GameObject door,Vector3 home,bool open,ref float progress)
        {
            if(!door)return;
            progress=Mathf.MoveTowards(progress,open?0:1,Time.deltaTime/Mathf.Max(.1f,open?doorOpenSeconds:doorCloseSeconds));
            door.transform.localPosition=home+Vector3.up*(DoorLift*(1-Mathf.SmoothStep(0,1,progress)));
            // 충돌체가 시각적 벽과 함께 움직이며 완전히 올라간 뒤에만 비활성화합니다.
            door.SetActive(progress>0);
        }
        public void OpenRear(){rearOpen=true;}
        public void CloseRear(){rearOpen=false;}
        public void CloseForward(){forwardOpen=false;}
        public void SetRoomOpen(int room,bool open)
        {
            if(room==previousRoomIndex)rearOpen=open;
            else if(room==previousRoomIndex+1)forwardOpen=open;
        }
        public bool PlayerInside(float x)=>x>LeftLimit+.2f&&x<RightLimit-.2f;
        public int DesiredRoom(float x)
        {
            float local=x-transform.position.x;
            return local<8.4f?previousRoomIndex:local>9.6f?previousRoomIndex+1:-1;
        }
        public bool CanRelease(bool fromLeft,float x)
        {
            if(!PlayerInside(x))return false;
            var camera=Camera.main;if(!camera)return false;
            if(fromLeft?camera.transform.position.x<LeftLimit:camera.transform.position.x>RightLimit)return false;
            // 양쪽 출입구 전체가 굽이 벽 뒤에 가려졌는지 검사해 방 삭제 순간을 숨깁니다.
            foreach(float z in SightZ)foreach(float y in SightY)
            {
                Vector3 end=transform.position+new Vector3(fromLeft?0:18,y,z);
                Vector3 delta=end-camera.transform.position;
                var ray=new Ray(camera.transform.position,delta.normalized);bool blocked=false;
                foreach(var wall in sightBlockers)
                    if(wall&&wall.enabled&&wall.Raycast(ray,out var hit,delta.magnitude)){blocked=true;break;}
                if(!blocked)return false;
            }
            return true;
        }
    }
}
