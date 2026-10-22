using UnityEngine;
using UnityEngine.Rendering;
namespace DogLife
{
    // Purely cosmetic mastery ribbons: no collider, damage, light or post-processing changes.
    public sealed class RelicMasteryVfx : MonoBehaviour
    {
        public enum Style { Sword, Ripple, Cyclone, Shadow, Arrow, Orbit }
        const int Samples=40;
        Style style; float born,life,radius; bool persistent,synth;
        Color color; LineRenderer[] ribbons,cores; Vector3[] points=new Vector3[Samples];
        ParticleSystem sparks; ParticleSystem.Particle[] motes=new ParticleSystem.Particle[18];
        static int live;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetCount(){live=0;}
        public static RelicMasteryVfx Create(InventoryWeapon item,Style style,Vector3 position,Vector3 forward,float radius,float life,Transform parent=null)
        {
            if(live>=64||!item)return null;
            var material=Resources.Load<Material>("VFX/MemoryCollar/Soul");if(!material)return null;
            var go=new GameObject("Mastery - "+style);go.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward.sqrMagnitude>.001f?forward:Vector3.forward));
            if(parent)go.transform.SetParent(parent,true);
            var fx=go.AddComponent<RelicMasteryVfx>();live++;fx.style=style;fx.radius=radius;fx.life=life;fx.persistent=life<=0;fx.born=Time.time;
            fx.synth=item.weaponForm==InventoryWeapon.WeaponForm.Synthesized;
            fx.color=item.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid?new Color(1,.12f,.22f):new Color(.12f,.65f,1);
            int count=style==Style.Cyclone?5:style==Style.Shadow?4:3;
            fx.ribbons=new LineRenderer[count];fx.cores=new LineRenderer[count];
            for(int i=0;i<count;i++){fx.ribbons[i]=fx.MakeLine(material,"Soft ribbon "+i);fx.cores[i]=fx.MakeLine(material,"Bright edge "+i);}
            var glitter=new GameObject("Energy motes");glitter.transform.SetParent(go.transform,false);
            fx.sparks=glitter.AddComponent<ParticleSystem>();var main=fx.sparks.main;main.playOnAwake=false;main.maxParticles=18;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=fx.sparks.emission;emission.enabled=false;var shape=fx.sparks.shape;shape.enabled=false;
            var renderer=glitter.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            fx.Update();return fx;
        }
        LineRenderer MakeLine(Material material,string label)
        {
            var node=new GameObject(label);node.transform.SetParent(transform,false);var line=node.AddComponent<LineRenderer>();
            line.sharedMaterial=material;line.useWorldSpace=false;line.positionCount=Samples;line.numCapVertices=3;line.numCornerVertices=2;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.15f,.8f),new Keyframe(.5f,1),new Keyframe(.85f,.8f),new Keyframe(1,0));return line;
        }
        void Update()
        {
            if(ribbons==null)return;float age=Time.time-born;
            if(!persistent&&age>=life){Destroy(gameObject);return;}
            float t=persistent?0:Mathf.Clamp01(age/life);
            float envelope=Mathf.SmoothStep(0,1,age/.055f)*(persistent?1:1-Mathf.SmoothStep(.45f,1,t));
            for(int band=0;band<ribbons.Length;band++){
                float phase=age*7+band*Mathf.PI*2/ribbons.Length;
                for(int n=0;n<Samples;n++){
                    float u=n/(float)(Samples-1),a;Vector3 p;
                    switch(style){
                        case Style.Sword:
                            a=Mathf.Lerp(-1.25f,1.25f,u);float r=radius*(1-band*.07f);
                            p=new Vector3(Mathf.Sin(a)*r,Mathf.Sin(u*Mathf.PI)*.12f+band*.12f,Mathf.Cos(a)*r-band*.14f);break;
                        case Style.Ripple:
                            a=u*Mathf.PI*2+phase;float rr=radius*(.4f+band*.23f);
                            p=new Vector3(Mathf.Cos(a)*rr,.06f+Mathf.Sin(a*3-age*10)*.07f,Mathf.Sin(a)*rr);break;
                        case Style.Cyclone:
                            a=u*Mathf.PI*1.7f+phase;float cr=radius*(.7f+.25f*u);
                            p=new Vector3(Mathf.Cos(a)*cr,(u-.5f)*.75f+band*.15f,Mathf.Sin(a)*cr);break;
                        case Style.Shadow:
                            a=u*Mathf.PI*1.7f+phase;float sr=radius*(.7f+t*.7f);
                            p=band<2?new Vector3(Mathf.Cos(a)*sr,Mathf.Sin(a)*sr,-t*.2f):new Vector3((band==2?-1:1)*(u-.5f)*radius,u*radius-radius*.5f,u*1.4f);break;
                        case Style.Arrow:
                            a=u*Mathf.PI*3+phase;float ar=.08f+Mathf.Sin(u*Mathf.PI)*.08f;
                            p=new Vector3(Mathf.Cos(a)*ar,Mathf.Sin(a)*ar,-u*1.4f);break;
                        default:
                            a=u*Mathf.PI*1.5f+phase;float or=radius*(.8f+band*.14f);
                            p=new Vector3(Mathf.Cos(a)*or,Mathf.Sin(a*2+phase)*.12f,Mathf.Sin(a)*or);break;
                    }
                    points[n]=p;
                }
                var tint=synth&&band==ribbons.Length-1?new Color(1,.73f,.23f):color;
                var edge=Color.Lerp(tint,Color.white,.8f);
                tint.a=envelope*.32f;edge.a=envelope*.7f;
                var ribbon=ribbons[band];var core=cores[band];ribbon.SetPositions(points);core.SetPositions(points);
                ribbon.widthMultiplier=style==Style.Arrow?.11f:.24f;core.widthMultiplier=style==Style.Arrow?.018f:.028f;
                ribbon.startColor=ribbon.endColor=tint;core.startColor=core.endColor=edge;
            }
            for(int i=0;i<motes.Length;i++){
                float u=Mathf.Repeat(i*.618034f+age*.55f,1),a=i*2.39996f+age*2.5f;
                Vector3 p;
                if(style==Style.Arrow)p=new Vector3(Mathf.Cos(a)*.18f,Mathf.Sin(a)*.18f,-u*1.5f);
                else if(style==Style.Sword){a=Mathf.Lerp(-1.25f,1.25f,u);p=new Vector3(Mathf.Sin(a)*radius,Mathf.Sin(i*3+age*5)*.18f,Mathf.Cos(a)*radius-.25f);}
                else if(style==Style.Shadow)p=new Vector3(Mathf.Cos(a)*radius*(.7f+u*.7f),Mathf.Sin(a)*radius*(.7f+u*.7f),u*.4f);
                else p=new Vector3(Mathf.Cos(a)*radius*(.65f+u*.35f),(style==Style.Cyclone?u*.9f:u*.2f),Mathf.Sin(a)*radius*(.65f+u*.35f));
                var c=synth&&i%3==0?new Color(1,.78f,.3f):Color.Lerp(color,Color.white,.6f);c.a=envelope*Mathf.Sin(u*Mathf.PI)*.85f;
                motes[i].position=p;motes[i].startColor=c;motes[i].startSize=.035f+.055f*Mathf.Sin(u*Mathf.PI);motes[i].startLifetime=1;motes[i].remainingLifetime=1;
            }
            sparks.SetParticles(motes,motes.Length);
        }
        void OnDestroy(){live=Mathf.Max(0,live-1);}
    }
}
