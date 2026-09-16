using UnityEngine;

namespace DogLife
{
    // 씬 안의 참조만 보관합니다. 다른 방의 오브젝트는 직접 참조하지 않습니다.
    public sealed class ForestRoom : MonoBehaviour
    {
        public int index;
        public float entryX, exitX;
        public GameObject encounter, forwardGate, rearSeal;
        public Transform[] recoveryStations;
        public Transform exit;
        public CombatHealth[] Enemies { get; private set; }
        public void BeginEncounter()
        {
            encounter.SetActive(true);
            Enemies = encounter.GetComponentsInChildren<CombatHealth>(true);
        }
    }
}
