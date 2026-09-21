using UnityEngine;
namespace DogLife
{
    // Uses the existing gate state so visuals and blocking end at the same time.
    public sealed class AutumnGateVisual:MonoBehaviour
    {
        public VineGate gate;
        public Transform leftPanel,rightPanel;
        public Vector3 leftClosed,rightClosed;
        void LateUpdate()
        {
            if(!gate)return;
            float open=Mathf.SmoothStep(0,1,gate.Progress);
            // Swing inside the doorway instead of sliding the leaves into the stone walls.
            Pose(leftPanel,leftClosed,new Vector3(-3.5f,0,0),-90*open);
            Pose(rightPanel,rightClosed,new Vector3(3.5f,0,0),90*open);
        }
        static void Pose(Transform panel,Vector3 closed,Vector3 hinge,float angle)
        {
            if(!panel)return;
            var rotation=Quaternion.Euler(0,angle,0);
            panel.localRotation=rotation;
            panel.localPosition=hinge+rotation*(closed-hinge);
        }
    }
}

