#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife.Verification
{
    // 에디터에서만 명시적으로 실행하는 실제 비동기 씬 전환 통합 검사입니다.
    public sealed class ForestStreamingVerifier : MonoBehaviour
    {
        public bool finished;
        public string failure;
        int maximumRooms;
        int physicalLimitChecks;
        readonly List<object> observations = new List<object>();
        int LoadedRooms() => Enumerable.Range(0, SceneManager.sceneCount).Count(i => SceneManager.GetSceneAt(i).name.StartsWith("Forest_Room_"));
        void Update() { maximumRooms = Mathf.Max(maximumRooms, LoadedRooms()); }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        IEnumerator Start()
        {
            var task = Run();
            while (true)
            {
                object current;
                try { if (!task.MoveNext()) break; current = task.Current; }
                catch (Exception e) { failure = e.ToString(); break; }
                yield return current;
            }
            finished = true;
            var result = new { passed = string.IsNullOrEmpty(failure), failure, maximumRooms, physicalLimitChecks, observations };
            System.IO.Directory.CreateDirectory("ArtSource/ForestVineGates");
            System.IO.File.WriteAllText("ArtSource/ForestVineGates/PlayModeVerification.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            Debug.Log("Forest streaming verification: " + (result.passed ? "PASS" : failure));
        }
        IEnumerator Run()
        {
            var streaming = FindFirstObjectByType<ForestRoomStreaming>();
            float deadline=Time.realtimeSinceStartup+30;
            while(!streaming.Ready && Time.realtimeSinceStartup<deadline)yield return null;
            Check(streaming.Ready,"Bootstrap did not finish");
            var hud=streaming.director.hud;hud.health.invulnerable=true;
            var playerBody=hud.motor.GetComponent<Rigidbody>();
            hud.motor.readInput=false;hud.weapons.readInput=false;hud.motor.movementCamera=null;hud.motor.dashCooldown=0;
            foreach(int side in new[]{-1,1})
            {
                playerBody.position=new Vector3(0,2,side*10);playerBody.linearVelocity=Vector3.up*8;
                hud.motor.MoveInput=new Vector2(0,side);hud.motor.RequestDash();
                yield return new WaitForSeconds(1.2f);
                Check(Mathf.Abs(playerBody.position.z)<12,"Jump/dash crossed forest limit");
                physicalLimitChecks++;
            }
            hud.motor.MoveInput=Vector2.zero;
            hud.motor.enabled=false;hud.weapons.enabled=false;
            var body=hud.motor.GetComponent<Rigidbody>();body.linearVelocity=Vector3.zero;body.isKinematic=true;
            for(int i=0;i<7;i++)
            {
                Check(streaming.CurrentRoom==i,"Wrong current room");
                Check(LoadedRooms()==(i<6?2:1),"Incorrect resident room count");
                var room=streaming.Current;
                Check(room.encounter.activeSelf,"Current encounter inactive");
                Check(room.forwardGate.activeSelf,"Gate opened before combat clear");
                Check(room.forwardVines && room.forwardVines.IsClosed,"Vines must start closed");
                if(i<6)
                {
                    var next=SceneManager.GetSceneByPath(streaming.roomScenePaths[i+1]).GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ForestRoom>()).Single();
                    Check(!next.encounter.activeSelf,"Preloaded encounter active too early");
                }
                if(i>0)
                {
                    Check(room.rearSeal.activeSelf,"Rear seal not closed");
                    Check(room.rearVines && room.rearVines.IsClosed,"Rear vines not fully closed");
                    Check(!SceneManager.GetSceneByPath(streaming.roomScenePaths[i-1]).isLoaded,"Previous room still loaded");
                    foreach(float z in new[]{-23f,0,23f})
                    {
                        var ray=new Ray(new Vector3(room.entryX+3,3,z),Vector3.left);
                        Check(room.rearSeal.GetComponent<Collider>().Raycast(ray,out var hit,5),"Rear sight line exposed");
                    }
                    Check(Camera.main.transform.position.x>=room.entryX+1f-.01f,"Camera crossed rear seal");
                    hud.motor.Respawn();yield return new WaitForFixedUpdate();
                    Check(body.position.x>room.entryX,"Respawn returned to unloaded room");
                }
                var ground=room.GetComponentsInChildren<MeshCollider>().First(c=>c.name.StartsWith("ForestFloor"));
                var limits=room.transform.Find("ForestSideLimits");
                Check(limits && limits.childCount==2,"Missing forest side limits");
                int sideRays=0;
                float edge=i==6?15:12;
                foreach(var wall in limits.GetComponentsInChildren<BoxCollider>())
                foreach(float x in new[]{ground.bounds.min.x+.05f,ground.bounds.center.x,ground.bounds.max.x-.05f})
                foreach(float y in new[]{1f,5f,10f})
                {
                    int side=wall.transform.position.z<0?-1:1;
                    Check(wall.Raycast(new Ray(new Vector3(x,y,0),Vector3.forward*side),out var sideHit,20),"Gap in forest boundary");
                    Check(Mathf.Abs(Mathf.Abs(sideHit.point.z)-edge)<.02f,"Wrong forest boundary width");
                    sideRays++;
                }
                int floorSamples=0;
                for(float x=ground.bounds.min.x+.05f;x<ground.bounds.max.x-.05f;x+=.25f)
                {
                    Check(ground.Raycast(new Ray(new Vector3(x,10,0),Vector3.down),out var hit,20),"Floor gap in room "+i+" at "+x);floorSamples++;
                }
                int agents=room.encounter.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>().Count(a=>a.isOnNavMesh);
                Check(agents==room.encounter.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>().Length,"Enemy off NavMesh");
                observations.Add(new {room=i+1,residentRooms=LoadedRooms(),floorSamples,sideRays,agentsOnNavMesh=agents,rearClosed=room.rearSeal.activeSelf});
                foreach(var hp in room.Enemies) hp.Damage(100000);
                yield return null;yield return null;
                // 개방 중에는 충돌이 유지되고, Blender 클립 재생이 끝나야 통과할 수 있습니다.
                Check(!room.forwardVines.IsOpen,"Vines opened without animation");
                Check(room.forwardVines.blockers.All(c=>c.enabled),"Collision released before animation ended");
                deadline=Time.realtimeSinceStartup+6;
                while(!room.forwardVines.IsOpen && Time.realtimeSinceStartup<deadline)yield return null;
                Check(room.forwardVines.IsOpen,"Vine opening timed out");
                Check(room.forwardVines.blockers.All(c=>!c.enabled),"Open vines still block passage");
                var animatedVine=room.forwardVines.animationRoot.transform.Find("Left_Vine_00");
                Check(animatedVine && Mathf.Abs(animatedVine.localPosition.x)>2,"Blender animation did not move vines");
                Check(streaming.CurrentRoom==i,"Advanced before crossing portal");
                if(i<6)
                {
                    body.position=new Vector3(room.exitX+3.5f,2.5f,0);Physics.SyncTransforms();
                    deadline=Time.realtimeSinceStartup+30;
                    while((streaming.CurrentRoom==i||streaming.Busy)&&Time.realtimeSinceStartup<deadline)yield return null;
                    Check(!streaming.Busy&&streaming.CurrentRoom==i+1,"Transition timed out");
                    yield return new WaitForSeconds(.2f);
                }
                else
                {
                    body.position=room.exit.position;Physics.SyncTransforms();
                    deadline=Time.realtimeSinceStartup+2;
                    while(!hud.completed && Time.realtimeSinceStartup<deadline)yield return new WaitForFixedUpdate();
                    Check(hud.completed,"Stage completion failed");
                }
            }
            Check(maximumRooms<=2,"More than two rooms were resident");
            Check(streaming.director.Cleared==7,"Incorrect completion count");
        }
    }
}
#endif
