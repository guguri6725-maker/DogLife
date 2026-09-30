#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 실제 방의 스폰 흐름으로 입장/탑승을 재생하고 주요 자세를 저장합니다.
    public static class FrozenKnightCinematicVerification
    {
        static FrozenKnightBoss boss;static float started;static bool entered,transition;
        static bool collapsed,horseSwordOverlap;
        static bool deathTest,finalStarted,lastHit;static int deathEvents;
        public static string StartDeath(){var result=Start();deathTest=true;return result;}
        static readonly System.Collections.Generic.HashSet<string> captured=new System.Collections.Generic.HashSet<string>();
        public static string Start()
        {
            if(!Application.isPlaying)return "Play mode required";
            captured.Clear();entered=transition=collapsed=horseSwordOverlap=false;started=Time.time;
            deathTest=finalStarted=lastHit=false;deathEvents=0;
            Object.FindFirstObjectByType<ForestRoom>().BeginEncounter();
            boss=Object.FindFirstObjectByType<FrozenKnightBoss>();
            if(!boss)return "Boss failed to spawn";
            boss.GetComponent<CombatHealth>().Died+=()=>deathEvents++;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            return "Cinematic verification started";
        }
        static void Tick()
        {
            if(!Application.isPlaying||!boss){EditorApplication.update-=Tick;return;}
            if(deathTest&&lastHit&&boss.GetComponent<CombatHealth>().Dead)
            {
                Snapshot("CorpseFinal");
                Debug.Log("DEATH CHECK: body retained="+boss.gameObject.activeInHierarchy);
                Debug.Log("DEATH CHECK: completed, deathEvents="+deathEvents+", player restored="+Object.FindFirstObjectByType<PlayerMotor>().enabled);
                EditorApplication.update-=Tick;EditorApplication.isPaused=true;return;
            }
            var animator=boss.GetComponentsInChildren<Animator>(true).First(a=>a.name=="Knight");var pose=animator.GetCurrentAnimatorStateInfo(0);
            var patch=boss.GetComponentInChildren<WinterIcePatch>(true);
            if(patch&&patch.IsActive&&boss.PhaseNumber==1&&patch.Growth>.5f&&captured.Add("IceSpread"))
            {Snapshot("IceSpread");Debug.Log("ICE CHECK: field spreads before mounting");}
            if(transition&&boss.State==FrozenKnightBoss.CombatState.Transition)
            {
                collapsed|=pose.IsName("Kneel")||pose.IsName("StandUp");
                var so=new SerializedObject(boss);
                if(so.FindProperty("swordPlanted")==null)
                {
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    bool planted=(bool)typeof(FrozenKnightBoss).GetField("swordPlanted",flags).GetValue(boss);
                    if(planted)
                    {
                        var sword=(Transform)so.FindProperty("swordRoot").objectReferenceValue;var horse=(Transform)so.FindProperty("horseRoot").objectReferenceValue;
                        var sr=sword.GetComponentInChildren<MeshRenderer>();var hr=horse.GetComponentInChildren<SkinnedMeshRenderer>();
                        if(sr&&hr)horseSwordOverlap|=sr.bounds.Intersects(hr.bounds);
                    }
                }
            }
            foreach(string state in new[]{"ThroneIdle","ThroneRise","ThroneDraw1","ThroneDraw2","CinematicMount","MountedDeath"})
            {
                if(!pose.IsName(state))continue;
                foreach(float at in new[]{.1f,.5f,.9f})
                    if(pose.normalizedTime>=at&&pose.normalizedTime<at+.08f&&captured.Add(state+at))Snapshot(state+at.ToString("F1",System.Globalization.CultureInfo.InvariantCulture));
            }
            entered|=boss.IntroductionRunning;
            if(entered&&!boss.IntroductionRunning&&!transition)
            {
                Debug.Log("CINEMATIC: entry complete, player restored="+Object.FindFirstObjectByType<PlayerMotor>().enabled);
                transition=true;boss.GetComponent<CombatHealth>().Damage(999999);
            }
            if(transition&&boss.PhaseNumber==2&&boss.State==FrozenKnightBoss.CombatState.Idle)
            {
                if(deathTest&&!finalStarted){finalStarted=true;boss.GetComponent<CombatHealth>().Damage(999999);return;}
                if(deathTest)return;
                Snapshot("MountedComplete");Debug.Log("CINEMATIC PASS: entry and mounting finished, player restored="+Object.FindFirstObjectByType<PlayerMotor>().enabled);
                Debug.Log("BOARDING CHECK: collapse="+collapsed+", horse/sword bounds overlap="+horseSwordOverlap);
                EditorApplication.update-=Tick;EditorApplication.isPaused=true;
            }
            if(deathTest&&finalStarted&&!lastHit)
            {
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var core=(FrozenKnightSwordCore)typeof(FrozenKnightBoss).GetField("swordCore",flags).GetValue(boss);
                if(core&&(bool)typeof(FrozenKnightSwordCore).GetField("active",flags).GetValue(core)){core.ReceiveSpear();core.ReceiveSpear();core.ReceiveSpear();}
                var hp=boss.GetComponent<CombatHealth>();
                if(core&&core.Broken&&hp.Current==1&&!hp.invulnerable)
                {lastHit=true;hp.Damage(.1f);Debug.Log("DEATH CHECK: final hit, delayed death="+(!hp.Dead&&deathEvents==0&&boss.gameObject.activeInHierarchy));}
            }
            if(Time.time-started>85){Debug.LogError("CINEMATIC TIMEOUT "+boss.State);EditorApplication.update-=Tick;EditorApplication.isPaused=true;}
        }
        static void Snapshot(string name)
        {
            var camera=Camera.main;if(!camera)return;
            var previous=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24);var tex=new Texture2D(960,540,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();Directory.CreateDirectory("Temp/FrozenKnight/Cinematics");File.WriteAllBytes("Temp/FrozenKnight/Cinematics/"+name+".png",tex.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
        }
    }
}
#endif
