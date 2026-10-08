using UnityEngine;
namespace DogLife
{
    public sealed partial class FrozenKnightVfx
    {
        [Header("냉기 / 얼음 파편 디테일")]
        [SerializeField,Range(6,32)] int mistCount=18;
        [SerializeField,Range(8,64)] int iceSparkCount=32;
        [SerializeField,Range(0,1)] float mistOpacity=.32f;
        [SerializeField,Min(.1f)] float mistSize=1.6f;
        Camera detailCamera;
        void SoftSurface(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float mode,float seed)
        {
            int n=effectUV.Count;Quad(a,b,c,d,color);
            effectUV[n]=new Vector4(0,0,mode,seed);effectUV[n+1]=new Vector4(1,0,mode,seed);
            effectUV[n+2]=new Vector4(1,1,mode,seed);effectUV[n+3]=new Vector4(0,1,mode,seed);
        }
        void ColdPuff(Vector3 p,float size,float height,Color tint,float mode,float seed)
        {
            Vector3 right=detailCamera?detailCamera.transform.right:Vector3.right;
            Vector3 up=detailCamera?detailCamera.transform.up:Vector3.up;
            right*=size*.5f;up*=height*.5f;
            SoftSurface(p-right-up,p+right-up,p+right+up,p-right+up,tint,mode,seed);
        }
        void DrawAtmosphere()
        {
            if(!detailCamera)detailCamera=Camera.main;
            // 조준 표시는 깔끔하게 유지하고, 실제 발동과 충전에만 냉기를 겹칩니다.
            if(theme==Theme.SpearAim)return;
            float age=Mathf.Max(0,Time.time-started);
            bool blade=theme==Theme.BladeCharge||theme==Theme.BladeFlight||theme==Theme.SwordConjure;
            bool field=theme==Theme.SwordField||theme==Theme.ColdField||theme==Theme.Fury||theme==Theme.IceTrail;
            bool burst=theme==Theme.Burst||theme==Theme.Stomp;
            if(!active&&!blade)return;
            float envelope=burst?Mathf.Clamp01(age*10)*Mathf.Clamp01(1-age/1.1f):1;
            if(envelope<=.001f)return;
            Vector3 c=origin+Vector3.up*floorOffset,side=Vector3.Cross(Vector3.up,direction).normalized;
            int count=blade?6:mistCount;
            for(int i=0;i<count;i++)
            {
                float u=(i+.5f)/count,cycle=Mathf.Repeat(age*.3f+i*.618f,1);
                float size=mistSize*(.7f+.4f*Mathf.Repeat(i*.37f,1));Vector3 p;
                if(blade)
                {
                    Vector3 tip=theme==Theme.SwordConjure&&swordTip?swordTip.position:origin;
                    p=tip-direction*(theme==Theme.BladeFlight?u*length:0)+Vector3.up*(u*.8f);
                    size*=.38f;
                }
                else if(shape==Shape.Line)
                {
                    size=Mathf.Min(size,width*.75f);
                    p=c+direction*(length*u)+side*(width-size)*Mathf.Sin(i*2.3f)*.35f;
                }
                else
                {
                    float inner=shape==Shape.Ring?Mathf.Min(width,length):0;
                    size=Mathf.Min(size,(length-inner)*.7f);
                    float r=burst?Mathf.Lerp(inner+size*.5f,length-size*.5f,Mathf.Clamp01(age*2.5f)):Mathf.Lerp(inner+size*.5f,length-size*.5f,Mathf.Repeat(i*.618f,1));
                    float span=shape==Shape.Sector?width:360;
                    p=c+Radial(span*(u-.5f),r);
                    if(span<359)size=Mathf.Min(size,2*r*Mathf.Sin(Mathf.Min(90,span*Mathf.Min(u,1-u))*Mathf.Deg2Rad));
                }
                p+=Vector3.up*(.18f+cycle*(field?.35f:.65f));
                ColdPuff(p,size,size*.65f,new Color(.4f,.68f,.82f,mistOpacity*envelope*(field?.7f:1)),2,i*3.13f);
            }
            // 작은 결정과 반짝임을 함께 쓰되 개수를 제한해 전장을 가리지 않습니다.
            int sparks=blade?8:iceSparkCount;
            for(int i=0;i<sparks;i++)
            {
                float u=(i+.5f)/sparks,cycle=Mathf.Repeat(age*.65f+i*.38197f,1);Vector3 p;
                if(blade)p=origin-direction*(u*length*.8f)+side*Mathf.Sin(i*2.3f)*.25f;
                else if(shape==Shape.Line)p=c+direction*length*u+side*width*Mathf.Sin(i*2.3f)*.38f;
                else
                {
                    float inner=shape==Shape.Ring?Mathf.Min(width,length):0,span=shape==Shape.Sector?width:360;
                    p=c+Radial(span*(u-.5f),Mathf.Lerp(inner+.08f,length-.08f,Mathf.Repeat(i*.618f,1)));
                }
                p+=Vector3.up*(.1f+Mathf.Sin(cycle*Mathf.PI)*(burst?1.8f:field?.65f:1.1f));
                float fade=Mathf.Sin(cycle*Mathf.PI)*envelope;
                ColdPuff(p,.09f+(i%3)*.025f,.14f,new Color(.7f,.9f,1,.75f*fade),3,i);
                if(burst&&i%3==0)IcePlate(p,Quaternion.Euler(age*170+i*37,i*137,age*95),.12f,.24f,.06f,fade*.7f);
            }
        }
    }
}
