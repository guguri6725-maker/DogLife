using UnityEngine;
namespace DogLife
{
    public sealed class SideViewCamera : MonoBehaviour
    {
        public Transform target;
        public float minX = -5, maxX = 20;
        public Vector3 offset = new Vector3(0, 3, -18);
        void LateUpdate()
        {
            if (!target) return;
            var desired = new Vector3(Mathf.Clamp(target.position.x, minX, maxX), Mathf.Max(1, target.position.y), 0) + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1 - Mathf.Exp(-8 * Time.deltaTime));
        }
    }
}
