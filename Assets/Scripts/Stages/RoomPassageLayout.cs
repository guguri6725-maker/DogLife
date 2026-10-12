using UnityEngine;

namespace DogLife
{
    // Keeps source geometry reusable: only obsolete straight passage pieces are hidden.
    public sealed class RoomPassageLayout : MonoBehaviour
    {
        [HideInInspector] public float appliedOffset;
        public bool hasOutgoingPassage;
        public void HideOldPassage()
        {
            if(!hasOutgoingPassage)return;
            foreach(var t in GetComponentsInChildren<Transform>(true))
            {
                string n=t.name.ToLowerInvariant();
                if(!n.Contains("passage")&&!n.Contains("corridor"))continue;
                // Side-room bridges are still used by shops and synthesis rooms.
                // Only the replaced forward passage is obsolete.
                if(n.Contains("sidepassage"))continue;
                if(t==transform||t.GetComponentInParent<VineGate>())continue;
                foreach(var r in t.GetComponentsInChildren<Renderer>(true))if(!r.GetComponentInParent<VineGate>())r.enabled=false;
                foreach(var c in t.GetComponentsInChildren<Collider>(true))if(!c.GetComponentInParent<VineGate>())c.enabled=false;
            }
        }
    }
}
