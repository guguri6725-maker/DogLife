using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogLife
{
    // 명시적인 실행 인자가 있을 때만 작동하는 빌드 검증 도구입니다.
    public sealed class StandalonePerformanceCheck : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(!Environment.GetCommandLineArgs().Contains("-doglife-performance-check"))return;
            var go=new GameObject("Standalone performance check");
            DontDestroyOnLoad(go);go.AddComponent<StandalonePerformanceCheck>();
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Stage1_Spring.unity");
            var stream=FindAnyObjectByType<ForestRoomStreaming>();
            double timeout=Time.realtimeSinceStartupAsDouble+90;
            while(stream&&!stream.Ready&&Time.realtimeSinceStartupAsDouble<timeout)yield return null;
            if(!stream||!stream.Ready){Debug.LogError("PERF_CHECK initialization failed");Application.Quit(2);yield break;}
            var motor=stream.director.hud.motor;
            var health=stream.director.hud.health;health.minimumHealth=5;health.Restore();
            motor.readInput=false;
            var cam=Camera.main.GetComponent<PlayerCamera>();cam.readInput=false;
            var combo=motor.GetComponent<BasicWeaponCombat>();
            Debug.Log("PERF_CHECK device="+SystemInfo.graphicsDeviceName+" api="+SystemInfo.graphicsDeviceType+" resolution="+Screen.width+"x"+Screen.height);
            for(int room=0;room<2;room++)
            {
                var samples=new List<float>(1200);float nextAttack=0;
                float began=Time.unscaledTime;
                while(Time.unscaledTime-began<15)
                {
                    float t=Time.unscaledTime-began;
                    motor.MoveInput=new Vector2(Mathf.Sin(t)*.6f,Mathf.Cos(t)*.6f);
                    cam.yaw=90+Mathf.Sin(t*.4f)*55;
                    if(Time.time>nextAttack&&combo){combo.PressAttack();combo.ReleaseAttack();nextAttack=Time.time+.5f;}
                    if(t>2)samples.Add(Time.unscaledDeltaTime*1000);
                    yield return null;
                }
                motor.MoveInput=Vector2.zero;
                samples.Sort();
                Debug.Log("PERF_CHECK room="+room+" count="+samples.Count+" medianMs="+samples[samples.Count/2].ToString("F2")+" p95Ms="+samples[(int)(samples.Count*.95f)].ToString("F2")+" maxMs="+samples.Last().ToString("F2")+" over33="+samples.Count(v=>v>33.3f));
                if(room==1)break;
                foreach(var enemy in stream.Current.Enemies)if(enemy)enemy.Damage(100000);
                timeout=Time.realtimeSinceStartupAsDouble+60;
                do {yield return null;}while((stream.Busy||!stream.Current.forwardVines.IsOpen)&&Time.realtimeSinceStartupAsDouble<timeout);
                if(stream.Busy||!stream.Current.forwardVines.IsOpen){Debug.LogError("PERF_CHECK transition timeout "+stream.LoadError);Application.Quit(3);yield break;}
                var next=FindObjectsByType<ForestRoom>(FindObjectsSortMode.None).Single(r=>r.index==1);
                motor.GetComponent<Rigidbody>().position=new Vector3(next.entryX+5,2.5f,0);
                timeout=Time.realtimeSinceStartupAsDouble+30;
                do {yield return null;}while((stream.CurrentRoom!=1||stream.Busy)&&Time.realtimeSinceStartupAsDouble<timeout);
                if(stream.CurrentRoom!=1||stream.Busy){Debug.LogError("PERF_CHECK advance timeout");Application.Quit(4);yield break;}
            }
            Debug.Log("PERF_CHECK completed");Application.Quit(0);
        }
    }
}
