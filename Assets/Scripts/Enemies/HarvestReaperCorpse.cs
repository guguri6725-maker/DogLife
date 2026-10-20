using UnityEngine;
namespace DogLife
{
    public sealed class HarvestReaperCorpse:MonoBehaviour
    {
        AnimationClip clip;float age,blend;
        Transform[] bones;Vector3[] positions;Quaternion[] rotations;
        public void Begin(AnimationClip value,float seconds)
        {
            if(!GetComponent<HarvestReaperDeathAudio>())gameObject.AddComponent<HarvestReaperDeathAudio>();
            clip=value;blend=seconds;bones=GetComponentsInChildren<Transform>();
            // 원래 보스가 제거되어도 사망 동작이 끝날 때까지 UI 숨김을 유지합니다.
            if(clip)BossCinematicUI.Begin(this);
            positions=new Vector3[bones.Length];rotations=new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;}
        }
        void Update()
        {
            if(!clip)return;
            age+=Time.deltaTime;clip.SampleAnimation(gameObject,Mathf.Min(age,clip.length));
            float t=Mathf.SmoothStep(0,1,age/Mathf.Max(.01f,blend));
            if(t<1)for(int i=1;i<bones.Length;i++)
            {bones[i].localPosition=Vector3.Lerp(positions[i],bones[i].localPosition,t);bones[i].localRotation=Quaternion.Slerp(rotations[i],bones[i].localRotation,t);}
            if(age>=clip.length)enabled=false;
        }
        void OnDisable(){BossCinematicUI.End(this);}
    }
}
