using UnityEngine;
namespace DogLife
{
    // 플레이어를 따라가면서 카메라의 가로 이동 범위를 제한합니다.
    public sealed class SideViewCamera : MonoBehaviour
    {
        public Transform target;
        public float minX = -5, maxX = 20;
        public Vector3 offset = new Vector3(0, 3, -18);
        // 캐릭터 이동이 끝난 뒤 부드럽게 목표 위치를 추적합니다.
        void LateUpdate()
        {
            if (!target) return;
            var desired = new Vector3(Mathf.Clamp(target.position.x, minX, maxX), Mathf.Max(1, target.position.y), 0) + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1 - Mathf.Exp(-8 * Time.deltaTime));
        }
    }
}
