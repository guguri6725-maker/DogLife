using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
 [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
 public sealed class KrakenVfx:MonoBehaviour
 {
  [SerializeField] private KrakenBoss boss;
  [SerializeField] private Material material;
  [SerializeField] private Color deep=new Color(.035f,.18f,.35f),ocean=new Color(.08f,.53f,.85f),foam=new Color(.65f,.95f,1),safe=new Color(.4f,1,.88f);
  [SerializeField,Range(24,128)] private int segments=72;
  [SerializeField,Min(.01f)] private float border=.13f,fillOpacity=.5f,sprayHeight=4,crestThickness=.28f;
  [Header("해류와 충격 표현")]
  [SerializeField,Range(12,64)] private int sprayCount=40;
  [SerializeField,Min(.1f)] private float foamWidth=.22f,vortexHeight=2.4f;
  [SerializeField,Min(0)] private float waterSurfaceOffset=.45f;
  [SerializeField,Min(1)] private float furyCrownRadius=15,furySprayMultiplier=1.7f;
  [Header("물막 / 포말 / 미세 물안개")]
  [SerializeField,Range(24,96)] int crownSegments=48;
  [SerializeField,Range(0,16)] int mistCount=8;
  [SerializeField,Range(0,1)] float mistOpacity=.16f;
  readonly List<Vector4> uv=new List<Vector4>(24000);
  // 0=읽기 쉬운 경고, 1=물막, 2=포말, 3=미세 물보라.
  float surfaceKind=1;
  Mesh mesh;readonly List<Vector3> v=new List<Vector3>(24000);readonly List<Color> c=new List<Color>(24000);readonly List<int> ids=new List<int>(36000);
  Vector3 viewRight=Vector3.right,viewUp=Vector3.up;
  Color A(Color a,float alpha){a.a=alpha;return a;}
  Vector3 D(float angle){float a=angle*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));}
  void Awake(){mesh=new Mesh{name="Kraken water / pearl foam / filled warnings",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;var r=GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
  void Tri(Vector3 a,Vector3 b,Vector3 d,Color color){int n=v.Count;v.Add(transform.InverseTransformPoint(a));v.Add(transform.InverseTransformPoint(b));v.Add(transform.InverseTransformPoint(d));c.Add(color);c.Add(color);c.Add(color);uv.Add(new Vector4(0,0,surfaceKind,0));uv.Add(new Vector4(1,0,surfaceKind,0));uv.Add(new Vector4(1,1,surfaceKind,0));ids.Add(n);ids.Add(n+1);ids.Add(n+2);}
  void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e,Color color){Tri(a,b,d,color);Tri(a,d,e,color);}
  void Line(Vector3 a,Vector3 b,float width,Color color){Vector3 side=Vector3.Cross(b-a,Vector3.up).normalized*width*.5f;if(side.sqrMagnitude<.0001f)side=Vector3.right*width*.5f;Quad(a-side,a+side,b+side,b-side,color);}
  void Ring(Vector3 p,float inner,float outer,Color color){for(int i=0;i<segments;i++){Vector3 a=D(i*360f/segments),b=D((i+1)*360f/segments);Quad(p+a*inner,p+a*outer,p+b*outer,p+b*inner,color);}}
  void Drop(Vector3 p,float size,Color color)
  {
   // 사각 경계는 셰이더에서 지우고 안쪽에도 불규칙한 밀도를 준다.
   float previous=surfaceKind;surfaceKind=3;int n=v.Count;
   Vector3 right=viewRight*size,up=viewUp*size;
   Quad(p-right-up,p+right-up,p+right+up,p-right+up,color);
   uv[n]=new Vector4(0,0,3,0);uv[n+1]=new Vector4(1,0,3,0);uv[n+2]=new Vector4(1,1,3,0);
   uv[n+3]=new Vector4(0,0,3,0);uv[n+4]=new Vector4(1,1,3,0);uv[n+5]=new Vector4(0,1,3,0);surfaceKind=previous;
  }
  void CircleWarning(Vector3 p,float radius,float t)
  {
   surfaceKind=0;
   Ring(p,0,radius,A(deep,.22f));Ring(p+Vector3.up*.01f,0,radius*t,A(ocean,fillOpacity));
   Ring(p+Vector3.up*.02f,Mathf.Max(0,radius*t-border),radius*t,A(foam,.95f));
   Ring(p,radius-border,radius,A(foam,.95f));Ring(p,radius-.45f,radius-.34f,A(ocean,.8f));
   surfaceKind=1;
  }
  void RectWarning(Vector3 p,Vector3 forward,float length,float width,float t)
  {
   surfaceKind=0;
   Vector3 side=Vector3.Cross(Vector3.up,forward)*width*.5f,end=p+forward*length,tip=Vector3.Lerp(p,end,t);
   Quad(p-side,p+side,end+side,end-side,A(deep,.22f));Quad(p-side+Vector3.up*.01f,p+side+Vector3.up*.01f,tip+side+Vector3.up*.01f,tip-side+Vector3.up*.01f,A(ocean,fillOpacity));
   Line(p-side,end-side,border,A(foam,.95f));Line(p+side,end+side,border,A(foam,.95f));Line(end-side,end+side,border,A(foam,.95f));Line(tip-side,tip+side,border,A(foam,1));
   surfaceKind=1;
  }
  void Splash(Vector3 p,float radius,float t,float heightMultiplier=1)
  {
   t=Mathf.Clamp01(t);float rise=4*t*(1-t),fade=Mathf.Sqrt(1-t);
   // 방사선 대신 이어진 물막이 퍼지고 가장자리부터 포말로 부서진다.
   for(int i=0;i<crownSegments;i++)for(int j=0;j<5;j++)
   {
    float a=i*Mathf.PI*2/crownSegments,b=(i+1)*Mathf.PI*2/crownSegments,u=j/5f,w=(j+1)/5f;
    surfaceKind=j>=3?2:1;
    Quad(Crown(p,radius,a,u,t,heightMultiplier),Crown(p,radius,b,u,t,heightMultiplier),Crown(p,radius,b,w,t,heightMultiplier),Crown(p,radius,a,w,t,heightMultiplier),A(j>=3?foam:Color.Lerp(deep,ocean,u),fade*(j>=3?.92f:.72f)));
   }
   for(int i=0;i<sprayCount;i++)
   {
    float seed=Mathf.Repeat(i*.618f,1),q=Mathf.Clamp01((t-seed*.12f)/.88f);Vector3 f=D(i*137.5f);
    Vector3 at=p+f*radius*(.3f+q*(.7f+seed*.55f))+Vector3.up*Mathf.Max(0,q*(1-q)*4)*sprayHeight*heightMultiplier*(.4f+.65f*seed);
    Drop(at,.045f+seed*.09f,A(foam,fade*.8f));
   }
   for(int i=0;i<mistCount;i++){float seed=Mathf.Repeat(i*.754f,1);Drop(p+D(i*137.5f)*radius*(.25f+t*.8f)+Vector3.up*(.3f+rise*sprayHeight*.45f),(.5f+t)*(.7f+seed),A(foam,mistOpacity*fade));}
   surfaceKind=2;float r=radius*(.3f+t);Ring(p+Vector3.up*.015f,Mathf.Max(0,r-radius*.25f),r,A(foam,fade*.55f));surfaceKind=1;
  }
  Vector3 Crown(Vector3 p,float radius,float a,float u,float t,float height)
  {
   float irregular=.8f+.12f*Mathf.Sin(a*5+.7f)+.08f*Mathf.Sin(a*11-1.2f);
   float reach=radius*(.18f+u*(.35f+t*.9f))*irregular;
   float y=Mathf.Sin(u*Mathf.PI*.85f)*4*t*(1-t)*sprayHeight*height*(.3f+.25f*irregular);
   return p+new Vector3(Mathf.Sin(a)*reach,y,Mathf.Cos(a)*reach);
  }
  void FoamWake(Vector3 p,float radius,float t)
  {
   surfaceKind=2;
   for(int i=0;i<crownSegments;i++)
   {
    float a=i*360f/crownSegments,b=(i+1)*360f/crownSegments;
    float ra=radius*(.86f+.08f*Mathf.Sin(a*.12f+t*3)),rb=radius*(.86f+.08f*Mathf.Sin(b*.12f+t*3));
    Quad(p+D(a)*ra*.48f,p+D(a)*ra,p+D(b)*rb,p+D(b)*rb*.48f,A(foam,.6f*(1-t)));
   }
   surfaceKind=1;
  }
  void Vortex(Vector3 p,float radius,float progress,bool rising)
  {
   surfaceKind=2;
   // 붙잡기는 안쪽으로 감기는 해류, 폭주는 위로 솟는 나선으로 구별한다.
   for(int arm=0;arm<3;arm++)
   {
    Vector3 previous=p;
    for(int j=0;j<=40;j++)
    {
     float u=j/40f,angle=arm*120+u*280-Time.time*75;
     Vector3 next=p+D(angle)*radius*(.2f+.72f*u)+Vector3.up*(rising?(1-u)*vortexHeight*progress:.025f);
     if(j>0)Line(previous,next,foamWidth*(1.2f+2*u),A(arm==0?foam:ocean,.18f+.35f*progress));previous=next;
    }
   }
   surfaceKind=1;
  }
  void Impact(KrakenTentacle t,Vector3 p)
  {
   // 선딜 중에는 물보라를 내지 않고 실제 충돌 프레임부터 터뜨린다.
   float q=t.ImpactProgress;if(q<0)return;
   if(t.Circle){Splash(p,t.Length,q);Vortex(p,t.Length,1-q,true);}
   else
   {
    for(int i=1;i<=3;i++)Splash(p+t.Forward*t.Length*(i/4f),t.Width*.8f,Mathf.Clamp01(q+(3-i)*.065f));
   }
  }
  Vector3 WavePoint(KrakenWave wave,float x,float u)
  {
   float crest=Mathf.Sin(u*Mathf.PI),ripple=Mathf.Sin(x*1.7f+Time.time*7)*.16f+Mathf.Sin(x*3.2f-Time.time*5)*.08f;
   // 파도 꼭대기가 앞쪽으로 말려 내려오는 단면을 만든다.
   return wave.transform.TransformPoint(new Vector3(x,crest*(wave.Height+ripple),-u*3+crest*crest*crest*2.7f));
  }
  void WaveStrip(KrakenWave wave,float left,float right)
  {
   if(right-left<.01f)return;
   for(int i=0;i<40;i++)for(int j=0;j<20;j++)
   {
    float x=Mathf.Lerp(left,right,i/40f),xx=Mathf.Lerp(left,right,(i+1)/40f),u=j/20f,w=(j+1)/20f;
    Vector3 a=WavePoint(wave,x,u),b=WavePoint(wave,xx,u),d=WavePoint(wave,xx,w),e=WavePoint(wave,x,w);
    surfaceKind=1;int n=v.Count;Color low=WaveColor(u),high=WaveColor(w);Quad(a,b,d,e,low);
    c[n+2]=high;c[n+4]=high;c[n+5]=high;
    if(j>=8&&j<=11){surfaceKind=2;Line(a,b,crestThickness*(j==10?1.5f:.8f),A(foam,j==10?.9f:.48f));if(j==10&&i%2==0)Drop(a+Vector3.up*(.2f+.25f*Mathf.Sin(Time.time*8+i)),.12f,A(foam,.8f));}
    if(j==2&&i%4==0)Drop(a+Vector3.up*.12f,.28f,A(foam,.4f));
   }
   surfaceKind=1;
  }
  Color WaveColor(float u){float crest=Mathf.Pow(Mathf.Sin(u*Mathf.PI),4);return A(Color.Lerp(deep,foam,crest*.7f),Mathf.Lerp(.38f,.68f,crest));}
  void LateUpdate()
  {
   // 물방울마다 카메라를 다시 검색하지 않고 프레임당 한 번만 확인한다.
   var camera=Camera.main;viewRight=camera?camera.transform.right:Vector3.right;viewUp=camera?camera.transform.up:Vector3.up;
   v.Clear();c.Clear();ids.Clear();uv.Clear();surfaceKind=1;
   if(boss&&boss.Engaged&&(boss.isActiveAndEnabled||MonsterAnimationPreview.IsPreviewing(boss)))
   {
    foreach(var t in boss.Tentacles)
    {
     if(!t||!t.Busy)continue;Vector3 p=t.Origin+Vector3.up*waterSurfaceOffset;
     bool warning=t.BeforeImpact;
     if(warning&&!t.Cosmetic){if(t.Circle){CircleWarning(p,t.Length,t.WarningFill);Vortex(p,t.Length,t.WarningFill,boss.CurrentPattern==KrakenBoss.Pattern.Fury);}else RectWarning(p,t.Forward,t.Length,t.Width,t.WarningFill);}
     if(t.State==KrakenTentacle.Mode.Strike)Impact(t,p);
     if(t.State==KrakenTentacle.Mode.Ground)
     {
      if(t.Circle){Vortex(p,t.Length,1-t.Progress,boss.CurrentPattern==KrakenBoss.Pattern.Capture);FoamWake(p,t.Length*(1+t.Progress*.2f),t.Progress);}
      else for(int k=1;k<=3;k++)FoamWake(p+t.Forward*t.Length*(k/4f),t.Width*(.7f+t.Progress*.15f),t.Progress);
     }
    }
    if(boss.CurrentPattern==KrakenBoss.Pattern.Wave&&boss.State==KrakenBoss.Phase.Warning)
    {
     var w=boss.Tide;float gap=Mathf.Clamp(w.safeGapWidth,0,w.width),offset=Mathf.Clamp(w.safeGapOffset,-w.width*.5f+gap*.5f,w.width*.5f-gap*.5f);Vector3 side=Vector3.Cross(Vector3.up,boss.Forward),p=boss.transform.position+Vector3.up*waterSurfaceOffset;
     float a=offset-gap*.5f,b=offset+gap*.5f;
     RectWarning(p+side*((-w.width*.5f+a)*.5f),boss.Forward,w.travelDistance,a+w.width*.5f,boss.Progress);
     RectWarning(p+side*((b+w.width*.5f)*.5f),boss.Forward,w.travelDistance,w.width*.5f-b,boss.Progress);
     Line(p+side*a,p+side*a+boss.Forward*w.travelDistance,border,A(safe,1));Line(p+side*b,p+side*b+boss.Forward*w.travelDistance,border,A(safe,1));
    }
    var wave=boss.CurrentWave;if(wave&&wave.Busy){WaveStrip(wave,-wave.Width*.5f,wave.Offset-wave.Gap*.5f);WaveStrip(wave,wave.Offset+wave.Gap*.5f,wave.Width*.5f);}
    if(boss.CurrentPattern==KrakenBoss.Pattern.Fury&&boss.State==KrakenBoss.Phase.Warning&&boss.FuryRound<0)
    {
     Vector3 p=boss.transform.position+Vector3.up*waterSurfaceOffset;
     Vortex(p,furyCrownRadius+1,boss.Progress,true);
     for(int i=0;i<4;i++){float radius=4+Mathf.Repeat(boss.Progress*12+i*3,12);Ring(p,radius-.2f,radius,A(foam,(1-boss.Progress)*.6f));}
     for(int i=0;i<8;i++)Splash(p+D(i*45)*furyCrownRadius,2.2f,Mathf.Repeat(boss.Progress+i*.11f,1),furySprayMultiplier);
    }
   }
   mesh.Clear();mesh.SetVertices(v);mesh.SetColors(c);mesh.SetUVs(0,uv);mesh.SetTriangles(ids,0);mesh.RecalculateBounds();
  }
  void OnDisable(){if(mesh)mesh.Clear();}void OnDestroy(){if(mesh)Destroy(mesh);}
 }
}
