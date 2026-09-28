using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    public sealed class WinterIcePatch:MonoBehaviour
    {
        static readonly List<WinterIcePatch> active=new List<WinterIcePatch>();
        CombatHealth owner;float born,lifetime,acceleration;
        public float Radius {get;private set;}
        public float Age=>Time.time-born;
        public float Growth=>Mathf.SmoothStep(0,1,Mathf.Clamp01(Age/.65f));
        public float Opacity=>Mathf.Clamp01((lifetime-Age)/.45f);
        public static void Create(Vector3 position,float radius,float duration,float steering,CombatHealth source,Material material)
        {
            var go=new GameObject("Winter slippery ice");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,source.gameObject.scene);go.transform.position=position;
            var patch=go.AddComponent<WinterIcePatch>();patch.owner=source;patch.Radius=radius;patch.lifetime=duration;patch.acceleration=steering;patch.born=Time.time;
            var fx=go.AddComponent<WinterAttackVfx>();fx.patch=patch;fx.Initialize(material);
        }
        void OnEnable(){active.Add(this);}
        void OnDisable(){active.Remove(this);}
        void Update(){if(!owner||owner.Dead||!owner.isActiveAndEnabled||Age>=lifetime)Destroy(gameObject);}
        public static Vector3 ApplySurface(Vector3 foot,Vector3 velocity,Vector3 desired,float dt)
        {
            float steering=float.MaxValue;
            foreach(var patch in active)
            {
                if(!patch||Mathf.Abs(foot.y-patch.transform.position.y)>.45f)continue;
                Vector3 d=Vector3.ProjectOnPlane(foot-patch.transform.position,Vector3.up);
                if(d.sqrMagnitude<=Mathf.Pow(patch.Radius*patch.Growth,2))steering=Mathf.Min(steering,patch.acceleration);
            }
            // Preserve top speed, but changing direction/braking takes time only on ice.
            return steering<float.MaxValue?Vector3.MoveTowards(Vector3.ProjectOnPlane(velocity,Vector3.up),desired,steering*dt):desired;
        }
    }
}
