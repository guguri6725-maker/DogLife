using UnityEngine;
namespace DogLife
{
    public sealed class FootstepAudioRelay:MonoBehaviour
    {
        public PlayerMovementAudio Owner {get;set;}
        public void OnFootContact(AnimationEvent evt)
        {
            if(Owner)Owner.FootContact(evt);
        }
    }
}
