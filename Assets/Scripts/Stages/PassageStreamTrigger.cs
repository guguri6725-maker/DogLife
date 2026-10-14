using UnityEngine;
namespace DogLife
{
    // Hierarchy의 두 Trigger에 각각 해제/준비 역할을 연결합니다.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PassageStreamTrigger : MonoBehaviour
    {
        public bool prepareNextRoom;
        void OnTriggerEnter(Collider other){Notify(other);}
        void OnTriggerStay(Collider other){Notify(other);}
        void Notify(Collider other)
        {
            if(other.GetComponentInParent<PlayerMotor>())
                GetComponentInParent<SeasonPassage>().Request(prepareNextRoom);
        }
    }
}
