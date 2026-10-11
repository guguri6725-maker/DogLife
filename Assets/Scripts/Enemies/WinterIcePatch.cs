using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    public sealed class WinterIcePatch:MonoBehaviour
    {
        static readonly List<WinterIcePatch> active=new List<WinterIcePatch>();
        CombatHealth owner;float born,lifetime,acceleration;bool initialized,reusable;
        public float Radius {get;private set;}
        public bool IsActive=>initialized;
        public float Age=>Time.time-born;
        float growthDuration=.65f;
        GameAudio.Handle fieldSound;bool hasSound;
        public float Growth=>Mathf.SmoothStep(0,1,Mathf.Clamp01(Age/growthDuration));
        // 연출용 빙판도 실제 표면과 같은 속도로 바깥으로 퍼집니다.
        public void SetGrowthDuration(float duration){growthDuration=Mathf.Max(.01f,duration);}
        public float Opacity=>Mathf.Clamp01((lifetime-Age)/.45f);
        public static void Create(Vector3 position,float radius,float duration,float steering,CombatHealth source,Material material)
        {
            var go=new GameObject("Winter slippery ice");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,source.gameObject.scene);go.transform.position=position;
            var patch=go.AddComponent<WinterIcePatch>();patch.Initialize(source,radius,duration,steering,false);
            var fx=go.AddComponent<WinterAttackVfx>();fx.patch=patch;fx.Initialize(material);
        }
        void OnEnable(){active.Add(this);}
        void OnDisable(){active.Remove(this);EndSound();}
        void EndSound(){fieldSound.Stop();if(hasSound&&Application.isPlaying){hasSound=false;GameAudio.Play("SnowSpirit_IceField_End",transform.position,.4f);}}
        // 보스 프리팹에 미리 배치한 빙판도 기존 이동 표면 처리에 등록합니다.
        public void Initialize(CombatHealth source,float radius,float duration,float steering,bool reuse=true)
        {owner=source;Radius=radius;lifetime=duration;acceleration=steering;born=Time.time;initialized=true;reusable=reuse;
            if(!reuse&&source&&source.GetComponent<WinterMonsterEnemy>()){hasSound=true;GameAudio.Play("SnowSpirit_Freeze_Spread",transform.position,.65f);fieldSound=GameAudio.Play("SnowSpirit_IceField_Loop",transform.position,.2f,follow:transform,loop:true);}}
        public void Clear(){initialized=false;EndSound();}
        void Update(){if(initialized&&(!owner||owner.Dead||!owner.isActiveAndEnabled||Age>=lifetime)){if(reusable)Clear();else Destroy(gameObject);}}
        public static Vector3 ApplySurface(Vector3 foot,Vector3 velocity,Vector3 desired,float dt)
        {
            float steering=float.MaxValue;
            foreach(var patch in active)
            {
                if(!patch||!patch.initialized||Mathf.Abs(foot.y-patch.transform.position.y)>.45f)continue;
                Vector3 d=Vector3.ProjectOnPlane(foot-patch.transform.position,Vector3.up);
                if(d.sqrMagnitude<=Mathf.Pow(patch.Radius*patch.Growth,2))steering=Mathf.Min(steering,patch.acceleration);
            }
            // Preserve top speed, but changing direction/braking takes time only on ice.
            return steering<float.MaxValue?Vector3.MoveTowards(Vector3.ProjectOnPlane(velocity,Vector3.up),desired,steering*dt):desired;
        }
    }
}
