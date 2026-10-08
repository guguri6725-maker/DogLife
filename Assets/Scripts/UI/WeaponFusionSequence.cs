using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // Hierarchy에서 만든 무대/파편/광륜을 움직이며, 아이템 지급은 담당하지 않습니다.
    [DefaultExecutionOrder(1000)]
    public sealed class WeaponFusionSequence : MonoBehaviour
    {
        public Transform firstSocket,secondSocket,resultSocket,fragments;
        public LineRenderer outerRing,innerRing,shockRing;
        public Light soulLight;
        public Renderer sigil;
        public ParticleSystem soulParticles;
        public Shader revealShader;
        [Min(1)] public float duration=4;
        [Min(.1f)] public float weaponSize=1.6f,orbitRadius=1.1f,cameraDistance=4.6f;
        [Min(.5f)] public float displayHeight=1.55f;
        [Min(0)] public float rotationSpeed=24,fragmentSpeed=260,lightIntensity=3,shake=.035f;
        public Color animalColor=new Color(.13f,.65f,1),humanColor=new Color(1,.16f,.23f);
        readonly List<GameObject> models=new List<GameObject>();
        readonly List<Canvas> hiddenHud=new List<Canvas>();
        readonly List<Material> materials=new List<Material>();
        readonly List<Material> firstMaterials=new List<Material>(),secondMaterials=new List<Material>(),resultMaterials=new List<Material>();
        Camera cameraTarget;PlayerCamera controller;bool controllerEnabled;
        Vector3 cameraPosition,cameraGoal;Quaternion cameraRotation;float cameraFov,elapsed;
        Color color;InventoryWeapon result;bool active,released;
        Material sigilMaterial;
        public bool Completed {get;private set;}
        public string Status=>Completed?"합성 완료\n"+result.displayName+"\n"+(result.boneOrigin==InventoryWeapon.BoneOrigin.Dog?"동물형":"인간형")+" · "+Category(result.weaponCategory):elapsed/duration<.25f?"두 영혼이 깨어납니다":elapsed/duration<.52f?"뼈와 영혼이 하나로 모입니다":"새로운 무기가 모습을 드러냅니다";
        static string Category(InventoryWeapon.WeaponCategory c){string[] names={"검","창","도끼","활","단검","부메랑"};return (int)c<names.Length?names[(int)c]:c.ToString();}
        public void Begin(InventoryWeapon a,InventoryWeapon b,InventoryWeapon output)
        {
            result=output;color=output.boneOrigin==InventoryWeapon.BoneOrigin.Dog?animalColor:humanColor;
            // 동적으로 로드되는 기존 HUD는 연출 동안만 숨기고 원래 켜져 있던 것만 복구합니다.
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(canvas.enabled&&canvas.renderMode==RenderMode.ScreenSpaceOverlay&&!canvas.GetComponent<EconomyWindow>())
                {hiddenHud.Add(canvas);canvas.enabled=false;}
            if(sigil){sigilMaterial=new Material(sigil.sharedMaterial);materials.Add(sigilMaterial);sigil.sharedMaterial=sigilMaterial;sigilMaterial.SetColor("_Tint",color);}
            if(soulParticles){var main=soulParticles.main;main.startColor=color;main.useUnscaledTime=true;soulParticles.Play();}
            cameraTarget=Camera.main;
            if(cameraTarget)
            {
                cameraPosition=cameraTarget.transform.position;cameraRotation=cameraTarget.transform.rotation;cameraFov=cameraTarget.fieldOfView;
                controller=cameraTarget.GetComponent<PlayerCamera>();if(controller){controllerEnabled=controller.enabled;controller.enabled=false;}
                Vector3 side=Vector3.ProjectOnPlane(cameraPosition-transform.position,Vector3.up).normalized;if(side.sqrMagnitude<.01f)side=Vector3.back;
                cameraGoal=transform.position+Vector3.up*(displayHeight+.7f)+side*cameraDistance;
                transform.rotation=Quaternion.LookRotation(-side,Vector3.up);
            }
            CreateVisual(a,firstSocket,firstMaterials);CreateVisual(b,secondSocket,secondMaterials);CreateVisual(output,resultSocket,resultMaterials);
            active=true;Tick(0);
        }
        void CreateVisual(InventoryWeapon item,Transform socket,List<Material> group)
        {
            if(!item||!item.visualPrefab||!socket)return;
            var model=Instantiate(item.visualPrefab,socket,false);models.Add(model);
            // 장착용 스크립트와 충돌은 전시 복제본에서만 멈춥니다.
            foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
            var renderers=model.GetComponentsInChildren<Renderer>();Bounds bounds=new Bounds();bool found=false;
            foreach(var r in renderers){if(r is ParticleSystemRenderer||r is TrailRenderer||r is LineRenderer){r.enabled=false;continue;}if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);}
            if(found)
            {
                // 긴 축을 세워 손잡이부터 드러나는 실루엣과 무기별 화면 크기를 맞춥니다.
                if(bounds.size.x>bounds.size.y&&bounds.size.x>bounds.size.z)model.transform.RotateAround(bounds.center,Vector3.forward,-90);
                else if(bounds.size.z>bounds.size.y)model.transform.RotateAround(bounds.center,Vector3.right,-90);
                float length=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));model.transform.localScale*=weaponSize/Mathf.Max(.01f,length);
                bounds=new Bounds();found=false;foreach(var r in renderers)if(r.enabled){if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);}model.transform.position+=socket.position-bounds.center;
            }
            foreach(var r in renderers)
            {
                if(!r.enabled)continue;var originals=r.sharedMaterials;var replacements=new Material[originals.Length];
                for(int i=0;i<originals.Length;i++)
                {
                    var source=originals[i];var m=new Material(revealShader);materials.Add(m);group.Add(m);
                    Texture texture=null;foreach(var key in new[]{"_BaseMap","_MainTex","_diffuseColorMap"})if(source&&source.HasProperty(key)&&source.GetTexture(key)){texture=source.GetTexture(key);break;}
                    if(texture)m.SetTexture("_MainTex",texture);m.SetColor("_SoulColor",color);m.SetFloat("_Bottom",transform.position.y-weaponSize*.65f);m.SetFloat("_Height",weaponSize*1.3f);replacements[i]=m;
                }
                r.sharedMaterials=replacements;
            }
        }
        void Update(){if(active){elapsed+=Time.unscaledDeltaTime;if(elapsed>=duration)Completed=true;Tick(Mathf.Clamp01(elapsed/duration));}}
        static float Ease(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        void Reveal(List<Material> group,float amount,Transform socket){foreach(var m in group){m.SetFloat("_Reveal",amount);m.SetFloat("_Bottom",socket.position.y-weaponSize*.65f);}}
        void Tick(float t)
        {
            float dissolve=Ease(.23f,.44f,t),assemble=Ease(.52f,.84f,t),finish=Ease(.82f,.96f,t);
            firstSocket.localPosition=new Vector3(-orbitRadius*(1-.6f*dissolve),displayHeight-.15f+.2f*Mathf.Sin(t*Mathf.PI),0);
            secondSocket.localPosition=new Vector3(orbitRadius*(1-.6f*dissolve),displayHeight-.15f+.2f*Mathf.Sin(t*Mathf.PI),0);
            firstSocket.localRotation=Quaternion.Euler(10,elapsed*rotationSpeed,20);secondSocket.localRotation=Quaternion.Euler(-10,-elapsed*rotationSpeed,-20);
            resultSocket.localPosition=new Vector3(0,displayHeight+.04f*Mathf.Sin(elapsed*1.8f),0);resultSocket.localRotation=Quaternion.Euler(0,elapsed*rotationSpeed,0);
            Reveal(firstMaterials,1-dissolve,firstSocket);Reveal(secondMaterials,1-dissolve,secondSocket);Reveal(resultMaterials,assemble,resultSocket);
            for(int i=0;i<fragments.childCount;i++)
            {
                var shard=fragments.GetChild(i);float phase=i*2.399963f;float spin=elapsed*fragmentSpeed*Mathf.Deg2Rad*(result.boneOrigin==InventoryWeapon.BoneOrigin.Dog?1:1.3f);
                float radius=Mathf.Lerp(orbitRadius,.09f,assemble);Vector3 orbit=new Vector3(Mathf.Cos(phase+spin)*radius,displayHeight-.45f+Mathf.Repeat(i*.173f+t*1.8f,1)*1.05f,Mathf.Sin(phase+spin)*radius);
                var start=new Vector3(i%2==0?-orbitRadius:orbitRadius,displayHeight-.15f,(i%3-1)*.12f);
                shard.localPosition=Vector3.Lerp(start,orbit,Ease(.2f,.36f,t));shard.localRotation=Quaternion.Euler(i*31+elapsed*120,i*67+elapsed*70,i*17);
                float size=(.055f+(i%4)*.015f)*Ease(.2f,.29f,t)*(1-Ease(.70f,.84f,t));shard.localScale=new Vector3(size,size*(2+i%3*.5f),size);
            }
            Ring(outerRing,1.35f,0,elapsed*18,Mathf.Sin(Mathf.PI*Mathf.Min(t,.95f))*.75f+.1f,64);
            Ring(innerRing,1.05f,.02f,-elapsed*27,.5f*(1-finish)+.16f,48);
            float burst=Mathf.InverseLerp(.80f,.96f,t);Ring(shockRing,Mathf.Lerp(.3f,2.7f,burst),.12f,0,t>=.8f?(1-burst)*.85f:0,64);
            if(soulLight){soulLight.color=color;soulLight.intensity=lightIntensity*(.25f+.6f*dissolve+Mathf.Sin(burst*Mathf.PI)*.5f);}
            if(sigilMaterial){sigilMaterial.SetFloat("_Clock",elapsed);sigilMaterial.SetFloat("_Strength",.15f+.6f*dissolve+.3f*finish);}
        }
        void Ring(LineRenderer line,float radius,float height,float turn,float alpha,int count)
        {
            if(!line)return;line.positionCount=count+1;
            for(int i=0;i<=count;i++){float angle=i*Mathf.PI*2/count+turn*Mathf.Deg2Rad;float rune=i%4==0?.045f:0;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*(radius+rune),height,Mathf.Sin(angle)*(radius+rune)));}
            var tint=color;tint.a=alpha;line.startColor=line.endColor=tint;
        }
        void LateUpdate()
        {
            if(!active||!cameraTarget)return;float t=Mathf.Clamp01(elapsed/duration),blend=Ease(0,.2f,t);Vector3 focus=transform.position+Vector3.up*displayHeight;
            Vector3 position=Vector3.Lerp(cameraPosition,Vector3.Lerp(cameraGoal,focus+(cameraGoal-focus)*.90f,Ease(.8f,1,t)),blend);
            float impulse=t>.8f&&t<.94f?Mathf.Sin(Mathf.InverseLerp(.8f,.94f,t)*Mathf.PI)*shake:0;position+=cameraTarget.transform.right*Mathf.Sin(elapsed*80)*impulse;
            cameraTarget.transform.SetPositionAndRotation(position,Quaternion.Slerp(cameraRotation,Quaternion.LookRotation(focus-position),blend));cameraTarget.fieldOfView=Mathf.Lerp(cameraFov,42,blend);
        }
        public void Skip(){if(!active)return;elapsed=duration;Completed=true;Tick(1);}
        public void Release()
        {
            if(released)return;released=true;active=false;
            foreach(var canvas in hiddenHud)if(canvas)canvas.enabled=true;hiddenHud.Clear();
            if(controller&&controllerEnabled){controller.BlendFromCinematic(.45f,cameraFov);controller.enabled=true;}
            else if(cameraTarget){cameraTarget.transform.SetPositionAndRotation(cameraPosition,cameraRotation);cameraTarget.fieldOfView=cameraFov;}
        }
        void OnDisable(){Release();}
        void OnDestroy(){Release();foreach(var m in materials)if(m)Destroy(m);}
    }
}
