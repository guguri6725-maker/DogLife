using UnityEngine;
namespace DogLife
{
    // Times match the five-second Blender collapse: brace, knees, weapon, torso contact.
    public sealed class HarvestReaperDeathAudio : MonoBehaviour
    {
        [Range(0,1)] public float volume=.75f;
        [SerializeField] float releaseTime=.05f,kneelTime=1.8f,weaponTime=3.65f,fallTime=4f;
        float age;int played;
        void Update()
        {
            if(Time.timeScale<=0)return;age+=Time.deltaTime;
            Cue(0,releaseTime,"Reaper_Death_Release");Cue(1,kneelTime,"Reaper_Death_Kneel");
            Cue(2,weaponTime,"Reaper_Scythe_Drop");Cue(3,fallTime,"Reaper_Death_Fall");
            if(played==15)enabled=false;
        }
        void Cue(int bit,float time,string key)
        {if(age<time||(played&(1<<bit))!=0)return;played|=1<<bit;GameAudio.Play(key,transform.position,volume,follow:transform);}
    }
}
