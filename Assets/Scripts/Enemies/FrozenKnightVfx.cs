using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace DogLife
{
    // 판정과 독립된 재사용 메시로 서리 문양, 경고 테두리, 얼음 가시와 대검 잔상을 그립니다.
    public sealed partial class FrozenKnightVfx : MonoBehaviour
    {
        public enum Shape { Ring, Line, Sector }
        [SerializeField] Material frostMaterial;
        [SerializeField] Mesh crystalMesh;
        [SerializeField] FrozenKnightBoss boss;
        [SerializeField] Transform swordBase,swordTip;
        [SerializeField,Range(24,128)] int segments=72;
        [SerializeField,Min(0)] float floorOffset=.055f,edgeWidth=.12f,crystalHeight=2,crystalWidth=.35f,ribbonLifetime=.25f;
        [SerializeField,Range(4,40)] int crystalCount=20;
        [SerializeField] Color warningColor=new Color(.15f,.7f,1,.22f),edgeColor=new Color(.65f,.94f,1,.85f),activeColor=new Color(.6f,.9f,1,.48f);
        [SerializeField,Range(4,32)] int frostMoteCount=18;
        [SerializeField,Min(.01f)] float frostMoteHeight=1.5f,swordCoreWidth=.045f;
        [Header("얼음검의 저주 전용")]
        [SerializeField,Range(1,3)] float furyCrystalHeightMultiplier=2f,furyCrystalWidthMultiplier=1.45f;
        [SerializeField,Range(1,3)] int furyCrystalDensity=2;
        [SerializeField,Min(.1f)] float furyShockwaveHeight=.75f;
        [SerializeField] bool showCrystals=true;
        [SerializeField] bool detailedIceSurface;
        bool FuryVisual=>boss&&boss.CurrentPattern==FrozenKnightBoss.Pattern.SwordFury&&boss.State!=FrozenKnightBoss.CombatState.Transition;
        readonly List<Vector3> vertices=new List<Vector3>(1200);
        readonly List<Color> colors=new List<Color>(1200);
        readonly List<int> triangles=new List<int>(2400);
        readonly List<Vector4> effectUV=new List<Vector4>(1200);
        readonly Queue<Vector3> ribbonBases=new Queue<Vector3>(),ribbonTips=new Queue<Vector3>();
        readonly Queue<float> ribbonTimes=new Queue<float>();
        int slashIndex;
        public void BeginSlash(int index)
        {
            // 두 타격의 잔상을 잇지 않고 각 휘두르기의 실제 칼 위치에서 새로 시작합니다.
            slashIndex=index;ribbonBases.Clear();ribbonTips.Clear();ribbonTimes.Clear();
        }
        Mesh areaMesh,ribbonMesh;
        MaterialPropertyBlock properties,surfaceProperties;
        Shape shape;Vector3 origin,direction;float length,width,started;bool visible,active;
        float warningProgress=-1;bool reverseWarning;
        public float WarningProgress=>warningProgress;
        public Vector3 SwordTipPosition=>swordTip?swordTip.position:transform.position;
        // 진행률은 공격 코루틴/Animator에서 전달합니다. 이펙트 자체 시간으로 판정을 추측하지 않습니다.
        public void SetWarningProgress(float progress,bool reverse=false){warningProgress=Mathf.Clamp01(progress);reverseWarning=reverse;}
        void Awake()=>EnsureResources();
        void EnsureResources()
        {
            // 에디터의 스크립트 재로드 후에도 저장되지 않는 PropertyBlock을 복구합니다.
            if(!areaMesh){areaMesh=new Mesh{name="FrozenKnight warning"};areaMesh.MarkDynamic();}
            if(!ribbonMesh){ribbonMesh=new Mesh{name="FrozenKnight sword ribbon"};ribbonMesh.MarkDynamic();}
            if(properties==null)properties=new MaterialPropertyBlock();
            if(surfaceProperties==null)surfaceProperties=new MaterialPropertyBlock();
        }
        public void Show(Shape value,Vector3 point,Vector3 forward,float range,float breadth,Theme style=Theme.Auto)
        {shape=value;origin=point;direction=forward;length=Mathf.Max(.01f,range);width=Mathf.Max(0,breadth);theme=style==Theme.Auto?ResolveTheme():style;visible=true;active=false;warningProgress=-1;started=Time.time;}
        public void MovePose(Vector3 point,Vector3 forward){origin=point;direction=forward;}
        public void MoveOrigin(Vector3 point){origin=point;}
        public void ResizePath(Vector3 point,float range){origin=point;length=Mathf.Max(.01f,range);}
        public void Activate(){active=true;if(warningProgress>=0)warningProgress=1;started=Time.time;}
        public void Hide(){visible=false;}
        void LateUpdate()
        {
            if(!frostMaterial)return;
            EnsureResources();
            if(finaleBlizzard){DrawFinaleBlizzard();return;}
            DrawSword();
            if(visible)DrawArea();
            DrawCinematic();
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {
            int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);for(int n=0;n<4;n++)colors.Add(color);
            for(int n=0;n<4;n++)effectUV.Add(Vector4.zero);
            triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);
        }
        void Stroke(Vector3 a,Vector3 b,float thickness,Color color)
        {var side=Vector3.Cross((b-a).normalized,Vector3.up)*thickness*.5f;Quad(a-side,a+side,b+side,b-side,color);}
        void Snowflake(Vector3 p,float size,Color color)
        {
            // 6방향 가지가 있는 결정 문양을 동일 메시로 묶어 그립니다.
            for(int j=0;j<6;j++){Vector3 ray=Radial(j*60,size);Stroke(p,p+ray,edgeWidth*.23f,color);Vector3 branch=p+ray*.6f;Stroke(branch,branch+Quaternion.AngleAxis(50,Vector3.up)*ray*.25f,edgeWidth*.16f,color);Stroke(branch,branch+Quaternion.AngleAxis(-50,Vector3.up)*ray*.25f,edgeWidth*.16f,color);}
        }
        Vector3 Radial(float degrees,float radius)=>Quaternion.AngleAxis(degrees,Vector3.up)*direction*radius;
        void Clear(){vertices.Clear();triangles.Clear();colors.Clear();effectUV.Clear();}
        void Submit(Mesh mesh)
        {
            if(mesh==areaMesh)DrawAtmosphere();
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,effectUV);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            // 상시 빙판은 공격 경고보다 낮은 강도로 그려 전장 전체의 무늬가 판정을 가리지 않게 합니다.
            bool frozenImpact=active&&(theme==Theme.Burst||theme==Theme.Stomp||theme==Theme.ColdField);
            surfaceProperties.SetFloat("_SurfaceDetail",mesh==areaMesh&&(detailedIceSurface||FuryVisual||frozenImpact)&&shape==Shape.Ring?(theme==Theme.SwordField?.28f:frozenImpact?.45f:1):0);
            surfaceProperties.SetVector("_FieldOrigin",new Vector4(origin.x,origin.y,origin.z,length));
            Graphics.DrawMesh(mesh,Matrix4x4.identity,frostMaterial,gameObject.layer,null,0,surfaceProperties,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
        }
        void DrawArea()
        {
            if(theme!=Theme.Auto){DrawStyledArea();return;}
            Clear();var center=origin+Vector3.up*floorOffset;// 옅은 빨강 예고 위로 기존 얼음빛 진행 채움을 표시합니다.
            Color fill=active?activeColor:new Color(1,.30f,.36f,.24f);
            Color edge=edgeColor;edge.a*=.8f+.2f*Mathf.Sin((Time.time-started)*7);
            if(shape==Shape.Line)
            {
                Vector3 side=Vector3.Cross(Vector3.up,direction)*width*.5f;Vector3 end=center+direction*length;
                Quad(center-side,center+side,end+side,end-side,fill);
                Stroke(center-side,end-side,edgeWidth,edge);Stroke(center+side,end+side,edgeWidth,edge);Stroke(end-side,end+side,edgeWidth,edge);
                // 길을 따라 갈라지는 서리와 진행 방향 문양을 반복합니다.
                int count=Mathf.Max(2,Mathf.CeilToInt(length/1.1f));
                for(int i=1;i<count;i++){Vector3 p=center+direction*(length*i/count);float sign=(i%2==0?1:-1);Stroke(p-direction*.5f,p+side*.65f*sign,edgeWidth*.45f,edge);Stroke(p+side*.65f*sign,p+direction*.4f,edgeWidth*.45f,edge);}
            }
            else
            {
                float angle=shape==Shape.Sector?width:360,inner=shape==Shape.Ring?Mathf.Min(width,length):0;
                for(int i=0;i<segments;i++)
                {
                    float a=-angle*.5f+angle*i/segments,b=-angle*.5f+angle*(i+1)/segments;
                    Quad(center+Radial(a,inner),center+Radial(a,length),center+Radial(b,length),center+Radial(b,inner),fill);
                    Quad(center+Radial(a,length-edgeWidth),center+Radial(a,length),center+Radial(b,length),center+Radial(b,length-edgeWidth),edge);
                    if(inner>0)Quad(center+Radial(a,inner),center+Radial(a,inner+edgeWidth),center+Radial(b,inner+edgeWidth),center+Radial(b,inner),edge);
                }
                for(int i=0;i<12;i++){float a=-angle*.5f+angle*(i+.5f)/12;Vector3 p=center+Radial(a,length*.91f);Stroke(p+Radial(a-35,.24f),p+Radial(a,.5f),edgeWidth*.5f,edge);Stroke(p+Radial(a+35,.24f),p+Radial(a,.5f),edgeWidth*.5f,edge);}
            }
            // 범위 안에만 서리 결정과 낮은 냉기 입자를 배치하여 안전 구역을 가리지 않습니다.
            Color detail=edgeColor;detail.a*=active?.65f:.45f;
            if(shape==Shape.Line)
            {
                int flakes=Mathf.Min(12,Mathf.CeilToInt(length/2));
                for(int i=0;i<flakes;i++)Snowflake(center+direction*length*(i+.5f)/flakes,Mathf.Min(.35f,Mathf.Min(width*.24f,length/(flakes*3))),detail);
            }
            else
            {
                float span=shape==Shape.Sector?width:360;
                float radius=shape==Shape.Ring&&width>0?(length+width)*.5f:length*.64f;
                for(int i=0;i<8;i++)Snowflake(center+Radial(-span*.5f+span*(i+.5f)/8,radius),Mathf.Min(.28f,length*.08f),detail);
            }
            if(active)
            {
                for(int i=0;i<frostMoteCount;i++)
                {
                    float cycle=Mathf.Repeat((Time.time-started)*.8f+i*.618f,1),fraction=(i+.5f)/frostMoteCount;
                    Vector3 p=shape==Shape.Line?center+direction*(length*fraction)+Vector3.Cross(Vector3.up,direction)*width*Mathf.Sin(i*7)*.35f:center+Radial((shape==Shape.Sector?width:360)*(fraction-.5f),shape==Shape.Ring&&width>0?Mathf.Lerp(width,length,.55f):length*.75f);
                    p+=Vector3.up*cycle*frostMoteHeight;float size=.035f+.055f*(1-cycle);Color mote=edgeColor;mote.a*=Mathf.Sin(cycle*Mathf.PI)*.65f;
                    Vector3 side=Vector3.right*size;Quad(p+Vector3.up*size*2,p+side,p-Vector3.up*size*2,p-side,mote);
                    side=Vector3.forward*size;Quad(p+Vector3.up*size*2,p+side,p-Vector3.up*size*2,p-side,mote);
                }
            }
            if(FuryVisual&&shape==Shape.Ring)
            {
                // 현재 위험 고리 안에서만 빛의 파동을 전개해, 지나간 안쪽 안전 지대는 비워 둡니다.
                float pulse=Mathf.Clamp01((Time.time-started)*(active?7:1.8f));
                float r=Mathf.Lerp(Mathf.Min(width,length),length,pulse),rise=active?Mathf.Sin(pulse*Mathf.PI)*furyShockwaveHeight:.035f;
                Color wave=new Color(.7f,.95f,1,active?.65f*(1-pulse):.38f);
                for(int i=0;i<segments;i++)
                {
                    float a=360f*i/segments,b=360f*(i+1)/segments;
                    Vector3 pa=center+Radial(a,r),pb=center+Radial(b,r);
                    Quad(pa,pb,pb+Vector3.up*rise,pa+Vector3.up*rise,wave);
                    Stroke(center+Radial(a,Mathf.Max(width,r-.08f)),center+Radial(b,Mathf.Max(width,r-.08f)),edgeWidth*1.5f,wave);
                }
            }
            if(shape==Shape.Ring&&(FuryVisual||detailedIceSurface))
            {
                // 안전구역은 비우고 위험 경계/빙판 위에 낮은 냉기 띠와 가지 모양 균열을 겹칩니다.
                float inner=Mathf.Min(width,length),outer=FuryVisual?Mathf.Min(length,inner+5):length;
                for(int band=0;band<3;band++)
                {
                    float radius=Mathf.Lerp(inner,outer,(band+1)/3.3f);Color mist=new Color(.45f,.85f,1,active?.10f:.055f);
                    for(int i=0;i<segments;i++)
                    {
                        float a=i*360f/segments,b=(i+1)*360f/segments;
                        float rise=.13f+.17f*(1+Mathf.Sin(a*.07f+Time.time*.8f+band));
                        Vector3 pa=center+Radial(a,radius),pb=center+Radial(b,radius);
                        Quad(pa,pb,pb+Vector3.up*rise,pa+Vector3.up*rise,mist);
                    }
                }
                for(int i=0;i<28;i++)
                {
                    float a=i*137.5f,r=Mathf.Lerp(inner+.2f,outer-.3f,Mathf.Repeat(i*.618f,1));
                    Vector3 p=center+Radial(a,r);Color crack=new Color(.5f,.88f,1,.24f);
                    Vector3 ray=Radial(a+38,.6f);Stroke(p-ray,p,edgeWidth*.35f,crack);Stroke(p,p+Radial(a-25,.45f),edgeWidth*.2f,crack);
                }
                if(inner<.01f)Snowflake(center,Mathf.Min(2,length*.2f),new Color(.55f,.9f,1,.42f));
            }
            DrawWarningFill(center);
            Submit(areaMesh);
            if(active&&crystalMesh&&showCrystals)
            {
                float grow=Mathf.Clamp01((Time.time-started)*7);
                // 높이 0인 결정은 역행렬이 깨져 검은 점으로 보일 수 있어 첫 프레임에는 생략합니다.
                if(grow<=.001f)return;
                int count=FuryVisual?crystalCount*furyCrystalDensity:crystalCount;
                for(int i=0;i<count;i++)
                {
                    float t=(i+.5f)/count;Vector3 p;
                    if(shape==Shape.Line)p=center+direction*(length*t)+Vector3.Cross(Vector3.up,direction)*width*((i%2==0?1:-1)*.35f);
                    else{float angle=shape==Shape.Sector?width:360;float radius=shape==Shape.Ring&&width>0?(length+width)*.5f:length*.82f;if(FuryVisual)radius=Mathf.Min(length-.3f,width+.8f+(i%3)*1.15f);p=center+Radial(-angle*.5f+angle*t,radius);}
                    properties.SetColor("_Tint",new Color(.55f,.88f,1,.75f));
                    var scale=new Vector3(crystalWidth,crystalHeight*(.7f+.3f*Mathf.Sin(i*3.7f))*grow,crystalWidth);
                    if(FuryVisual)scale=Vector3.Scale(scale,new Vector3(furyCrystalWidthMultiplier,furyCrystalHeightMultiplier,furyCrystalWidthMultiplier));
                    Graphics.DrawMesh(crystalMesh,Matrix4x4.TRS(p,Quaternion.Euler(8*Mathf.Sin(i),i*137,12*Mathf.Cos(i)),scale),frostMaterial,gameObject.layer,null,0,properties,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
                }
            }
        }
        void DrawSword()
        {
            if(!boss||!swordBase||!swordTip)return;
            bool emit=boss.SwordActive;
            if(emit){ribbonBases.Enqueue(swordBase.position);ribbonTips.Enqueue(swordTip.position);ribbonTimes.Enqueue(Time.time);}
            while(ribbonTimes.Count>0&&(Time.time-ribbonTimes.Peek()>ribbonLifetime||ribbonTimes.Count>32)){ribbonTimes.Dequeue();ribbonBases.Dequeue();ribbonTips.Dequeue();}
            if(ribbonTimes.Count>1)
            {
                Clear();var a=ribbonBases.ToArray();var b=ribbonTips.ToArray();var times=ribbonTimes.ToArray();
                for(int i=1;i<a.Length;i++)
                {
                    float fade=Mathf.Clamp01(1-(Time.time-times[i])/Mathf.Max(.01f,ribbonLifetime));
                    Color c=new Color(.3f,.65f,.9f,fade*(slashIndex==1?.5f:.36f));int start=colors.Count;SoftSurface(a[i-1],b[i-1],b[i],a[i],c,4,i*.37f);
                    // 검 끝으로 갈수록 흰 청색이 강해지는 넓은 잔상과 얇은 얼음날을 겹칩니다.
                    colors[start+1]=colors[start+2]=new Color(.82f,.95f,1,fade*(slashIndex==1?.9f:.75f));
                    Vector3 inset0=(a[i-1]-b[i-1]).normalized*swordCoreWidth,inset1=(a[i]-b[i]).normalized*swordCoreWidth;
                    Quad(b[i-1]+inset0,b[i-1],b[i],b[i]+inset1,new Color(.9f,1,1,fade*.85f));
                }Submit(ribbonMesh);
            }
            if(boss.Empowered&&crystalMesh)
                for(int i=0;i<7;i++){Vector3 p=Vector3.Lerp(swordBase.position,swordTip.position,(i+.5f)/7f);properties.SetColor("_Tint",new Color(.3f,.8f,1,.6f));Graphics.DrawMesh(crystalMesh,Matrix4x4.TRS(p,swordTip.rotation*Quaternion.Euler(0,Time.time*90+i*50,70),Vector3.one*(.12f+.05f*Mathf.Sin(Time.time*6+i))),frostMaterial,gameObject.layer,null,0,properties,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);}
        }
        void OnDisable(){Hide();ClearCinematic();ribbonBases.Clear();ribbonTips.Clear();ribbonTimes.Clear();}
        void OnDestroy(){if(areaMesh)Destroy(areaMesh);if(ribbonMesh)Destroy(ribbonMesh);if(cinematicMesh)Destroy(cinematicMesh);}
    }
}
