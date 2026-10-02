using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
 [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
 public sealed class HarvestReaperVfx:MonoBehaviour
 {
  [SerializeField] private HarvestReaperBoss boss;
  [SerializeField] private Material material;
  [SerializeField] private Color amber=new Color(.95f,.46f,.12f),gold=new Color(1,.78f,.35f),umber=new Color(.32f,.12f,.055f),safe=new Color(.75f,.88f,.62f);
  [SerializeField] private float edgeWidth=.14f,warningOpacity=.14f,leafSpeed=1.2f,stormHeight=6,pumpkinSize=.8f,burstHeight=3;
  [SerializeField,Range(12,128)] private int segments=72;
  [SerializeField,Range(16,180)] private int stormLeaves=110;
  [Header("50% 폭풍 시야 확보")]
  [SerializeField,Min(.1f)] private float stormPatternHeight=1.6f;
  [SerializeField,Range(0,1)] private float stormPatternOpacity=.16f;
  [SerializeField,Range(16,180)] private int stormPatternLeaves=56;
  [Header("Attack timing fill")]
  [SerializeField,Range(0,1)] private float timingFillOpacity=.58f;
  [Header("Boss ornament / weapon trail")]
  [SerializeField] private Transform weaponTip,weaponGrip;
  [SerializeField] private float ornamentWidth=1.3f,trailLifetime=.24f,trailWidth=.65f,flameHeight=3.5f;
  readonly Vector3[] trailPoints=new Vector3[32],trailInner=new Vector3[32];readonly float[] trailTimes=new float[32];int trailCount;
  Mesh mesh;MeshRenderer surface;
  readonly List<Vector3> vertices=new List<Vector3>(30000);readonly List<Color> colors=new List<Color>(30000);readonly List<int> indices=new List<int>(45000);
  void Awake(){mesh=new Mesh{name="Harvest leaves, pumpkins and attack warnings",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;surface=GetComponent<MeshRenderer>();surface.sharedMaterial=material;surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;surface.receiveShadows=false;}
  Color A(Color c,float a){c.a=a;return c;}
  Vector3 D(float a){a*=Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));}
  void Tri(Vector3 a,Vector3 b,Vector3 c,Color color){int n=vertices.Count;vertices.Add(transform.InverseTransformPoint(a));vertices.Add(transform.InverseTransformPoint(b));vertices.Add(transform.InverseTransformPoint(c));colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(n);indices.Add(n+1);indices.Add(n+2);}
  void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){Tri(a,b,c,color);Tri(a,c,d,color);}
  void Line(Vector3 a,Vector3 b,float width,Color color){Vector3 s=Vector3.Cross(b-a,Vector3.up).normalized*width*.5f;if(s.sqrMagnitude<.001f)s=Vector3.right*width*.5f;Quad(a-s,a+s,b+s,b-s,color);}
  void Sector(Vector3 p,float inner,float outer,float yaw,float angle,Color color)
  {int n=Mathf.Max(2,Mathf.CeilToInt(segments*angle/360));for(int i=0;i<n;i++){Vector3 a=D(yaw-angle*.5f+angle*i/n),b=D(yaw-angle*.5f+angle*(i+1)/n);Quad(p+a*inner,p+a*outer,p+b*outer,p+b*inner,color);}}
  void Ring(Vector3 p,float radius,Color color){Sector(p,Mathf.Max(0,radius-edgeWidth),radius,0,360,color);}
  void Sigil(Vector3 p,float inner,float radius,float yaw,float angle,float progress,bool active)
  {
   float width=Mathf.Min(ornamentWidth,radius*.12f);
   Sector(p,Mathf.Max(inner,radius-width),radius,yaw,angle,A(umber,.65f));
   Sector(p+Vector3.up*.006f,radius-edgeWidth,radius,yaw,angle,A(gold,.95f));
   Sector(p+Vector3.up*.008f,Mathf.Max(inner,radius-width),Mathf.Max(inner,radius-width+edgeWidth*.4f),yaw,angle,A(amber,.85f));
   int count=Mathf.Max(5,Mathf.CeilToInt(angle/10));
   for(int i=0;i<count;i++)
   {
    float a=yaw-angle*.5f+angle*(i+.5f)/count;Vector3 f=D(a),side=D(a+90);float r=radius-width*.6f;
    Vector3 c=p+f*r+Vector3.up*.02f;
    Tri(c-f*width*.34f,c+side*width*.23f,c+f*width*.34f,A(gold,.75f));Tri(c-f*width*.34f,c+f*width*.34f,c-side*width*.23f,A(amber,.9f));
    if(i%2==0)Leaf(p+f*Mathf.Max(inner,radius-width*1.7f)+Vector3.up*.02f,width*.46f,a,A(gold,.48f));
   }
   // Interlocking harvest-vine bands remain entirely inside the danger sector.
   if(radius>6)
   {
    for(int band=0;band<2;band++)
    {
     float r=Mathf.Lerp(inner,radius,.48f+band*.27f);
     Sector(p+Vector3.up*.008f,r-.045f,r+.045f,yaw,angle,A(gold,.28f));
     for(int i=0;i<count;i++)
     {
      float a=yaw-angle*.5f+angle*(i+.5f)/count;Vector3 f=D(a),side=D(a+90),c=p+f*r+Vector3.up*.02f;
      float size=width*(band==0?.5f:.75f);Leaf(c,size,a+90,A(amber,.65f));
      Line(c-side*size,c+f*size*.65f,.065f,A(gold,.55f));Line(c+f*size*.65f,c+side*size,.065f,A(gold,.55f));
     }
    }
   }

  }
  // Filled area advances toward the boundary; never covers the inner safe circle or safe wedge.
  void TimingSector(Vector3 p,float inner,float outer,float yaw,float angle,float progress,float opacity=-1)
  {
   float r=Mathf.Lerp(inner,outer,Mathf.Clamp01(progress));if(r<=inner)return;
   Sector(p+Vector3.up*.012f,inner,r,yaw,angle,A(amber,opacity<0?timingFillOpacity:opacity));
   Sector(p+Vector3.up*.016f,Mathf.Max(inner,r-edgeWidth),r,yaw,angle,A(gold,.95f));
  }
  void Flames(Vector3 p,float inner,float radius,float yaw,float angle,float t)
  {
   float height=Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI)*flameHeight;
   for(int i=0;i<28;i++)
   {
    float a=yaw-angle*.5f+angle*(i+.5f)/28;float r=Mathf.Lerp(inner,radius,.35f+.6f*Mathf.Repeat(i*.618f,1));Vector3 center=p+D(a)*r,side=D(a+90)*.3f;
    Vector3 tip=center+Vector3.up*height*(.6f+.4f*Mathf.Sin(i*2.1f))+D(a+30)*height*.2f;
    Tri(center-side,center+side,tip,A(amber,.75f*(1-t)));Tri(center-side*.4f,center+side*.4f,Vector3.Lerp(center,tip,.78f),A(gold,.85f*(1-t)));
   }
  }
  void WeaponTrail(bool active)
  {
   if(!active||!weaponTip){trailCount=0;return;}
   Vector3 tip=weaponTip.position;
   if(trailCount==0||Vector3.Distance(tip,trailPoints[trailCount-1])>.035f)
   {
    if(trailCount==trailPoints.Length){for(int i=1;i<trailCount;i++){trailPoints[i-1]=trailPoints[i];trailInner[i-1]=trailInner[i];trailTimes[i-1]=trailTimes[i];}trailCount--;}
    trailPoints[trailCount]=tip;trailInner[trailCount]=tip-(weaponGrip?(tip-weaponGrip.position).normalized:Vector3.up)*trailWidth;trailTimes[trailCount++]=Time.time;
   }
   Vector3 outward=weaponGrip?(tip-weaponGrip.position).normalized:Vector3.up;
   Line(tip-outward*trailWidth,tip,trailWidth*.18f,A(gold,.95f));
   for(int i=1;i<trailCount;i++)
   {
    float fade=Mathf.Clamp01(1-(Time.time-trailTimes[i])/Mathf.Max(.01f,trailLifetime));if(fade<=0)continue;
    // Each sample keeps its original blade orientation; the ribbon follows the cut.
    Quad(trailPoints[i-1],trailPoints[i],trailInner[i],trailInner[i-1],A(amber,fade*.62f));
    Quad(trailPoints[i-1],trailPoints[i],Vector3.Lerp(trailPoints[i],trailInner[i],.28f),Vector3.Lerp(trailPoints[i-1],trailInner[i-1],.28f),A(gold,fade*.8f));
    Line(trailPoints[i-1],trailPoints[i],trailWidth*.2f,A(gold,fade*.95f));
   }
  }
  static readonly Vector2[] Maple={new Vector2(0,-1),new Vector2(-.15f,-.5f),new Vector2(-.65f,-.55f),new Vector2(-.5f,-.1f),new Vector2(-1,.1f),new Vector2(-.6f,.3f),new Vector2(-.72f,.72f),new Vector2(-.25f,.5f),new Vector2(0,1),new Vector2(.25f,.5f),new Vector2(.72f,.72f),new Vector2(.6f,.3f),new Vector2(1,.1f),new Vector2(.5f,-.1f),new Vector2(.65f,-.55f),new Vector2(.15f,-.5f)};
  void Leaf(Vector3 p,float size,float spin,Color color)
  {
   Vector3 x=D(spin)*size,z=D(spin+90)*size,mid=p+Vector3.up*size*.13f;
   for(int i=0;i<Maple.Length;i++){Vector2 a=Maple[i],b=Maple[(i+1)%Maple.Length];Tri(mid,p+x*a.x+z*a.y,p+x*b.x+z*b.y,i%3==0?A(Color.Lerp(color,umber,.25f),color.a):color);}
   Line(p-z,p+z,size*.05f,A(gold,color.a*.85f));Line(p,p+(x+z)*.65f,size*.025f,A(gold,color.a*.5f));Line(p,p+(-x+z)*.65f,size*.025f,A(gold,color.a*.5f));
  }
  void Pumpkin(Vector3 p,float size,float alpha)
  {
   int longitude=24,latitude=10;
   for(int j=0;j<latitude;j++)for(int i=0;i<longitude;i++)
   {
    Vector3 a=Bulb(i,j,longitude,latitude),b=Bulb(i+1,j,longitude,latitude),c=Bulb(i+1,j+1,longitude,latitude),d=Bulb(i,j+1,longitude,latitude);
    Quad(p+a*size,p+b*size,p+c*size,p+d*size,A(Color.Lerp(umber,amber,.55f+.35f*Mathf.Sin(i*Mathf.PI*2/3)),alpha));
   }
   Line(p+Vector3.up*size*.8f,p+Vector3.up*size*1.15f+Vector3.right*size*.14f,size*.22f,A(umber,alpha));
   Vector3 f=boss?boss.Forward:Vector3.forward,r=Vector3.Cross(Vector3.up,f);Vector3 face=p+f*size*.85f+Vector3.up*size*.12f;
   for(int side=-1;side<=1;side+=2){Vector3 eye=face+r*side*size*.32f;Tri(eye-r*size*.17f,eye+r*size*.17f,eye+Vector3.up*size*.24f,A(gold,alpha));}
   Line(face-Vector3.up*size*.27f-r*size*.38f,face-Vector3.up*size*.27f+r*size*.38f,size*.09f,A(gold,alpha));
  }
  Vector3 Bulb(int i,int j,int n,int m){float a=i*Mathf.PI*2/n,b=j*Mathf.PI/m;float radius=Mathf.Sin(b)*(.88f+.12f*Mathf.Cos(a*8));return new Vector3(Mathf.Cos(a)*radius,Mathf.Cos(b)*.8f,Mathf.Sin(a)*radius);}
  void Burst(Vector3 p,float radius,float t)
  {Ring(p,radius,A(gold,1-t));Ring(p,Mathf.Max(0,radius*(.5f+.5f*t)),A(amber,(1-t)*.65f));for(int i=0;i<48;i++){float a=i*137.5f;Vector3 v=p+D(a)*radius*t+Vector3.up*Mathf.Sin(t*Mathf.PI)*burstHeight*(.4f+(i%4)*.2f);Leaf(v,.28f,a+Time.time*80,A(i%2==0?gold:amber,1-t));}}
  void LateUpdate()
  {
   vertices.Clear();colors.Clear();indices.Clear();
   if(boss&&boss.isActiveAndEnabled&&boss.Engaged)
   {
    Vector3 p=boss.Ground;var state=boss.State;var kind=boss.CurrentPattern;var cfg=boss.CurrentAttack;float yaw=Mathf.Atan2(boss.Forward.x,boss.Forward.z)*Mathf.Rad2Deg,t=boss.Progress;
    bool warning=state==HarvestReaperBoss.Phase.Warning,active=state==HarvestReaperBoss.Phase.Active;
    float alpha=warningOpacity*.45f;
    bool melee=kind==HarvestReaperBoss.Pattern.Sweep||kind==HarvestReaperBoss.Pattern.Cleave||kind==HarvestReaperBoss.Pattern.Shockwave;
    // Match the existing damage frame, including the weapon wind-up inside the active animation.
    float fill=melee?Mathf.Clamp01((warning?t*cfg.warning:cfg.warning+t*cfg.duration)/Mathf.Max(.01f,cfg.warning+cfg.duration*cfg.hitTime)):(warning?t:1f);
    WeaponTrail(active&&(kind==HarvestReaperBoss.Pattern.Sweep||kind==HarvestReaperBoss.Pattern.Cleave||kind==HarvestReaperBoss.Pattern.Shockwave));
    if(warning||active)
    {
     if(kind==HarvestReaperBoss.Pattern.SafeWedge)
     {
      var sp=boss.Special;
      if(boss.Gathering)
      {
       Sigil(p,sp.innerRadius,sp.outerRadius,0,360,t,active);
       Sector(p,sp.innerRadius,sp.outerRadius,0,360,A(amber,alpha));
       TimingSector(p,sp.innerRadius,sp.outerRadius,0,360,warning?t:1);
       Ring(p,sp.innerRadius,A(safe,1));
      }
      else
      {
       // The wedge is safe ONLY inside the former safe circle.
       Sigil(p,0,sp.innerRadius,boss.SafeYaw+180,360-sp.safeAngle,t,active);
       Sector(p,0,sp.innerRadius,boss.SafeYaw+180,360-sp.safeAngle,A(amber,active?.5f:alpha));
       TimingSector(p,0,sp.innerRadius,boss.SafeYaw+180,360-sp.safeAngle,warning?t:1);
       Sector(p,0,sp.innerRadius,boss.SafeYaw,sp.safeAngle,A(safe,.12f));
       for(int sign=-1;sign<=1;sign+=2)Line(p,p+D(boss.SafeYaw+sign*sp.safeAngle*.5f)*sp.innerRadius,edgeWidth,A(safe,1));
       if(active){Flames(p,0,sp.innerRadius,boss.SafeYaw+180,360-sp.safeAngle,t);for(int i=0;i<32;i++){float a=boss.SafeYaw+sp.safeAngle*.5f+(360-sp.safeAngle)*(i+.5f)/32;Leaf(p+D(a)*sp.innerRadius*.8f+Vector3.up*Mathf.Sin(t*Mathf.PI)*burstHeight,.4f,a,A(gold,1-t));}}
      }
     }
     else if(kind==HarvestReaperBoss.Pattern.Cleave)
     {
      Vector3 side=Vector3.Cross(Vector3.up,boss.Forward)*cfg.width*.5f,end=p+boss.Forward*cfg.range;
      Quad(p-side,p+side,end+side,end-side,A(amber,active?.4f:alpha));Line(p-side,end-side,edgeWidth,A(gold,.9f));Line(p+side,end+side,edgeWidth,A(gold,.9f));Line(end-side,end+side,edgeWidth,A(gold,.9f));
      Vector3 filledEnd=Vector3.Lerp(p,end,fill),lift=Vector3.up*.012f;
      Quad(p-side+lift,p+side+lift,filledEnd+side+lift,filledEnd-side+lift,A(amber,timingFillOpacity));
      Line(filledEnd-side+lift,filledEnd+side+lift,edgeWidth,A(gold,.95f));
      for(int mark=1;mark<=10;mark++){
       Vector3 at=Vector3.Lerp(p,end,mark/11f);Vector3 across=side*.55f;
       Line(at-across-boss.Forward*.4f,at,.09f,A(gold,.65f));Line(at+across-boss.Forward*.4f,at,.09f,A(gold,.65f));}
      if(active){for(int k=0;k<18;k++){Vector3 at=Vector3.Lerp(p,end,k/17f);Vector3 tip=at+Vector3.up*Mathf.Sin(t*Mathf.PI)*burstHeight*(1+.3f*Mathf.Sin(k));Tri(at-side*.35f,at+side*.35f,tip,A(amber,.65f*(1-t)));}Line(p,end,.4f,A(gold,1-t));for(int i=0;i<20;i++)Leaf(Vector3.Lerp(p,end,i/19f)+Vector3.up*Mathf.Sin(t*Mathf.PI)*burstHeight,.5f,i*57,A(amber,1-t));}
     }
     else if(kind==HarvestReaperBoss.Pattern.Sweep)
     {
      Sigil(p,0,cfg.range,yaw,cfg.angle,t,active);
      Sector(p,0,cfg.range,yaw,cfg.angle,A(amber,active?.28f:alpha));Sector(p,cfg.range-edgeWidth,cfg.range,yaw,cfg.angle,A(gold,.9f));
      TimingSector(p,0,cfg.range,yaw,cfg.angle,fill);
      for(int sign=-1;sign<=1;sign+=2)Line(p,p+D(yaw+sign*cfg.angle*.5f)*cfg.range,edgeWidth,A(gold,.8f));
      if(active)
      {
       // Match the reversed blade stroke and use the exact damage range/angle.
       float stroke=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.62f,t));
       float head=yaw+cfg.angle*.5f-cfg.angle*stroke;
       float tail=Mathf.Min(yaw+cfg.angle*.5f,head+55f),arc=Mathf.Max(.01f,tail-head);
       float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,1,t));
       Vector3 slash=p+Vector3.up*1.35f;
       Sector(slash,cfg.range*.65f,cfg.range,head+arc*.5f,arc,A(amber,.62f*fade));
       Sector(slash+Vector3.up*.015f,cfg.range*.94f,cfg.range,head+arc*.5f,arc,A(gold,.95f*fade));
       Sector(slash-Vector3.up*.1f,cfg.range*.45f,cfg.range*.87f,head+arc*.5f,arc,A(amber,.2f*fade));
      }
      if(active&&weaponTip){for(int i=0;i<8;i++)Leaf(weaponTip.position-boss.Forward*(i*.12f)+Vector3.up*Mathf.Sin(i*1.7f+t*8)*.25f,.12f+i*.015f,yaw+i*37,A(amber,(1-t)*.7f));}
     }
     else if(kind==HarvestReaperBoss.Pattern.Shockwave||kind==HarvestReaperBoss.Pattern.Storm)
     {
      Sigil(p,0,cfg.range,0,360,t,active);
      if(active&&kind==HarvestReaperBoss.Pattern.Shockwave)Flames(p,0,cfg.range,0,360,t);
      Sector(p,0,cfg.range,0,360,A(amber,alpha));Ring(p,cfg.range,A(gold,.9f));Ring(p,cfg.range-.4f,A(amber,.6f));
      // 50% 폭풍은 바닥을 읽을 수 있게 얇게 표시하고 가장자리는 유지합니다.
      TimingSector(p,0,cfg.range,0,360,fill,kind==HarvestReaperBoss.Pattern.Storm?stormPatternOpacity:-1);
      if(active&&kind==HarvestReaperBoss.Pattern.Shockwave){Ring(p,cfg.range*t,A(gold,1-t));Burst(p,cfg.range,t);}
      if(active&&kind==HarvestReaperBoss.Pattern.Storm)for(int band=0;band<5;band++)for(int j=0;j<64;j++)
      {
       float u=j/64f,v=(j+1)/64f,spin=Time.time*leafSpeed*75+band*72;
       Vector3 a=p+D(spin+u*250)*(cfg.range*(.35f+u*.6f))+Vector3.up*(stormPatternHeight*(u*.75f+band*.05f));
       Vector3 b=p+D(spin+v*250)*(cfg.range*(.35f+v*.6f))+Vector3.up*(stormPatternHeight*(v*.75f+band*.05f));
       Line(a,b,.12f+.4f*Mathf.Sin(u*Mathf.PI),A(band%2==0?amber:gold,.24f*Mathf.Sin(u*Mathf.PI)));
      }
      if(kind==HarvestReaperBoss.Pattern.Storm)for(int i=0;i<stormPatternLeaves;i++){float u=i/(float)stormPatternLeaves,a=i*137.5f+Time.time*leafSpeed*60,r=cfg.range*(.25f+.75f*Mathf.Repeat(u*7,1));Leaf(p+D(a)*r+Vector3.up*(active?Mathf.Repeat(u+Time.time*leafSpeed*.15f,1)*stormPatternHeight:.2f),active?.4f:.23f,a,A(i%3==0?gold:amber,active?.85f:.45f));}
     }
     else if(kind==HarvestReaperBoss.Pattern.Pumpkins)for(int i=0;i<18;i++)Leaf(p+D(i*20+Time.time*40)*2+Vector3.up*(1+i*.2f),.3f,i*50,A(gold,.65f));
    }
    if(boss.OuterStormActive)
    {
     var sp=boss.Special;
     Sector(p,sp.innerRadius,sp.outerRadius,0,360,A(umber,.3f));Ring(p,sp.innerRadius,A(gold,.95f));
     // Continuous annular leaf storm: no particles or ribbons cross the safe circle.
     for(int band=0;band<4;band++)for(int j=0;j<48;j++)
     {
      float u=j/48f,v=(j+1)/48f,spin=Time.time*65+band*90;
      float r=Mathf.Lerp(sp.innerRadius+.25f,sp.outerRadius,.12f+band*.23f);
      Vector3 a=p+D(spin+u*300)*r+Vector3.up*(.3f+Mathf.Sin(u*Mathf.PI)*stormHeight*.7f);
      Vector3 b=p+D(spin+v*300)*r+Vector3.up*(.3f+Mathf.Sin(v*Mathf.PI)*stormHeight*.7f);
      Line(a,b,.12f+.22f*Mathf.Sin(u*Mathf.PI),A(band%2==0?amber:gold,.32f));
     }
     for(int i=0;i<stormLeaves;i++)
     {
      float r=Mathf.Lerp(sp.innerRadius+.35f,sp.outerRadius-.3f,Mathf.Repeat(i*.618f,1)),a=i*137.5f+Time.time*(45+i%5*5);
      Leaf(p+D(a)*r+Vector3.up*(.2f+Mathf.Repeat(i*.37f+Time.time*.3f,1)*stormHeight),.22f+(i%4)*.055f,a,A(i%3==0?gold:amber,.85f));
     }
    }
    var pc=boss.Pumpkin;
    foreach(var patch in boss.Patches)
    {
     Vector3 at=patch.point+Vector3.up*.07f;float age=Time.time-patch.born;
     if(age<pc.explosionWarning){Sigil(at,0,pc.explosionRadius,0,360,age/Mathf.Max(.01f,pc.explosionWarning),false);Sector(at,0,pc.explosionRadius,0,360,A(amber,alpha));Ring(at,pc.explosionRadius,A(gold,1));TimingSector(at,0,pc.explosionRadius,0,360,age/Mathf.Max(.01f,pc.explosionWarning));Pumpkin(at+Vector3.up*(pumpkinSize+Mathf.Max(0,pc.spawnHeight-age*pc.fallSpeed)),pumpkinSize*(1+.05f*Mathf.Sin(age*10)),1);}
     else{float life=age-pc.explosionWarning,fade=Mathf.Clamp01((pc.poolDuration-life));Sector(at,0,pc.poolRadius,0,360,A(umber,.34f*fade));Ring(at,pc.poolRadius,A(amber,.8f*fade));for(int i=0;i<10;i++)Leaf(at+D(i*137.5f+Time.time*15)*pc.poolRadius*.7f,.3f,i*39,A(amber,.65f*fade));if(life<pc.explosionDuration){float burst=life/Mathf.Max(.01f,pc.explosionDuration);Burst(at,pc.explosionRadius,burst);Flames(at,0,pc.explosionRadius,0,360,burst);}}
    }
   }
   mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
  }
  void OnDisable(){trailCount=0;if(mesh)mesh.Clear();}
  void OnDestroy(){if(mesh)Destroy(mesh);}
 }
}
