using UnityEngine;
using UnityEngine.Rendering;
namespace DogLife
{
    // 돌진 중에만 현재 자세를 제한된 메시 버퍼에 굽습니다. 잔상 오브젝트를 매번 생성하지 않습니다.
    public sealed class FrozenKnightAfterimage:MonoBehaviour
    {
        [SerializeField] FrozenKnightBoss boss;
        [SerializeField] SkinnedMeshRenderer horse,rider;
        [SerializeField] Material material;
        [SerializeField,Range(2,5)] int capacity=3;
        [SerializeField,Min(.08f)] float interval=.14f,lifetime=.36f,minDistance=.5f;
        [SerializeField] Color tint=new Color(.18f,.7f,1,.2f);
        Mesh[,] meshes;Matrix4x4[,] matrices;float[] born;bool[] mounted;
        MaterialPropertyBlock properties;int cursor;float nextSample;Vector3 lastPosition;
        void Awake()
        {
            meshes=new Mesh[capacity,2];matrices=new Matrix4x4[capacity,2];born=new float[capacity];mounted=new bool[capacity];properties=new MaterialPropertyBlock();
            for(int i=0;i<capacity;i++){born[i]=-100;for(int j=0;j<2;j++){meshes[i,j]=new Mesh{name="Frozen charge afterimage"};meshes[i,j].MarkDynamic();}}
        }
        void LateUpdate()
        {
            if(!boss||!horse||!material)return;
            var pattern=boss.CurrentPattern;
            bool charge=boss.State==FrozenKnightBoss.CombatState.Active&&(pattern==FrozenKnightBoss.Pattern.SwordFury||pattern==FrozenKnightBoss.Pattern.Charge||pattern==FrozenKnightBoss.Pattern.MultiCharge||pattern==FrozenKnightBoss.Pattern.MountedFury);
            if(charge&&Time.time>=nextSample&&Vector3.Distance(lastPosition,horse.transform.position)>=minDistance)
            {
                // 이 리그의 BakeMesh 결과에는 월드 배율이 이미 반영되어 있어 위치와 회전만 적용합니다.
                horse.BakeMesh(meshes[cursor,0]);matrices[cursor,0]=Matrix4x4.TRS(horse.transform.position,horse.transform.rotation,Vector3.one);
                mounted[cursor]=boss.PhaseNumber==2&&rider;
                if(mounted[cursor]){rider.BakeMesh(meshes[cursor,1]);matrices[cursor,1]=Matrix4x4.TRS(rider.transform.position,rider.transform.rotation,Vector3.one);}
                born[cursor]=Time.time;cursor=(cursor+1)%capacity;nextSample=Time.time+interval;lastPosition=horse.transform.position;
            }
            for(int i=0;i<capacity;i++)
            {
                float fade=1-(Time.time-born[i])/lifetime;if(fade<=0)continue;Color c=tint;c.a*=fade*fade;properties.SetColor("_Tint",c);
                for(int j=0;j<(mounted[i]?2:1);j++)Graphics.DrawMesh(meshes[i,j],matrices[i,j],material,gameObject.layer,null,0,properties,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
            }
        }
        void OnDisable(){if(born!=null)for(int i=0;i<born.Length;i++)born[i]=-100;}
        void OnDestroy(){if(meshes!=null)foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
