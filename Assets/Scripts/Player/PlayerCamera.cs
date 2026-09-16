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
            transform.SetPositionAndRotation(position, rotation);
        }
        void OnDisable() { CaptureCursor(false); }
    }
}
