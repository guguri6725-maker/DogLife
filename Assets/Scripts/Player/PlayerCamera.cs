using UnityEngine;
namespace DogLife
{
    // 마우스로 회전하는 3인칭 카메라. 지형에 닿으면 가까이 당깁니다.
    public sealed class PlayerCamera : MonoBehaviour
    {
        public Transform target;
        public LayerMask obstructionMask = 1 << 8;
        public float distance = 5.5f, height = 1f, sensitivity = 2f;
        public float shoulderOffset = .55f;
        public float yaw = 90, pitch = 18;
        public bool readInput = true;
        Vector3 cinematicFrom;Quaternion cinematicRotation;float cinematicReturnStart,cinematicReturnDuration,cinematicFov,gameplayFov;
        Camera ownCamera;
        // 연출 마지막 화면에서 평소 추적 카메라로 부드럽게 복귀합니다.
        public void BlendFromCinematic(float duration,float originalFov)
        {cinematicFrom=transform.position;cinematicRotation=transform.rotation;cinematicReturnStart=Time.unscaledTime;cinematicReturnDuration=duration;ownCamera=GetComponent<Camera>();cinematicFov=ownCamera?ownCamera.fieldOfView:originalFov;gameplayFov=originalFov;}
        // 지나온 방을 해제한 뒤 카메라가 닫힌 문 뒤로 넘어가지 않게 합니다.
        [System.NonSerialized] public float minimumWorldX = float.NegativeInfinity;
        void Start() { CaptureCursor(true); }
        public void CaptureCursor(bool capture) {
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !capture;
        }
        void Update() {
            if (SkillWindow.BlocksGameplayInput) return;
            if (!readInput) return;
            if (Input.GetKeyDown(KeyCode.Escape)) CaptureCursor(Cursor.lockState != CursorLockMode.Locked);
            if (Cursor.lockState != CursorLockMode.Locked) return;
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity, -25, 65);
        }
        void LateUpdate() {
            if (!target) return;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            // 어깨 옆으로 시점을 옮겨 캐릭터가 중앙 조준점을 가리지 않게 합니다.
            var pivot = target.position + Vector3.up * height + rotation * Vector3.right * shoulderOffset;
            var back = rotation * Vector3.back; var length = distance;
            if (Physics.SphereCast(pivot, .22f, back, out var hit, distance, obstructionMask, QueryTriggerInteraction.Ignore)) length = Mathf.Max(.3f, hit.distance - .15f);
            var position = pivot + back * length;
            position.x = Mathf.Max(position.x, minimumWorldX);
            float blend=cinematicReturnDuration>0?Mathf.SmoothStep(0,1,(Time.unscaledTime-cinematicReturnStart)/cinematicReturnDuration):1;
            transform.SetPositionAndRotation(Vector3.Lerp(cinematicFrom,position,blend),Quaternion.Slerp(cinematicRotation,rotation,blend));
            if(ownCamera){ownCamera.fieldOfView=Mathf.Lerp(cinematicFov,gameplayFov,blend);if(blend>=1)ownCamera=null;}
        }
        void OnDisable() { CaptureCursor(false); }
    }
}
