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
  Mesh mesh;readonly List<Vector3> v=new List<Vector3>(24000);readonly List<Color> c=new List<Color>(24000);readonly List<int> ids=new List<int>(36000);
  Color A(Color a,float alpha){a.a=alpha;return a;}
  Vector3 D(float angle){float a=angle*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));}
  void Awake(){mesh=new Mesh{name="Kraken water / pearl foam / filled warnings",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;var r=GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
  void Tri(Vector3 a,Vector3 b,Vector3 d,Color color){int n=v.Count;v.Add(transform.InverseTransformPoint(a));v.Add(transform.InverseTransformPoint(b));v.Add(transform.InverseTransformPoint(d));c.Add(color);c.Add(color);c.Add(color);ids.Add(n);ids.Add(n+1);ids.Add(n+2);}
  void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e,Color color){Tri(a,b,d,color);Tri(a,d,e,color);}
  void Line(Vector3 a,Vector3 b,float width,Color color){Vector3 side=Vector3.Cross(b-a,Vector3.up).normalized*width*.5f;if(side.sqrMagnitude<.0001f)side=Vector3.right*width*.5f;Quad(a-side,a+side,b+side,b-side,color);}
  void Ring(Vector3 p,float inner,float outer,Color color){for(int i=0;i<segments;i++){Vector3 a=D(i*360f/segments),b=D((i+1)*360f/segments);Quad(p+a*inner,p+a*outer,p+b*outer,p+b*inner,color);}}
  void Drop(Vector3 p,float size,Color color){Vector3 a=Vector3.right*size,b=Vector3.forward*size;Tri(p-a,p+b,p+Vector3.up*size*2,color);Tri(p+b,p+a,p+Vector3.up*size*2,color);Tri(p+a,p-b,p+Vector3.up*size*2,color);Tri(p-b,p-a,p+Vector3.up*size*2,color);}
  void CircleWarning(Vector3 p,float radius,float t)
  {
   Ring(p,0,radius,A(deep,.22f));Ring(p+Vector3.up*.01f,0,radius*t,A(ocean,fillOpacity));
   Ring(p+Vector3.up*.02f,Mathf.Max(0,radius*t-border),radius*t,A(foam,.95f));
   Ring(p,radius-border,radius,A(foam,.95f));Ring(p,radius-.45f,radius-.34f,A(ocean,.8f));
   for(int i=0;i<16;i++){float a=i*22.5f;Vector3 f=D(a),s=D(a+90),at=p+f*(radius-.23f);Tri(at-f*.17f,at+s*.11f,at+f*.17f,A(foam,.85f));Tri(at-f*.17f,at+f*.17f,at-s*.11f,A(ocean,.9f));}
  }
  void RectWarning(Vector3 p,Vector3 forward,float length,float width,float t)
  {
   Vector3 side=Vector3.Cross(Vector3.up,forward)*width*.5f,end=p+forward*length,tip=Vector3.Lerp(p,end,t);
   Quad(p-side,p+side,end+side,end-side,A(deep,.22f));Quad(p-side+Vector3.up*.01f,p+side+Vector3.up*.01f,tip+side+Vector3.up*.01f,tip-side+Vector3.up*.01f,A(ocean,fillOpacity));
   Line(p-side,end-side,border,A(foam,.95f));Line(p+side,end+side,border,A(foam,.95f));Line(end-side,end+side,border,A(foam,.95f));Line(tip-side,tip+side,border,A(foam,1));
   for(int i=1;i<=8;i++){Vector3 at=Vector3.Lerp(p,end,i/9f);Line(at-side*.55f-forward*.25f,at,.075f,A(foam,.6f));Line(at,at+side*.55f-forward*.25f,.075f,A(foam,.6f));}
  }
  void Splash(Vector3 p,float radius,float t)
  {
   float rise=Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI);
   Ring(p,Mathf.Max(0,radius*t-.25f),radius*t,A(foam,1-t));
   for(int i=0;i<36;i++){float a=i*137.5f;Vector3 at=p+D(a)*radius*(.3f+.7f*t);Drop(at+Vector3.up*rise*sprayHeight*(.3f+.7f*Mathf.Repeat(i*.618f,1)),.08f+(i%4)*.035f,A(i%3==0?foam:ocean,1-t));}
   for(int i=0;i<24;i++){Vector3 f=D(i*15f),side=D(i*15+90)*.2f;Vector3 at=p+f*radius*.5f;Tri(at-side,at+side,at+f*radius*.2f+Vector3.up*rise*sprayHeight,A(ocean,.5f*(1-t)));}
  }
  Vector3 WavePoint(KrakenWave wave,float x,float u)
  {
   float crest=Mathf.Sin(u*Mathf.PI),ripple=Mathf.Sin(x*1.7f+Time.time*7)*.16f+Mathf.Sin(x*3.2f-Time.time*5)*.08f;
   return wave.transform.TransformPoint(new Vector3(x,crest*(wave.Height+ripple),-u*3+Mathf.Sin(u*Mathf.PI)*.65f));
  }
  void WaveStrip(KrakenWave wave,float left,float right)
  {
   if(right-left<.01f)return;
   for(int i=0;i<28;i++)for(int j=0;j<8;j++)
   {
    float x=Mathf.Lerp(left,right,i/28f),xx=Mathf.Lerp(left,right,(i+1)/28f),u=j/8f,w=(j+1)/8f;
    Vector3 a=WavePoint(wave,x,u),b=WavePoint(wave,xx,u),d=WavePoint(wave,xx,w),e=WavePoint(wave,x,w);
    Quad(a,b,d,e,A(Color.Lerp(deep,foam,Mathf.Pow(Mathf.Sin(u*Mathf.PI),5)),.65f));
    if(j>=3&&j<=5){Line(a,b,crestThickness*(j==4?1.2f:.55f),A(foam,j==4?.95f:.48f));if(i%2==0)Drop(a+Vector3.up*(.2f+.25f*Mathf.Sin(Time.time*8+i)),.13f,A(foam,.8f));}
    if(j==1&&i%3==0)Drop(a+Vector3.up*.12f,.18f,A(foam,.65f));
   }
  }
  void LateUpdate()
  {
   v.Clear();c.Clear();ids.Clear();
   if(boss&&boss.Engaged&&boss.isActiveAndEnabled)
   {
    foreach(var t in boss.Tentacles)
    {
     if(!t||!t.Busy)continue;Vector3 p=t.Origin+Vector3.up*.08f;
     bool warning=t.BeforeImpact;
     if(warning&&!t.Cosmetic){if(t.Circle)CircleWarning(p,t.Length,t.WarningFill);else RectWarning(p,t.Forward,t.Length,t.Width,t.WarningFill);}
     if(t.State==KrakenTentacle.Mode.Strike){Vector3 at=t.Circle?p:p+t.Forward*t.Length*.6f;Splash(at,t.Circle?t.Length:t.Width*1.5f,t.Progress);}
     if(t.State==KrakenTentacle.Mode.Ground){Vector3 at=t.Circle?p:p+t.Forward*t.Length*.5f;Ring(at,Mathf.Max(0,t.Width-.13f),t.Width,A(foam,.3f));}
    }
    if(boss.CurrentPattern==KrakenBoss.Pattern.Wave&&boss.State==KrakenBoss.Phase.Warning)
    {
     var w=boss.Tide;float gap=Mathf.Clamp(w.safeGapWidth,0,w.width),offset=Mathf.Clamp(w.safeGapOffset,-w.width*.5f+gap*.5f,w.width*.5f-gap*.5f);Vector3 side=Vector3.Cross(Vector3.up,boss.Forward),p=boss.transform.position+Vector3.up*.1f;
     float a=offset-gap*.5f,b=offset+gap*.5f;
     RectWarning(p+side*((-w.width*.5f+a)*.5f),boss.Forward,w.travelDistance,a+w.width*.5f,boss.Progress);
     RectWarning(p+side*((b+w.width*.5f)*.5f),boss.Forward,w.travelDistance,w.width*.5f-b,boss.Progress);
     Line(p+side*a,p+side*a+boss.Forward*w.travelDistance,border,A(safe,1));Line(p+side*b,p+side*b+boss.Forward*w.travelDistance,border,A(safe,1));
    }
    var wave=boss.CurrentWave;if(wave&&wave.Busy){WaveStrip(wave,-wave.Width*.5f,wave.Offset-wave.Gap*.5f);WaveStrip(wave,wave.Offset+wave.Gap*.5f,wave.Width*.5f);}
    if(boss.CurrentPattern==KrakenBoss.Pattern.Fury&&boss.State==KrakenBoss.Phase.Warning&&boss.FuryRound<0)
    {
     Vector3 p=boss.transform.position+Vector3.up*.1f;for(int i=0;i<4;i++){float radius=3+Mathf.Repeat(boss.Progress*9+i*2,9);Ring(p,radius-.12f,radius,A(foam,(1-boss.Progress)*.6f));}
    }
   }
   mesh.Clear();mesh.SetVertices(v);mesh.SetColors(c);mesh.SetTriangles(ids,0);mesh.RecalculateBounds();
  }
  void OnDisable(){if(mesh)mesh.Clear();}void OnDestroy(){if(mesh)Destroy(mesh);}
 }
}
