using UnityEngine;
namespace DogLife
{
    public sealed class WinterDeathVisual:MonoBehaviour
    {
        AnimationClip clip;float elapsed,hold;bool spirit;
        Transform[] bones;Vector3[] positions;Quaternion[] rotations;Vector3 originalScale,origin,initialOrigin;
        public void Begin(AnimationClip animation,float linger,bool isSpirit)
        {
            clip=animation;hold=linger;spirit=isSpirit;originalScale=transform.localScale;origin=transform.position;
            initialOrigin=origin;
            if(Physics.Raycast(origin+Vector3.up,Vector3.down,out var ground,12,1<<8,QueryTriggerInteraction.Ignore))origin.y=ground.point.y;
            bones=GetComponentsInChildren<Transform>();positions=new Vector3[bones.Length];rotations=new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;}
        }
        void Update()
        {
            if(!clip)return;elapsed+=Time.deltaTime;
            clip.SampleAnimation(gameObject,Mathf.Min(elapsed,clip.length));
            float blend=Mathf.SmoothStep(0,1,elapsed/.22f);
            transform.position=Vector3.Lerp(initialOrigin,origin,Mathf.SmoothStep(0,1,elapsed/.3f));
            if(blend<1)for(int i=1;i<bones.Length;i++){bones[i].localPosition=Vector3.Lerp(positions[i],bones[i].localPosition,blend);bones[i].localRotation=Quaternion.Slerp(rotations[i],bones[i].localRotation,blend);}
            if(spirit){float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(clip.length*.65f,clip.length,elapsed));transform.localScale=originalScale*Mathf.Lerp(1,.001f,t);}
            if(elapsed>clip.length+hold){float t=(elapsed-clip.length-hold)/1.2f;transform.position=origin-Vector3.up*t*2;if(t>=1)Destroy(gameObject);}
        }
    }
}
