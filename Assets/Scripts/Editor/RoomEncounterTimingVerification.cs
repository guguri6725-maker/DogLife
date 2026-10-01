#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 실제 스트리밍에서 다음 방 대기와 문 닫힘 시 AI 해제를 검증합니다.
    public static class RoomEncounterTimingVerification
    {
        static int step;static double started,sampled;static ForestRoom next;
        static Transform[] actors;static Vector3[] positions;static bool closingStart;
        public static string Start()
        {
            step=0;started=EditorApplication.timeSinceStartup;next=null;closingStart=false;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;return "Room timing verification started";
        }
        static void Finish(string message,bool error=false)
        {
            if(error)Debug.LogError(message);else Debug.Log(message);
            EditorApplication.update-=Tick;EditorApplication.isPaused=true;
        }
        static void Tick()
        {
            if(!Application.isPlaying){EditorApplication.update-=Tick;return;}
            if(EditorApplication.timeSinceStartup-started>150){Finish("ROOM CHECK timeout",true);return;}
            var stream=ForestRoomStreaming.Instance;if(!stream||!stream.Ready||stream.Busy)return;
            var player=Object.FindFirstObjectByType<PlayerMotor>();if(!player)return;
            player.GetComponent<CombatHealth>().invulnerable=true;
            if(step==0)
            {
                foreach(var hp in stream.Current.Enemies)if(hp&&!hp.Dead)hp.FatalDamage();step=1;return;
            }
            if(step==1)
            {
                next=Object.FindObjectsByType<ForestRoom>(FindObjectsSortMode.None).FirstOrDefault(r=>r.index==1);
                if(!next||next.Enemies==null)return;
                if(next.CombatStarted){Finish("ROOM CHECK: next room engaged before entry",true);return;}
                actors=next.Enemies.Where(h=>h).Select(h=>h.transform).ToArray();positions=actors.Select(t=>t.position).ToArray();sampled=EditorApplication.timeSinceStartup;step=2;return;
            }
            if(step==2&&EditorApplication.timeSinceStartup-sampled>1.5)
            {
                float drift=actors.Select((t,i)=>Vector3.Distance(t.position,positions[i])).DefaultIfEmpty(0).Max();
                if(drift>.05f){Finish("ROOM CHECK: premature movement "+drift,true);return;}
                player.GetComponent<Rigidbody>().position=new Vector3(next.entryX+4,2,0);
                player.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;
                Physics.SyncTransforms();
                sampled=EditorApplication.timeSinceStartup;step=3;return;
            }
            if(step==3&&next.CombatStarted)
            {
                closingStart=true;sampled=EditorApplication.timeSinceStartup;step=4;
            }
            if(step==4&&EditorApplication.timeSinceStartup-sampled>2)
            {
                float moved=actors.Select((t,i)=>Vector3.Distance(t.position,positions[i])).DefaultIfEmpty(0).Max();
                Finish("ROOM CHECK: "+next.gameObject.scene.path+", waited before entry=True, started="+closingStart+", movement="+moved,moved<.05f);
            }
        }
    }
}
#endif
