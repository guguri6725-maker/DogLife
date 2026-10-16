using UnityEngine;

namespace DogLife
{
    // 나뭇잎 등의 Trigger를 카메라에만 구조물로 취급합니다. 이동/피격 판정은 막지 않습니다.
    [DisallowMultipleComponent]
    public sealed class CameraStructure : MonoBehaviour { }
}
