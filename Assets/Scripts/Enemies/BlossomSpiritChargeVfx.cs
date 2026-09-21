using UnityEngine;

namespace DogLife
{
    // 손 사이에 모이는 꽃잎은 고정 개수로 재사용합니다.
    [RequireComponent(typeof(BlossomSpiritEnemy))]
    public sealed class BlossomSpiritChargeVfx : MonoBehaviour
    {
        public Mesh petalMesh;
        public Material material;
        BlossomSpiritEnemy enemy;
        Transform orb;
        readonly Transform[] petals=new Transform[10];
        void Awake()
        {
            enemy=GetComponent<BlossomSpiritEnemy>();
            orb=new GameObject("ChargedPetalBloom").transform;
            orb.SetParent(transform,false);
            for(int i=0;i<petals.Length;i++)
            {
                var p=new GameObject("ChargePetal"+i);p.transform.SetParent(orb,false);petals[i]=p.transform;
                p.AddComponent<MeshFilter>().sharedMesh=petalMesh;
                var r=p.AddComponent<MeshRenderer>();r.sharedMaterial=material;
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
            }
            orb.gameObject.SetActive(false);
        }
        void LateUpdate()
        {
            float progress=enemy.ChargeProgress;
            bool visible=progress>0&&enemy.leftHand&&enemy.rightHand;
            orb.gameObject.SetActive(visible);if(!visible)return;
            orb.position=(enemy.leftHand.position+enemy.rightHand.position)*.5f;
            orb.rotation=enemy.visualRoot?enemy.visualRoot.rotation:transform.rotation;
            Vector3 scale=transform.lossyScale;
            orb.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(scale.x)),1/Mathf.Max(.001f,Mathf.Abs(scale.y)),1/Mathf.Max(.001f,Mathf.Abs(scale.z)));
            for(int i=0;i<petals.Length;i++)
            {
                float angle=Time.time*(3+progress*5)+i*Mathf.PI*2/petals.Length;
                float radius=Mathf.Lerp(.80f,.30f,progress)*(i%2==0?1:.65f);
                petals[i].localPosition=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,Mathf.Sin(angle*2)*radius*.4f);
                petals[i].localRotation=Quaternion.Euler(i*37,Time.time*130,i*72+Time.time*90);
                petals[i].localScale=Vector3.one*Mathf.Lerp(.08f,.68f,progress)*(1+.08f*Mathf.Sin(Time.time*18));
            }
        }
        void OnDisable(){if(orb)orb.gameObject.SetActive(false);}
    }
}
