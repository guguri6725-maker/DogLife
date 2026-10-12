#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife
{
    public sealed class RoomTriangleSurvey:MonoBehaviour
    {
        public string Status {get;private set;}
        readonly List<string> rows=new List<string>();
        readonly Dictionary<Mesh,long> counts=new Dictionary<Mesh,long>();
        readonly Dictionary<string,int> prefs=new Dictionary<string,int>();
        float oldScale;bool oldBackground;
        const string Output="Builds/PolygonSurvey/rooms.csv";
        void Awake(){DontDestroyOnLoad(gameObject);}
        long Tri(Mesh m)
        {
            if(!m)return 0;long n;if(counts.TryGetValue(m,out n))return n;
            for(int i=0;i<m.subMeshCount;i++)if(m.GetTopology(i)==MeshTopology.Triangles)n+=(long)m.GetIndexCount(i)/3;
            counts[m]=n;return n;
        }
        long[] Count()
        {
            var excluded=new HashSet<Renderer>();
            foreach(var lod in FindObjectsByType<LODGroup>(FindObjectsSortMode.None)){
                var levels=lod.GetLODs();for(int i=1;i<levels.Length;i++)foreach(var r in levels[i].renderers)if(r)excluded.Add(r);
                if(levels.Length>0)foreach(var r in levels[0].renderers)if(r)excluded.Remove(r);
            }
            long meshes=0,particles=0,lines=0;
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)){
                if(!r.enabled||!r.gameObject.activeInHierarchy||excluded.Contains(r))continue;
                var skin=r as SkinnedMeshRenderer;if(skin){meshes+=Tri(skin.sharedMesh);continue;}
                var mf=r.GetComponent<MeshFilter>();if(mf){meshes+=Tri(mf.sharedMesh);continue;}
                var psr=r as ParticleSystemRenderer;
                if(psr){var ps=r.GetComponent<ParticleSystem>();if(ps)particles+=ps.particleCount*(psr.renderMode==ParticleSystemRenderMode.Mesh?Tri(psr.mesh):2);continue;}
                var line=r as LineRenderer;if(line)lines+=Math.Max(0,line.positionCount-1)*2;
                var trail=r as TrailRenderer;if(trail)lines+=Math.Max(0,trail.positionCount-1)*2;
            }
            return new[]{meshes,particles,lines};
        }
        IEnumerator Start()
        {
            oldScale=Time.timeScale;oldBackground=Application.runInBackground;Application.runInBackground=true;
            foreach(string id in new[]{"prologue","spring","summer","autumn","winter","life","farewell","revenge"}){string key="DogLife.Memories.v1."+id;prefs[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):-1;}
            Directory.CreateDirectory("Builds/PolygonSurvey");rows.Add("season,room,scene,mesh_min,mesh_max,particle_tri_max,line_tri_est_max,total_sample_max,samples,seconds,boss_state");
            yield return Run();Status="Complete";Restore();
        }
        IEnumerator Run()
        {
            var paths=new[]{"Assets/Scenes/Stage1_Spring.unity","Assets/Scenes/Stage3_Summer.unity","Assets/Scenes/Stage3_Fall.unity","Assets/Scenes/Stage4_Winter.unity"};
            var names=new[]{"Spring","Summer","Fall","Winter"};
            for(int season=0;season<paths.Length;season++){
                Status="Loading "+names[season];Time.timeScale=1;
                yield return SceneManager.LoadSceneAsync(paths[season]);
                var stream=FindFirstObjectByType<ForestRoomStreaming>();
                float deadline=Time.realtimeSinceStartup+120;
                while(stream&&!stream.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
                if(!stream||!stream.Ready){Status="Failed loading "+names[season];yield break;}
                for(int i=0;i<stream.roomScenePaths.Length;i++){
                    Status=names[season]+" "+(i+1)+"/"+stream.roomScenePaths.Length;
                    if(i>0){var routine=(IEnumerator)typeof(ForestRoomStreaming).GetMethod("JumpToSummerBoss",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stream,new object[]{i});yield return stream.StartCoroutine(routine);}
                    var hud=stream.director.hud;var motor=hud.motor;var rb=motor.GetComponent<Rigidbody>();
                    hud.health.minimumHealth=hud.health.maxHealth;hud.health.invulnerable=true;motor.readInput=false;hud.weapons.readInput=false;
                    var room=stream.Current;var target=new Vector3((room.entryX+room.exitX)*.5f,1,0);
                    UnityEngine.AI.NavMeshHit nav;if(UnityEngine.AI.NavMesh.SamplePosition(target,out nav,15,UnityEngine.AI.NavMesh.AllAreas))target=nav.position+Vector3.up;
                    rb.position=target;motor.transform.position=target;
                    foreach(var hp in room.Enemies)if(hp)hp.minimumHealth=hp.maxHealth;
                    Time.timeScale=2;
                    float duration=i==stream.roomScenePaths.Length-1?30:12;
                    float began=Time.time;long min=long.MaxValue,max=0,part=0,line=0,total=0;int samples=0;
                    while(Time.time-began<duration){
                        if(StoryBook.IsOpen)StoryBook.Instance.Finish();
                        var values=Count();min=Math.Min(min,values[0]);max=Math.Max(max,values[0]);part=Math.Max(part,values[1]);line=Math.Max(line,values[2]);total=Math.Max(total,values.Sum());samples++;
                        yield return new WaitForSecondsRealtime(.3f);
                    }
                    var kraken=FindFirstObjectByType<KrakenPhaseOne>();var knight=FindFirstObjectByType<FrozenKnightBoss>();
                    string state=kraken?kraken.State.ToString():knight?"Winter boss sample":"normal/initial phase";
                    rows.Add(string.Join(",",new[]{names[season],(i+1).ToString(),Path.GetFileNameWithoutExtension(stream.roomScenePaths[i]),min.ToString(),max.ToString(),part.ToString(),line.ToString(),total.ToString(),samples.ToString(),duration.ToString(),state}));
                    File.WriteAllLines(Output,rows);Time.timeScale=1;
                }
            }
        }
        void Restore(){Time.timeScale=oldScale;Application.runInBackground=oldBackground;foreach(var p in prefs){if(p.Value<0)PlayerPrefs.DeleteKey(p.Key);else PlayerPrefs.SetInt(p.Key,p.Value);}if(prefs.Count>0)PlayerPrefs.Save();prefs.Clear();}
        void OnDestroy(){Restore();}
    }
}
#endif
