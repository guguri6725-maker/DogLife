using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
namespace DogLife.EditorTools
{
    public static class WinterAudioVerification
    {
        static readonly List<string> heard=new List<string>();static GameObject[] subjects;static int step,deaths;static double next;
        static void Heard(string key){heard.Add(key);}
        public static string Start()
        {
            if(!EditorApplication.isPlaying)return "Play mode required";
            if(StoryBook.IsOpen)StoryBook.Instance.Finish();Time.timeScale=1;
            var player=UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();player.GetComponent<CombatHealth>().invulnerable=true;
            heard.Clear();deaths=0;GameAudio.EffectStarted+=Heard;
            subjects=WinterMonsterBuilder.Names.Select((name,i)=>{
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/"+name+".prefab");
                NavMeshHit point;var pos=player.transform.position+Vector3.right*(i*3+4);NavMesh.SamplePosition(pos,out point,30,NavMesh.AllAreas);
                var go=UnityEngine.Object.Instantiate(source,point.position,Quaternion.identity);go.name="Winter QA "+name;
                go.GetComponent<WinterMonsterEnemy>().enabled=false;typeof(WinterMonsterEnemy).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(go.GetComponent<WinterMonsterEnemy>(),null);go.GetComponent<CombatHealth>().Died+=()=>deaths++;return go;
            }).ToArray();step=0;next=EditorApplication.timeSinceStartup+.7;EditorApplication.update+=Tick;return "Winter sound/death verification started";
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish();return;}if(EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.5;
            try
            {
                if(step<4)
                {
                    var state=step==0?WinterMonsterEnemy.Phase.Warning:step==3?WinterMonsterEnemy.Phase.SecondAttack:WinterMonsterEnemy.Phase.Attack;
                    foreach(var go in subjects)
                    {
                        var e=go.GetComponent<WinterMonsterEnemy>();
                        if(step!=2)typeof(WinterMonsterEnemy).GetMethod("Enter",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,new object[]{state});
                        if(step>0)typeof(WinterMonsterEnemy).GetField("elapsed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(e,step==2?1.15f:.5f);
                    }
                    if(step==1){var spirit=subjects[1];WinterIcePatch.Create(spirit.transform.position,3,.9f,3,spirit.GetComponent<CombatHealth>(),null);}
                }
                else if(step==4)
                {
                    foreach(var go in subjects){go.GetComponent<CombatHealth>().Damage(100000);go.GetComponent<CombatHealth>().Damage(100000);}
                }
                else if(step==5)
                {
                    int corpses=UnityEngine.Object.FindObjectsByType<WinterDeathVisual>(FindObjectsSortMode.None).Length;
                    if(corpses!=4||deaths!=4)throw new Exception("Expected 4 unique deaths/corpses; got "+deaths+"/"+corpses);
                    next=EditorApplication.timeSinceStartup+10;
                }
                else
                {
                    if(UnityEngine.Object.FindObjectsByType<WinterDeathVisual>(FindObjectsSortMode.None).Length!=0)throw new Exception("Corpse cleanup failed");
                    string[] required={"Golem_Attack_Charge","Golem_Slam","Golem_Death","SnowSpirit_Charge","SnowSpirit_Freeze_Spread","SnowSpirit_IceField_Loop","SnowSpirit_IceField_End","SnowSpirit_Death","IceWolf_Leap","IceWolf_Land","IceWolf_Claw_Swing","IceWolf_Death","Mammoth_Tusk_Uppercut","Mammoth_Death"};
                    var missing=required.Except(heard).ToArray();if(missing.Length>0)throw new Exception("Missing cues: "+string.Join(",",missing));
                    string report="PASS: 4 unique deaths, 4 surviving animated visuals, cleanup, all required sound cues.\n"+string.Join("\n",heard);
                    System.IO.File.WriteAllText("Builds/WinterAudio/Runtime.txt",report);Debug.Log(report);Finish();EditorApplication.isPaused=true;
                }
                step++;
            }
            catch(Exception ex){Debug.LogError("Winter verification: "+ex);Finish();EditorApplication.isPaused=true;}
        }
        static void Finish(){EditorApplication.update-=Tick;GameAudio.EffectStarted-=Heard;if(subjects!=null)foreach(var go in subjects)if(go)UnityEngine.Object.Destroy(go);}
    }
}

