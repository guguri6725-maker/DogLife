using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    [DefaultExecutionOrder(-200)]
    public sealed class BossMemoryPickup:MonoBehaviour
    {
        [SerializeField,Min(.5f)] float interactionDistance=3;
        [SerializeField] KeyCode interactionKey=KeyCode.F;
        public bool Collected {get;private set;}
        static int consumedFrame=-1;
        public static bool InputConsumed=>Time.frameCount==consumedFrame;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInput(){consumedFrame=-1;}
        StageOneDirector director;Transform crystal;Material material;Mesh mesh;
        Vector3 resting;float age;GUIStyle prompt;Font font;
        GameAudio.Handle ambience;
        public static BossMemoryPickup Create(StageOneDirector owner,StoryChapter chapter)
        {
            var go=new GameObject("Boss Memory — "+chapter.id);
            SceneManager.MoveGameObjectToScene(go,owner.gameObject.scene);
            var pickup=go.AddComponent<BossMemoryPickup>();pickup.director=owner;
            var player=owner.hud.motor?owner.hud.motor.transform:owner.hud.health.transform;Vector3 point=player.position;
            var room=owner.streaming?owner.streaming.Current:null;
            if(room&&room.Enemies!=null)foreach(var h in room.Enemies)if(h&&!h.GetComponent<KrakenPhaseTentacle>()){point=h.transform.position;break;}
            var towards=player.position-point;towards.y=0;
            point+=towards.normalized*4;
            UnityEngine.AI.NavMeshHit nav;
            if(UnityEngine.AI.NavMesh.SamplePosition(point,out nav,8,UnityEngine.AI.NavMesh.AllAreas))point=nav.position;
            else if(Physics.Raycast(point+Vector3.up*2,Vector3.down,out var hit,12,1<<8,QueryTriggerInteraction.Ignore))point=hit.point;
            pickup.resting=point+Vector3.up*1.1f;
            // 수중 크라켄의 루트/바닥은 시체 안에 있으므로 플레이어의 수영 높이에 놓습니다.
            var swim=player.GetComponent<PlayerWaterMovement>();
            if(swim&&swim.Submerged&&room&&room.Enemies!=null)
                foreach(var enemy in room.Enemies)
                {
                    var kraken=enemy?enemy.GetComponent<KrakenPhaseTwo>():null;
                    if(!kraken||!enemy.Dead)continue;
                    Vector3 away=player.position-kraken.Center;away.y=0;
                    if(away.sqrMagnitude<.01f){away=-player.forward;away.y=0;}
                    if(away.sqrMagnitude<.01f)away=Vector3.left;
                    Vector3 floating=player.position+away.normalized*2+Vector3.up*.6f;
                    var arena=kraken.Arena;
                    floating.x=Mathf.Clamp(floating.x,arena.min.x+1,arena.max.x-1);
                    floating.z=Mathf.Clamp(floating.z,arena.min.z+1,arena.max.z-1);
                    floating.y=Mathf.Clamp(floating.y,arena.min.y+1,swim.Surface-1);
                    pickup.resting=floating;
                    break;
                }
            go.transform.position=pickup.resting;
            pickup.Build(chapter.accent);
            pickup.ambience=GameAudio.Play("Memory_Object_Loop",go.transform.position,.32f,follow:go.transform,loop:true,spatialRange:12);
            return pickup;
        }
        void Build(Color tint)
        {
            font=StoryBook.Instance.Library.font;
            material=new Material(Shader.Find("Standard"));material.color=Color.Lerp(tint,Color.white,.4f);
            material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",tint*2.8f);material.SetFloat("_Glossiness",.8f);
            mesh=new Mesh {name="Memory crystal"};
            var v=new[]{new Vector3(0,.6f,0),new Vector3(.25f,0,0),new Vector3(0,0,.25f),new Vector3(-.25f,0,0),new Vector3(0,0,-.25f),new Vector3(0,-.45f,0)};
            mesh.vertices=v;mesh.triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};mesh.RecalculateNormals();mesh.RecalculateBounds();
            crystal=new GameObject("Luminous memory crystal",typeof(MeshFilter),typeof(MeshRenderer)).transform;crystal.SetParent(transform,false);
            crystal.GetComponent<MeshFilter>().sharedMesh=mesh;crystal.GetComponent<Renderer>().sharedMaterial=material;
            for(int i=0;i<8;i++){
                var mote=new GameObject("Orbiting glimmer",typeof(MeshFilter),typeof(MeshRenderer)).transform;mote.SetParent(crystal,false);
                float a=i*Mathf.PI/4;mote.localPosition=new Vector3(Mathf.Cos(a)*.65f,Mathf.Sin(a*2)*.22f,Mathf.Sin(a)*.65f);mote.localScale=Vector3.one*.10f;
                mote.GetComponent<MeshFilter>().sharedMesh=mesh;mote.GetComponent<Renderer>().sharedMaterial=material;
            }
            var light=gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=tint;light.range=4;light.intensity=2;light.shadows=LightShadows.None;
        }
        bool CanInteract=>director&&!director.hud.health.Dead&&!Collected&&age>.7f&&!SkillWindow.BlocksGameplayInput&&Vector3.Distance(director.hud.motor?director.hud.motor.transform.position:director.hud.health.transform.position,transform.position)<=interactionDistance;
        public bool TryInteract()
        {
            if(!CanInteract)return false;
            Collected=true;ambience.Stop();consumedFrame=Time.frameCount;return true;
        }
        void Update()
        {
            if(!director||director.hud.health.Dead){Destroy(gameObject);return;}
            age+=Time.deltaTime;
            transform.position=resting+Vector3.up*(Mathf.Pow(Mathf.Max(0,1-age),2)*1.4f+Mathf.Sin(age*2)*.12f);
            crystal.localRotation=Quaternion.Euler(0,age*35,8*Mathf.Sin(age));
            if(CanInteract&&Input.GetKeyDown(interactionKey))TryInteract();
        }
        void OnGUI()
        {
            if(!CanInteract)return;
            if(prompt==null)prompt=new GUIStyle(GUI.skin.box){font=font,fontSize=22,alignment=TextAnchor.MiddleCenter};
            GUI.Box(new Rect(Screen.width*.5f-160,Screen.height*.72f,320,48),"[F] 추억 살펴보기",prompt);
        }
        void OnDisable(){ambience.Stop();}
        void OnDestroy(){ambience.Stop();if(material)Destroy(material);if(mesh)Destroy(mesh);}
    }
}
