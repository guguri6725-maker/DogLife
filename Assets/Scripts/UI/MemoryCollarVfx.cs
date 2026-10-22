using UnityEngine;
namespace DogLife {
 // Local, bounded VFX: no physics or damage components, no global post-processing.
 public sealed class MemoryCollarVfx:MonoBehaviour {
  public Transform floatingCollar, restingStone;
  public Material soulMaterial;
  [SerializeField,Min(.5f)] float gatherDuration=3.2f;
  [SerializeField,Range(8,40)] int soulCount=24;
  [SerializeField] float gatherRadius=3.8f,idleRadius=.65f;
  LineRenderer[] streams, soulHeads; LineRenderer halo,flareA,flareB; Light soulLight;
  ParticleSystem motes; float born; Vector3 collarPosition; Vector3 stoneScale;
  public bool Ready=>Time.time-born>=gatherDuration;
  LineRenderer Line(string name,int count,float width){
   var go=new GameObject(name);go.transform.SetParent(transform,false);var l=go.AddComponent<LineRenderer>();
   l.sharedMaterial=soulMaterial;l.useWorldSpace=false;l.positionCount=count;l.widthMultiplier=width;l.numCapVertices=4;l.numCornerVertices=3;
   l.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;l.receiveShadows=false;
   var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(new Color(.12f,.55f,1),0),new GradientColorKey(new Color(.35f,.9f,1),.7f),new GradientColorKey(new Color(1,.9f,.65f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.5f),new GradientAlphaKey(1,1)});l.colorGradient=g;
   l.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.75f,.7f),new Keyframe(1,1));return l;
  }
  void Start(){
   born=Time.time;collarPosition=floatingCollar.localPosition;stoneScale=restingStone.localScale;floatingCollar.localScale=Vector3.zero;
   streams=new LineRenderer[soulCount];soulHeads=new LineRenderer[soulCount];for(int i=0;i<soulCount;i++){streams[i]=Line("Gathering soul "+i,24,.11f);soulHeads[i]=Line("Soul heart "+i,2,.19f);soulHeads[i].widthCurve=AnimationCurve.Linear(0,1,1,1);soulHeads[i].startColor=soulHeads[i].endColor=new Color(.7f,.9f,1,.95f);}
   halo=Line("Memory halo",81,.035f);flareA=Line("Soul flare horizontal",3,.15f);flareB=Line("Soul flare vertical",3,.09f);
   var lightGo=new GameObject("Gentle soul light");lightGo.transform.SetParent(transform,false);lightGo.transform.localPosition=Vector3.up*.8f;soulLight=lightGo.AddComponent<Light>();soulLight.color=new Color(.22f,.7f,1);soulLight.range=3;soulLight.shadows=LightShadows.None;
   var particleGo=new GameObject("Soul dust");particleGo.transform.SetParent(transform,false);particleGo.transform.localPosition=Vector3.up*.8f;
   motes=particleGo.AddComponent<ParticleSystem>();motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=motes.main;main.startLifetime=1.5f;main.startSpeed=.25f;main.startSize=new ParticleSystem.MinMaxCurve(.018f,.065f);main.startColor=new Color(.45f,.8f,1,.7f);main.maxParticles=100;main.simulationSpace=ParticleSystemSimulationSpace.Local;
   var emission=motes.emission;emission.rateOverTime=8;var shape=motes.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.5f;
   var size=motes.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
   var renderer=motes.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=soulMaterial;motes.Play();
  }
  bool burst;
  void Update(){
   if(streams==null)return;float age=Time.time-born,t=Mathf.Clamp01(age/gatherDuration);
   float reveal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.57f,.93f,t));floatingCollar.localScale=Vector3.one*reveal;
   floatingCollar.localPosition=collarPosition+Vector3.up*(Mathf.Sin(age*1.4f)*.035f-(1-reveal)*.25f);floatingCollar.localRotation=Quaternion.Euler(0,Mathf.Sin(age*.55f)*12,Mathf.Sin(age*.9f)*2);
   restingStone.localScale=stoneScale*Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.45f));
   for(int i=0;i<streams.Length;i++){
    var line=streams[i];line.enabled=!Ready||i<5;soulHeads[i].enabled=line.enabled;if(!line.enabled)continue;
    float stagger=(i%6)*.045f,p=Mathf.Clamp01((t-stagger)/(1-stagger));
    for(int j=0;j<24;j++){
     float q=j/23f,u=Mathf.Clamp01(p-(1-q)*.16f),angle=i*2.39996f+u*6.8f;
     float radius=gatherRadius*(1-u)*(1-u)+.035f;
     Vector3 v=new Vector3(Mathf.Cos(angle)*radius,.8f+Mathf.Sin(u*Mathf.PI)*(1+(i%4)*.22f)+(i%3)*.12f*(1-u),Mathf.Sin(angle)*radius);
     if(Ready){angle=age*.6f+i*Mathf.PI*2/5-q*.45f;radius=idleRadius;v=new Vector3(Mathf.Cos(angle)*radius,.75f+Mathf.Sin(angle*2+i)*.22f,Mathf.Sin(angle)*radius);}
     line.SetPosition(j,v);
    }
    line.widthMultiplier=Ready?.035f:.11f*Mathf.Sin(Mathf.PI*Mathf.Clamp01(p));
    var head=line.GetPosition(23);soulHeads[i].SetPosition(0,head-Vector3.up*.09f);soulHeads[i].SetPosition(1,head+Vector3.up*.09f);soulHeads[i].widthMultiplier=Ready?.09f:.18f*Mathf.Sin(Mathf.PI*p);
   }
   float flash=Mathf.Exp(-Mathf.Pow((age-gatherDuration*.79f)/.22f,2));
   soulLight.intensity=.25f+flash*2.4f;
   float ringRadius=Ready?.61f:.15f+1.6f*Mathf.Clamp01((t-.6f)/.4f);
   for(int j=0;j<81;j++){float a=j*Mathf.PI*2/80;halo.SetPosition(j,new Vector3(Mathf.Cos(a)*ringRadius,.23f,Mathf.Sin(a)*ringRadius));}
   halo.widthMultiplier=Ready?.045f:.08f*(1-t);
   flareA.widthMultiplier=flash*.15f;flareB.widthMultiplier=flash*.12f;
   flareA.SetPositions(new[]{new Vector3(-1.1f,.8f,0),new Vector3(0,.8f,0),new Vector3(1.1f,.8f,0)});
   flareB.SetPositions(new[]{new Vector3(0,-.1f,0),new Vector3(0,.8f,0),new Vector3(0,1.7f,0)});
   if(!burst&&t>.78f){burst=true;motes.Emit(65);}
  }
 }
}
