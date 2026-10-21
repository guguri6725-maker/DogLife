using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife.EditorTools
{
    // 실제 방 로딩/해제를 반복하여 방 1개 제한과 양방향 재방문 상태를 검증합니다.
    public static class PassageStreamingCheck
    {
        public static string Result="Not run";
        static ForestRoomStreaming stream;
        static PlayerMotor motor;
        static int swaps,emptyFrames;
        static void Check(bool value,string message){if(!value)throw new Exception("STREAM CHECK: "+message);}
        static void Count()
        {
            int count=stream.roomScenePaths.Count(p=>SceneManager.GetSceneByPath(p).isLoaded);
            Check(count<=1,"overlapping room scenes: "+count);
            if(count==0)emptyFrames++;
        }
        static IEnumerator Wait(Func<bool> condition,string label,float seconds=60)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(!condition())
            {Count();Check(string.IsNullOrEmpty(stream.LoadError),stream.LoadError);Check(Time.realtimeSinceStartup<end,"timeout "+label);yield return null;}
            Count();
        }
        static void Place(Vector3 p)
        {
            motor.transform.position=p;var body=motor.GetComponent<Rigidbody>();body.position=p;body.linearVelocity=Vector3.zero;
            Camera.main.transform.position=p+new Vector3(-.15f,2,0);Physics.SyncTransforms();
        }
        static IEnumerator Clear()
        {
            if(stream.Current.Cleared)yield break;
            foreach(var gate in stream.Current.sideVines)if(gate)Check(gate.IsClosed&&!gate.IsPassable,"side room open before clear");
            foreach(var enemy in stream.Current.Enemies)if(enemy&&!enemy.Dead)enemy.FatalDamage();
            yield return Wait(()=>stream.Current.Cleared,"room clear");
            yield return new WaitForSeconds(4);
            foreach(var gate in stream.Current.sideVines)if(gate)Check(gate.IsOpen&&gate.IsPassable,"side room did not open after clear");
        }
        static IEnumerator Travel(int target)
        {
            int source=stream.CurrentRoom;bool forward=target>source;
            var passage=UnityEngine.Object.FindObjectsByType<SeasonPassage>(FindObjectsSortMode.None).Single(p=>p.previousRoomIndex==Math.Min(source,target));
            var origin=passage.transform.position;
            Place(origin+new Vector3(forward?7.1f:10.5f,.65f,-3.1f));
            yield return Wait(()=>stream.LoadedRoomCount==0&&!stream.Busy,"release "+source);
            Check(passage.IsRearClosed&&passage.IsForwardClosed,"both seals before unload");
            yield return null;Count();
            Place(origin+new Vector3(forward?10.5f:7.1f,.65f,-3.1f));
            yield return Wait(()=>stream.IsRoomPrepared(target),"load "+target);
            // 문이 열리는 시점부터 반대편 연결 통로와 차단문이 존재해야 합니다.
            var connections=UnityEngine.Object.FindObjectsByType<SeasonPassage>(FindObjectsSortMode.None);
            foreach(int link in new[]{target-1,target})
            {
                if(link<0||link>=stream.roomScenePaths.Length-1)continue;
                var connection=connections.SingleOrDefault(p=>p.previousRoomIndex==link);
                Check(connection&&connection.gameObject.activeInHierarchy,"missing adjacent passage before entry: "+link);
                Check(connection.rearSeal&&connection.forwardSeal,"missing passage doors: "+link);
                Check(connection.GetComponentsInChildren<Collider>().Any(c=>c.enabled),"missing passage collision: "+link);
                Check(connection.GetComponentsInChildren<Renderer>().Any(r=>r.enabled),"missing passage visuals: "+link);
                if(connection!=passage)
                    Check(link==target?connection.IsForwardClosed:connection.IsRearClosed,"unloaded neighbor is exposed: "+link);
            }
            bool revisiting=stream.WasCleared(target);
            Check(revisiting||!stream.Current.CombatStarted,"unvisited room starts early");
            var room=stream.Current;
            foreach(var gate in room.sideVines)if(gate)Check(revisiting?gate.IsOpen:gate.IsClosed,"side room restored incorrectly");
            if(revisiting&&target>0&&room.rearVines)Check(room.rearVines.IsOpen&&room.rearVines.IsPassable,"revisit rear door is animating");
            Place(new Vector3(forward?room.entryX+5:room.exitX-5,2,0));
            yield return Wait(()=>!stream.InPassage,"enter "+target);
            if(revisiting)Check(room.Cleared&&room.Enemies.Length==0,"cleared enemy respawn");
            else
            {
                Check(room.CombatStarted,"new combat did not start");
                if(room.rearVines){yield return Wait(()=>room.rearVines.IsClosed,"combat rear door closing",8);Check(!room.rearVines.IsPassable,"combat rear door passable");}
            }
            swaps++;Count();
        }
        public static IEnumerator Run(ForestRoomStreaming instance)
        {
            stream=instance;Result="Running";swaps=emptyFrames=0;
            yield return Wait(()=>stream.Ready,"initial load");
            var hud=stream.director.hud;motor=hud.motor;var camera=Camera.main.GetComponent<PlayerCamera>();
            bool oldMotor=motor.enabled,oldCamera=camera.enabled,oldInvulnerable=hud.health.invulnerable;
            motor.enabled=false;camera.enabled=false;hud.health.Restore();hud.health.invulnerable=true;Time.timeScale=1;
            // 중첩 코루틴의 실패도 기록하고 플레이어 설정을 복구합니다.
            var routine=Route();var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(routine);
            try
            {
                while(stack.Count>0)
                {
                    object yielded=null;bool moved=false;
                    try{moved=stack.Peek().MoveNext();if(moved)yielded=stack.Peek().Current;}
                    catch(Exception e){Result="FAIL "+e.Message;Debug.LogError(Result);break;}
                    if(!moved){stack.Pop();continue;}
                    if(yielded is IEnumerator){stack.Push((IEnumerator)yielded);continue;}
                    yield return yielded;
                }
                if(!Result.StartsWith("FAIL"))Result="PASS swaps="+swaps+", passage-only frames="+emptyFrames+", max rooms=1, clears="+stream.director.Cleared;
            }
            finally{motor.enabled=oldMotor;camera.enabled=oldCamera;hud.health.invulnerable=oldInvulnerable;}
            Debug.Log(Result);
        }
        static IEnumerator Route()
        {
            yield return Clear();
            foreach(int target in new[]{1,0,1,2,3,2,1,0})
            {
                yield return Travel(target);yield return Clear();
                Check(stream.director.Cleared>=target+1,"clear progress regressed");
            }
            Check(stream.director.Cleared==4,"revisit duplicated progression");
        }
    }
}
