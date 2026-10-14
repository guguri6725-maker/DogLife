using System.Collections;
using UnityEngine;
namespace DogLife
{
    // Room lifetime owns music; phase-one health depletion does not end the track.
    public sealed class BossBattleMusic : MonoBehaviour
    {
        CombatHealth boss,player;string resource;AudioClip clip;bool running;
        public void Prepare(Component actor,string key)
        {
            if(boss)return;
            boss=actor.GetComponent<CombatHealth>();resource="Audio/Music/"+key;
            if(boss)boss.Died+=Stop;
            StartCoroutine(Load());
        }
        IEnumerator Load()
        {
            var request=Resources.LoadAsync<AudioClip>(resource);yield return request;
            clip=request.asset as AudioClip;
            if(running&&clip)GameAudio.PlayMusic(clip,this);
        }
        public void Begin()
        {
            if(running||!boss||boss.Dead)return;
            var motor=FindFirstObjectByType<PlayerMotor>();player=motor?motor.GetComponent<CombatHealth>():null;
            if(player&&player.Dead)return;
            running=true;if(player)player.Died+=Stop;
            if(clip)GameAudio.PlayMusic(clip,this);
        }
        public void Stop()
        {
            running=false;if(player)player.Died-=Stop;player=null;
            GameAudio.StopMusic(this);
        }
        void OnDisable(){Stop();}
        void OnDestroy(){if(boss)boss.Died-=Stop;}
    }
}
