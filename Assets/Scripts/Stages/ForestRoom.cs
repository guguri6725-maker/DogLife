using UnityEngine;

namespace DogLife
{
    // 씬 안의 참조만 보관합니다. 다른 방의 오브젝트는 직접 참조하지 않습니다.
    public sealed class ForestRoom : MonoBehaviour
    {
        public int index;
        public float entryX, exitX;
        public GameObject encounter, forwardGate, rearSeal;
        public VineGate forwardVines, rearVines;
        // 상점과 합성 공간의 옆문은 현재 방을 클리어하면 함께 열립니다.
        public VineGate[] sideVines = new VineGate[0];
        public Transform[] recoveryStations;
        public Transform exit;
        // 계절 맵은 로딩 중 적을 숨기고, 해당 방에 진입했을 때 전투를 시작합니다.
        public bool prepareOnLoad;
        public Unity.AI.Navigation.NavMeshSurface navigation;
        void Awake()
        {
            if (prepareOnLoad && encounter) encounter.SetActive(false);
        }
        public CombatHealth[] Enemies { get; private set; }
        public void BeginEncounter()
        {
            encounter.SetActive(true);
            Enemies = encounter.GetComponentsInChildren<CombatHealth>(true);
        }
    }
}
