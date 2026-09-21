using UnityEngine;

namespace DogLife
{
    [RequireComponent(typeof(FloralDeerEnemy))]
    public sealed class DeerChargeVfx : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Material material;
        readonly TrailRenderer[] trails=new TrailRenderer[3];
        FloralDeerEnemy enemy;
        bool charging;
        void Awake()
        {
            enemy=GetComponent<FloralDeerEnemy>();
            if(!head)foreach(var t in GetComponentsInChildren<Transform>())if(t.name=="Head"){head=t;break;}
            for(int i=0;i<trails.Length;i++)
            {
                var go=new GameObject("HornWindTrail"+i);go.transform.SetParent(transform,false);
                var trail=go.AddComponent<TrailRenderer>();trails[i]=trail;
                trail.sharedMaterial=material;trail.time=.42f;trail.minVertexDistance=.035f;
                trail.widthCurve=AnimationCurve.EaseInOut(0,(i==2?.22f:.34f)*Mathf.Abs(transform.lossyScale.x),1,0);
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.88f,.95f),0),new GradientColorKey(new Color(1,.25f,.55f),1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(0,1)});
                trail.colorGradient=gradient;trail.numCornerVertices=4;trail.numCapVertices=4;
                trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;trail.emitting=false;
            }
        }
        void LateUpdate()
        {
            bool active=enemy.IsCharging;
            float scale=Mathf.Abs(transform.lossyScale.x);
            Vector3 center=head?head.position:transform.position;
            for(int i=0;i<trails.Length;i++)
            {
                trails[i].transform.position=center+transform.right*((i-1)*.23f*scale)+Vector3.up*(i==1?.28f:.13f)*scale;
                if(active&&!charging)trails[i].Clear();
                trails[i].emitting=active;
            }
            charging=active;
        }
        void OnDisable(){charging=false;foreach(var trail in trails)if(trail){trail.emitting=false;trail.Clear();}}
    }
}

